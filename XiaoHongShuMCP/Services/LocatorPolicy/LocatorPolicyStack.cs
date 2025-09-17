using System.Text;
using HushOps.Core.Automation.Abstractions;
using HushOps.Core.Observability;

// 说明：命名空间迁移至 HushOps.Services。
namespace XiaoHongShuMCP.Services;

/// <summary>
/// 默认定位策略栈实现（基于 Playwright 选择器语法特性，但通过抽象 IAutoPage 调用）。
/// 注意：不使用 JS 注入；仅使用 Locator 引擎的 CSS/文本/has-text 能力。
/// </summary>
public sealed class LocatorPolicyStack : ILocatorPolicyStack
{
    private readonly IDomElementManager _dom;
    private readonly IElementFinder _finder;
    private readonly IMetrics? _metrics;
    private readonly HushOps.Core.Selectors.ISelectorTelemetry? _telemetry;
    private readonly IHistogram? _hLocate;
    private readonly ICounter? _cAttempts;
    private readonly ICounter? _cFail;

    public LocatorPolicyStack(IDomElementManager dom, IElementFinder finder, IMetrics? metrics = null, HushOps.Core.Selectors.ISelectorTelemetry? telemetry = null)
    {
        _dom = dom;
        _finder = finder;
        _metrics = metrics;
        _telemetry = telemetry;
        _hLocate = metrics?.CreateHistogram("locate_stage_duration_ms", "定位阶段耗时（毫秒）");
        _cAttempts = metrics?.CreateCounter("locate_attempts_total", "定位尝试计数");
        _cFail = metrics?.CreateCounter("locate_failures_total", "定位失败计数");
    }

    public async Task<LocatorAcquireResult> AcquireAsync(IAutoPage page, LocatorHint hint, CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var labels = LabelSet.From(("stage", "locate"), ("role", hint.Role ?? ""), ("name", hint.NameOrText ?? ""));

        try
        {
            // 1) 组合：容器范围（若有）
            var containers = new List<string>();
            foreach (var ca in hint.ContainerAliases)
            {
                var sels = _dom.GetSelectors(ca);
                if (sels is { Count: > 0 }) containers.AddRange(sels);
            }
            var containerPrefix = containers.Count > 0 ? string.Join(",", containers.Select(s => s + " ")) : string.Empty;

            // 2) A11y/语义（角色 + 文本/名称）
            if (!string.IsNullOrWhiteSpace(hint.Role) && !string.IsNullOrWhiteSpace(hint.NameOrText))
            {
                var name = Escape(hint.NameOrText!);
                var roleKey = $"role:{hint.Role!.Trim().ToLowerInvariant()}";
                var roleSels = BuildRoleSelectors(hint.Role!, name, containerPrefix);
                var orderedRoleSels = _telemetry?.OrderByTelemetry(roleKey, roleSels) ?? roleSels;
                var attempt = 0;
                foreach (var sel in orderedRoleSels)
                {
                    _cAttempts?.Add(1, LabelSet.From(("role", hint.Role!), ("name", hint.NameOrText!), ("strategy", "a11y-role")));
                    var swTry = System.Diagnostics.Stopwatch.StartNew();
                    var el = await TryQueryAsync(page, sel, hint, ct);
                    swTry.Stop();
                    _telemetry?.RecordAttempt(roleKey, sel, el != null, (long)swTry.Elapsed.TotalMilliseconds, ++attempt);
                    if (el != null) return Ok(el, "a11y-role");
                }
            }

            // 3) 别名候选（DomElementManager 提供稳定 CSS）
            foreach (var alias in hint.Aliases)
            {
                var el = await _finder.FindElementAsync(page, alias, retries: 1, timeout: hint.StepTimeoutMs);
                if (el != null)
                {
                    if (!string.IsNullOrWhiteSpace(hint.NameOrText))
                    {
                        // 追加一次 alias+has-text 重试以增强匹配度
                        var name = Escape(hint.NameOrText!);
                        var baseSels = _dom.GetSelectors(alias);
                        var ordered = _telemetry?.OrderByTelemetry(alias, baseSels) ?? baseSels;
                        var attempt = 0;
                        foreach (var sel in ordered)
                        {
                            var combo = sel + $":has-text(\"{name}\")";
                            _cAttempts?.Add(1, LabelSet.From(("name", hint.NameOrText!), ("strategy", "alias-has-text")));
                            var swTry = System.Diagnostics.Stopwatch.StartNew();
                            var el2 = await TryQueryAsync(page, combo, hint, ct);
                            swTry.Stop();
                            _telemetry?.RecordAttempt(alias, combo, el2 != null, (long)swTry.Elapsed.TotalMilliseconds, ++attempt);
                            if (el2 != null) return Ok(el2, "alias-has-text");
                        }
                    }
                    _cAttempts?.Add(1, LabelSet.From(("strategy", "alias")));
                    return Ok(el, "alias");
                }
            }

            // 4) 容器+别名组合（更强约束）
            if (hint.Aliases.Count > 0 && containerPrefix.Length > 0)
            {
                foreach (var alias in hint.Aliases)
                {
                    var baseSels = _dom.GetSelectors(alias);
                    var ordered = _telemetry?.OrderByTelemetry(alias, baseSels) ?? baseSels;
                    var attempt = 0;
                    foreach (var sel in ordered)
                    {
                        var combo = containerPrefix + sel;
                        if (!string.IsNullOrWhiteSpace(hint.NameOrText)) combo += $":has-text(\"{Escape(hint.NameOrText!)}\")";
                        _cAttempts?.Add(1, LabelSet.From(("strategy", "container-alias")));
                        var swTry = System.Diagnostics.Stopwatch.StartNew();
                        var el = await TryQueryAsync(page, combo, hint, ct);
                        swTry.Stop();
                        _telemetry?.RecordAttempt(alias, combo, el != null, (long)swTry.Elapsed.TotalMilliseconds, ++attempt);
                        if (el != null) return Ok(el, string.IsNullOrWhiteSpace(hint.NameOrText) ? "container-alias" : "container-alias-has-text");
                    }
                }
            }

            // 5) 文本引擎（粗粒度）：text=Name
            if (!string.IsNullOrWhiteSpace(hint.NameOrText))
            {
                _cAttempts?.Add(1, LabelSet.From(("name", hint.NameOrText!), ("strategy", "text-engine")));
                var el = await TryQueryAsync(page, containerPrefix + $"text={hint.NameOrText}", hint, ct);
                if (el != null) return Ok(el, "text-engine");
            }

            // 6) 失败
            _cFail?.Add(1, in labels);
            return new LocatorAcquireResult { Element = null, Strategy = string.Empty };
        }
        finally
        {
            sw.Stop();
            _hLocate?.Record(sw.Elapsed.TotalMilliseconds, in labels);
        }
    }

