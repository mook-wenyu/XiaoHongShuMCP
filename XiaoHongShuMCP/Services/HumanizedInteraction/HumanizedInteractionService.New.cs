using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HushOps.Core.Automation.Abstractions;
using HushOps.Core.Humanization;
using HushOps.Core.Runtime.Playwright;
using Microsoft.Playwright;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace XiaoHongShuMCP.Services;

/// <summary>
/// 拟人化交互服务（重构版，强类型 IAuto*）
/// - 设计目标：
///   1) 统一强类型通路（IAutoPage/IAutoElement），不暴露 IPage/IElementHandle；
///   2) 默认禁注入；点击走 <see cref="IHumanizedClickPolicy"/> 分层降级，拟人化 Hover/停顿/轨迹；
///   3) 与既有调用兼容通过扩展方法实现（见 HumanizedInteractionService.Extensions）。
/// - 注意：仅使用人因延时与浏览器原生输入，不做 JS 注入；默认零脚本。
/// </summary>
public class HumanizedInteractionService : IHumanizedInteractionService
{
    private readonly IBrowserManager _browserManager;
    private readonly IDelayManager _delay;
    private readonly IElementFinder _finder;
    private readonly List<ITextInputStrategy> _inputStrategies;
    private readonly IDomElementManager _dom;
    private readonly IHumanizedClickPolicy _clickPolicy;
    private readonly ILogger<HumanizedInteractionService>? _logger;
    private readonly TimeSpan _cacheTtl;

    public HumanizedInteractionService(
        IBrowserManager browserManager,
        IDelayManager delayManager,
        IElementFinder elementFinder,
        IEnumerable<ITextInputStrategy> inputStrategies,
        IDomElementManager domElementManager,
        IOptions<XhsSettings> xhsOptions,
        IHumanizedClickPolicy clickPolicy,
        ILogger<HumanizedInteractionService>? logger = null)
    {
        _browserManager = browserManager;
        _delay = delayManager;
        _finder = elementFinder;
        _inputStrategies = inputStrategies.ToList();
        _dom = domElementManager;
        _clickPolicy = clickPolicy;
        _logger = logger;
        var ttlMin = xhsOptions?.Value?.InteractionCache?.TtlMinutes;
        ttlMin = (ttlMin is null or <= 0) ? 3 : Math.Min(ttlMin.Value, 1440);
        _cacheTtl = TimeSpan.FromMinutes(ttlMin.Value);
    }

    // =============== 基础拟人化动作 ===============

    public async Task HumanClickAsync(string selectorAlias)
    {
        var page = await _browserManager.GetAutoPageAsync();
        var el = await FindElementAsync(page, selectorAlias, retries: 3, timeout: 3000)
                 ?? throw new Exception($"无法找到元素: {selectorAlias}");
        await HumanClickAsync(el);
    }

    public async Task HumanClickAsync(IAutoElement element)
    {
        var page = await _browserManager.GetAutoPageAsync();
        // 统一通过点击策略执行（含预检/轨迹/兜底）；默认禁注入
        var decision = await _clickPolicy.ClickAsync(page, element);
        if (!decision.Success)
        {
            throw new Exception($"点击失败，路径={decision.Path}, 尝试={decision.Attempts}");
        }
    }

    public async Task HumanHoverAsync(string selectorAlias)
    {
        var page = await _browserManager.GetAutoPageAsync();
        var el = await FindElementAsync(page, selectorAlias, retries: 2, timeout: 2000);
        if (el != null) await HumanHoverAsync(el);
    }

    public async Task HumanHoverAsync(IAutoElement element)
    {
        await _delay.WaitAsync(HumanWaitType.HoverPause);
        try { await element.HoverAsync(); } catch { }
        await _delay.WaitAsync(HumanWaitType.ReviewPause);
    }

    public async Task HumanTypeAsync(IAutoPage page, string selectorAlias, string text)
    {
        var el = await FindElementAsync(page, selectorAlias, retries: 3, timeout: 3000)
                 ?? throw new Exception($"无法找到元素: {selectorAlias}");
        await InputTextAsync(page, el, text);
    }

