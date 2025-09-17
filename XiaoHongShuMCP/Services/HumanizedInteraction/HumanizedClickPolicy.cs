using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using Microsoft.Extensions.Options;
using HushOps.Core.Observability;

namespace XiaoHongShuMCP.Services;

/// <summary>
/// 拟人化点击策略（统一封装点击准备、回退与兜底）。
/// - 点击前：ScrollIntoViewIfNeeded + 轻量可点性检测 + Hover + 拟人化停顿；
/// - 首选：element.ClickAsync();
/// - 兜底：DOM dispatchEvent('click') → 坐标点击（中心点），并在每步之间加入人性化等待；
/// - 不包含业务层验证（如计数/图标变化），该验证由调用者负责。
/// </summary>
using HushOps.Core.Automation.Abstractions;
using HushOps.Core.Runtime.Playwright;
using HushOps.Core.Runtime.Playwright.AntiDetection;

public class HumanizedClickPolicy : IHumanizedClickPolicy
{
    private readonly HushOps.Core.Humanization.IDelayManager _delay;
    private readonly HushOps.Core.Humanization.IClickabilityDetector _detector;
    private readonly ILogger<HumanizedClickPolicy>? _logger;
    private readonly HushOps.Core.Humanization.IDomPreflightInspector _preflight;
    private readonly IMetrics? _metrics;
    private readonly bool _allowJsInjectionFallback;
    private readonly ICounter? _uiInjectionCounter;
    private readonly HushOps.Core.Humanization.IPacingAdvisor? _pacing;
    private readonly ICounter? _mTrajSteps;
    private readonly IHistogram? _hTrajMs;
    private readonly ICounter? _mTrajHotspots;
    private readonly IHistogram? _hTrajStepPx;
    private readonly IPlaywrightAntiDetectionPipeline _anti;

    public HumanizedClickPolicy(
        HushOps.Core.Humanization.IDelayManager delayManager,
        HushOps.Core.Humanization.IClickabilityDetector detector,
        HushOps.Core.Humanization.IDomPreflightInspector preflight,
        IOptions<XhsSettings>? xhsOptions = null,
        IMetrics? metrics = null,
        ILogger<HumanizedClickPolicy>? logger = null,
        HushOps.Core.Humanization.IPacingAdvisor? pacing = null,
        IPlaywrightAntiDetectionPipeline? anti = null)
    {
        _delay = delayManager;
        _detector = detector;
        _logger = logger;
        _preflight = preflight;
        _metrics = metrics;
        _pacing = pacing;
        _allowJsInjectionFallback = xhsOptions?.Value?.InteractionPolicy?.EnableJsInjectionFallback ?? false;
        _uiInjectionCounter = _metrics?.CreateCounter("ui_injection_total", "UI 注入兜底使用计数（应为0）");
        _mTrajSteps = _metrics?.CreateCounter("trajectory_steps_total", "轨迹步数计数（坐标点击路径）");
        _hTrajMs = _metrics?.CreateHistogram("trajectory_duration_ms", "轨迹执行总时长（毫秒）");
        _mTrajHotspots = _metrics?.CreateCounter("trajectory_hotspot_pauses_total", "轨迹热点停顿计数");
        _hTrajStepPx = _metrics?.CreateHistogram("trajectory_step_length_px", "轨迹单步像素距离");
        _anti = anti ?? new DefaultPlaywrightAntiDetectionPipeline();
    }