    private static IEnumerable<string> BuildRoleSelectors(string role, string name, string containerPrefix)
    {
        role = role.Trim().ToLowerInvariant();
        var list = new List<string>();
        switch (role)
        {
            case "button":
                list.Add(containerPrefix + $"button:has-text(\"{name}\")");
                list.Add(containerPrefix + $"[role=button]:has-text(\"{name}\")");
                list.Add(containerPrefix + $"a:has-text(\"{name}\")");
                break;
            case "link":
                list.Add(containerPrefix + $"a:has-text(\"{name}\")");
                list.Add(containerPrefix + $"[role=link]:has-text(\"{name}\")");
                break;
            case "textbox":
                list.Add(containerPrefix + $"input[placeholder*=\"{name}\"]");
                list.Add(containerPrefix + $"textarea[placeholder*=\"{name}\"]");
                list.Add(containerPrefix + $"input[type=search]:not([disabled])");
                break;
            default:
                // 通用回退：任意带文本的可交互元素
                list.Add(containerPrefix + $"*:has-text(\"{name}\")");
                break;
        }
        return list;
    }

    private static string Escape(string s) => s.Replace("\"", "\\\"");

    private static async Task<IAutoElement?> TryQueryAsync(IAutoPage page, string selector, LocatorHint hint, CancellationToken ct)
    {
        try
        {
            var el = await page.QueryAsync(selector, hint.StepTimeoutMs, ct);
            if (el == null) return null;
            if (hint.VisibleOnly)
            {
                try { if (!await el.IsVisibleAsync(ct)) return null; } catch { }
            }
            return el;
        }
        catch
        {
            return null;
        }
    }

    private static LocatorAcquireResult Ok(IAutoElement el, string strategy)
        => new LocatorAcquireResult { Element = el, Strategy = strategy };
}