    public async Task HumanScrollAsync(IAutoPage page, CancellationToken cancellationToken = default)
        => await HumanScrollAsync(page, targetDistance: 0, waitForLoad: false, cancellationToken);

    public async Task HumanScrollAsync(IAutoPage page, int targetDistance, bool waitForLoad = true, CancellationToken cancellationToken = default)
    {
        if (targetDistance <= 0) targetDistance = Random.Shared.Next(300, 800);
        await page.MouseWheelAsync(0, targetDistance, cancellationToken);
        await _delay.WaitAsync(HumanWaitType.ScrollCompletion, cancellationToken: cancellationToken);
        if (waitForLoad) await _delay.WaitAsync(HumanWaitType.VirtualListUpdate, cancellationToken: cancellationToken);
    }

    // =============== 查找与输入 ===============

    public async Task<IAutoElement?> FindElementAsync(IAutoPage page, string selectorAlias, int retries = 3, int timeout = 3000)
    {
        var playwrightPage = PlaywrightAutoFactory.TryUnwrap(page);
        var selectors = _dom.GetSelectors(selectorAlias) ?? new List<string>();
        var attempts = Math.Max(1, retries);
        var perAttempt = Math.Max(100, timeout / attempts);

        for (int attempt = 0; attempt < attempts; attempt++)
        {
            if (playwrightPage != null)
            {
                var handle = await TryLocatorAsync(playwrightPage, selectorAlias, PageState.Auto, perAttempt, CancellationToken.None);
                if (handle != null) return PlaywrightAutoFactory.Wrap(handle);
            }

            foreach (var sel in selectors)
            {
                var el = await page.QueryAsync(sel, perAttempt);
                if (el != null) return el;
            }

            if (attempt < attempts - 1)
            {
                await Task.Delay(perAttempt);
            }
        }

        // 中文：已移除视觉兜底流程，仅依赖 Locator 与 DOM 选择器。
        return null;
    }

    public async Task<IAutoElement?> FindElementAsync(IAutoPage page, string selectorAlias, PageState pageState, int retries = 3, int timeout = 3000, CancellationToken cancellationToken = default)
    {
        var playwrightPage = PlaywrightAutoFactory.TryUnwrap(page);
        var selectors = _dom.GetSelectors(selectorAlias, pageState) ?? new List<string>();
        var attempts = Math.Max(1, retries);
        var perAttempt = Math.Max(100, timeout / attempts);

        for (int attempt = 0; attempt < attempts; attempt++)
        {
            if (playwrightPage != null)
            {
                var handle = await TryLocatorAsync(playwrightPage, selectorAlias, pageState, perAttempt, cancellationToken);
                if (handle != null) return PlaywrightAutoFactory.Wrap(handle);
            }

            foreach (var sel in selectors)
            {
                var el = await page.QueryAsync(sel, perAttempt, cancellationToken);
                if (el != null) return el;
            }

            if (attempt < attempts - 1)
            {
                await Task.Delay(perAttempt, cancellationToken);
            }
        }

        return null;
    }

    // =============== 兼容 IPage 版本（用于旧代码路径的最小改动保留） ===============
    /// <summary>
    /// IPage 版本：根据别名在页面上查找元素句柄。
    /// 仅用于兼容遗留调用，新的调用请使用 IAutoPage 版本。
    /// </summary>
    public async Task<IElementHandle?> FindElementAsync(IPage page, string selectorAlias, int retries = 3, int timeout = 3000)
    {
        var selectors = _dom.GetSelectors(selectorAlias) ?? new List<string>();
        var attempts = Math.Max(1, retries);
        var perAttempt = Math.Max(100, timeout / attempts);

        for (int attempt = 0; attempt < attempts; attempt++)
        {
            var handle = await TryLocatorAsync(page, selectorAlias, PageState.Auto, perAttempt, CancellationToken.None);
            if (handle != null) return handle;

            foreach (var sel in selectors)
            {
                try
                {
                    var el = await page.QuerySelectorAsync(sel);
                    if (el != null) return el;
                }
                catch { }
            }

            if (attempt < attempts - 1)
            {
                await Task.Delay(perAttempt);
            }
        }

        // 中文：已取消视觉兜底，若未命中 Locator 或选择器则返回 null。
        return null;
    }