    public async Task<ClickDecision> ClickAsync(IAutoPage page, IAutoElement element, CancellationToken ct = default)
    {
        var decision = new ClickDecision();
        // Step 0: 进入可视区
        try { await element.ScrollIntoViewIfNeededAsync(ct); } catch { }

        // Step 1a: 语义预检（禁用/加载/aria 等）
        var pre = await _preflight.InspectAsync(element, ct);
        decision.PreflightReady = pre.IsReady;
        decision.PreflightReason = pre.Reason;
        if (!pre.IsReady)
        {
            _logger?.LogDebug("[Preflight] not ready: {reason}", pre.Reason);
            // 对忙碌/加载类原因允许一次短等待后复检
            if (pre.IsBusy)
            {
                await _delay.WaitAsync(HushOps.Core.Humanization.HumanWaitType.ContentLoading, cancellationToken: ct);
                pre = await _preflight.InspectAsync(element, ct);
                decision.PreflightReady = pre.IsReady;
                decision.PreflightReason = pre.Reason;
            }
        }
        // 若明确禁用且复检仍禁用，则直接抛错，避免无意义点击
        if (!pre.IsReady && pre.IsDisabled)
        {
            throw new InvalidOperationException($"元素处于禁用态，放弃点击：{pre.Reason}");
        }

        // Step 1b: 物理可点性评估
        var rep = await _detector.AssessAsync(element, ct);
        _logger?.LogDebug("[Clickability] clickable={clickable}, reason={reason}", rep.IsClickable, rep.Reason);

        // Step 2: Hover + 思考停顿
        await SafeHover(element);
        await _delay.WaitAsync(HushOps.Core.Humanization.HumanWaitType.ClickPreparation, cancellationToken: ct);

        // 尝试顺序：常规 → dispatchEvent → 坐标
        var steps = new (string Name, Func<Task<bool>> Hit)[]
        {
            ("regular", async () => await TryRegularClick(element)),
            ("dispatch", async () => await TryDispatchClick(element)),
            ("coordinate", async () => await TryCoordinateClick(page, element))
        };

        Exception? lastError = null;
        for (int i = 0; i < steps.Length; i++)
        {
            try
            {
                decision.StepsTried.Add(steps[i].Name);
                var ok = await steps[i].Hit();
                if (ok)
                {
                    decision.Success = true;
                    decision.Path = steps[i].Name;
                    decision.Attempts = i + 1;
                    return decision;
                }
            }
            catch (Exception ex)
            {
                lastError = ex;
                _logger?.LogDebug(ex, "点击尝试失败 #{i}", i + 1);
            }
            await _delay.WaitAsync(HushOps.Core.Humanization.HumanWaitType.RetryBackoff, i + 1, ct);
        }

        // 若三次尝试仍失败，抛出最后一次异常或统一异常
        decision.Success = false;
        decision.Path = "none";
        decision.Attempts = steps.Length;
        throw lastError ?? new Exception("点击失败：所有兜底路径均未成功");
    }

    private async Task<bool> TryRegularClick(IAutoElement element)
    {
        try
        {
            await element.ClickAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> TryDispatchClick(IAutoElement element)
    {
        if (!_allowJsInjectionFallback)
        {
            _logger?.LogDebug("已禁用 JS 注入兜底（dispatchEvent）");
            return false;
        }
        try
        {
            var handle = await PlaywrightAutoFactory.TryUnwrapAsync(element);
            if (handle is null) return false;
            var ok = await _anti.TryUiInjectionAsync(handle, "click.dispatchEvent", async h =>
            {
                await h.EvaluateAsync("(el)=>{ el.dispatchEvent(new MouseEvent('click', {bubbles:true, cancelable:true, view:window})); }");
            });
            if (ok)
            {
                _uiInjectionCounter?.Add(1, LabelSet.From(("type", "dispatchEvent"), ("path", "click")));
            }
            return ok;
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "受控 JS 注入兜底失败");
            return false;
        }
    }

    private static (double x, double y)? _lastMouse;

    private async Task<bool> TryCoordinateClick(IAutoPage page, IAutoElement element)
    {
        try
        {
            var center = await element.GetCenterAsync();
            if (center == null) return false;
            // 使用人类化轨迹生成器按步移动并点击（无 JS 注入）。
            var gen = new HushOps.Core.Humanization.MinimumJerkTrajectoryGenerator();
            var speedMult = _pacing?.CurrentMultiplier ?? 1.0;
            var t0 = System.Diagnostics.Stopwatch.StartNew();
            var path = gen.Generate(_lastMouse, (center.Value.x, center.Value.y), speedMult);
            double? lastX = _lastMouse?.x; double? lastY = _lastMouse?.y;
            int hotspots = 0;
            foreach (var pt in path)
            {
                await page.MouseMoveAsync(pt.X, pt.Y);
                if (lastX is not null && lastY is not null)
                {
                    var dx = pt.X - lastX.Value; var dy = pt.Y - lastY.Value;
                    var step = Math.Sqrt(dx*dx + dy*dy);
                    try { _hTrajStepPx?.Record(step, LabelSet.From(("path", "coordinate"))); } catch { }
                }
                lastX = pt.X; lastY = pt.Y;
                if (pt.PauseMs > 0) await Task.Delay(pt.PauseMs);
                if (pt.PauseMs > 0) hotspots++;
            }
            // 目标点微随机偏移，避免总点中心
            var jitterX = (Random.Shared.NextDouble() - 0.5) * 1.2;
            var jitterY = (Random.Shared.NextDouble() - 0.5) * 1.2;
            var tx = center.Value.x + jitterX;
            var ty = center.Value.y + jitterY;
            await page.MouseClickAsync(tx, ty);
            _lastMouse = (tx, ty);
            try
            {
                _mTrajSteps?.Add(path.Count, LabelSet.From(("path", "coordinate")));
                t0.Stop();
                _hTrajMs?.Record(t0.Elapsed.TotalMilliseconds, LabelSet.From(("path", "coordinate")));
                if (hotspots > 0) _mTrajHotspots?.Add(hotspots, LabelSet.From(("path", "coordinate")));
            }
            catch { }
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task SafeHover(IAutoElement element)
    {
        try { await element.HoverAsync(); }
        catch { /* 忽略 hover 失败 */ }
    }
}