    private async Task<IElementHandle?> TryLocatorAsync(IPage page, string alias, PageState state, int timeoutMs, CancellationToken ct)
    {
        var locator = _dom.CreateLocator(page, alias, state);
        if (locator == null) return null;
        try
        {
            ct.ThrowIfCancellationRequested();
            return await locator.ElementHandleAsync(new LocatorElementHandleOptions { Timeout = timeoutMs });
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// IPage 版本（带页面状态）：根据别名和页面状态查找元素句柄。
    /// </summary>
    public async Task<IElementHandle?> FindElementAsync(IPage page, string selectorAlias, PageState pageState, int retries = 3, int timeout = 3000, CancellationToken cancellationToken = default)
    {
        var selectors = _dom.GetSelectors(selectorAlias, pageState) ?? new List<string>();
        var perAttempt = Math.Max(100, timeout / Math.Max(1, retries));
        for (int attempt = 0; attempt < Math.Max(1, retries); attempt++)
        {
            foreach (var sel in selectors)
            {
                try
                {
                    var el = await page.QuerySelectorAsync(sel);
                    if (el != null) return el;
                }
                catch { }
            }
            if (attempt < retries - 1) await Task.Delay(perAttempt, cancellationToken);
        }
        return null;
    }

    public async Task InputTextAsync(IAutoPage page, IAutoElement element, string text)
    {
        // 简化：不依赖底层句柄，统一用 IAutoElement.TypeAsync；
        // 仍保持“语义单元 + 人因停顿”以降低自动化特征。
        foreach (var unit in SplitToUnits(text))
        {
            await element.TypeAsync(unit);
            await _delay.WaitAsync(HumanWaitType.TypingSemanticUnit);
        }
    }

    private static IEnumerable<string> SplitToUnits(string text)
    {
        if (string.IsNullOrEmpty(text)) yield break;
        var buf = new List<char>(16);
        foreach (var ch in text)
        {
            buf.Add(ch);
            if (char.IsWhiteSpace(ch) || char.IsPunctuation(ch))
            {
                yield return new string(buf.ToArray());
                buf.Clear();
            }
        }
        if (buf.Count > 0) yield return new string(buf.ToArray());
    }

    // =============== 等待/延时 ===============

    public Task HumanWaitAsync(HumanWaitType waitType, CancellationToken cancellationToken = default)
        => _delay.WaitAsync(waitType, cancellationToken: cancellationToken);

    public Task HumanRetryDelayAsync(int attemptNumber, CancellationToken cancellationToken = default)
        => _delay.WaitAsync(HumanWaitType.RetryBackoff, attemptNumber, cancellationToken);

    public Task HumanBetweenActionsDelayAsync(CancellationToken cancellationToken = default)
        => _delay.WaitAsync(HumanWaitType.BetweenActions, cancellationToken: cancellationToken);

    // =============== 点赞/收藏（保守，无 DOM 依赖，交由 API 监听确认） ===============

    public async Task<InteractionResult> HumanLikeAsync()
    {
        await _delay.WaitAsync(HumanWaitType.ThinkingPause);
        return new InteractionResult(true, "点赞", "未知", "未知", "已发起点赞（等待API确认）");
    }

    public async Task<InteractionResult> HumanUnlikeAsync(IAutoPage page)
    {
        await _delay.WaitAsync(HumanWaitType.ThinkingPause);
        return new InteractionResult(true, "取消点赞", "未知", "未知", "已发起取消点赞（等待API确认）");
    }

    public async Task<InteractionResult> HumanFavoriteAsync(IAutoPage page)
    {
        await _delay.WaitAsync(HumanWaitType.ThinkingPause);
        return new InteractionResult(true, "收藏", "未知", "未知", "已发起收藏（等待API确认）");
    }

    public async Task<InteractionResult> HumanUnfavoriteAsync(IAutoPage page)
    {
        await _delay.WaitAsync(HumanWaitType.ThinkingPause);
        return new InteractionResult(true, "取消收藏", "未知", "未知", "已发起取消收藏（等待API确认）");
    }
}


