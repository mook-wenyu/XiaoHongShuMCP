using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HushOps.Core.Automation.Abstractions;
using HushOps.Core.Humanization;
using HushOps.Core.Runtime.Playwright;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace XiaoHongShuMCP.Services.CommentFlow;

/// <summary>
/// 中文：页面守护者，实现页面状态检测与评论区域激活逻辑。
/// </summary>
internal sealed class PageGuardian : IPageGuardian
{
    private readonly IDomElementManager _domElementManager;
    private readonly IHumanizedInteractionService _humanizedInteraction;
    private readonly IPageLoadWaitService _pageLoadWaitService;
    private readonly ILogger<PageGuardian> _logger;

    public PageGuardian(
        IDomElementManager domElementManager,
        IHumanizedInteractionService humanizedInteraction,
        IPageLoadWaitService pageLoadWaitService,
        ILogger<PageGuardian> logger)
    {
        _domElementManager = domElementManager;
        _humanizedInteraction = humanizedInteraction;
        _pageLoadWaitService = pageLoadWaitService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<PageStatusInfo> InspectAsync(IPage page, PageType expectedType, CancellationToken ct)
    {
        var status = new PageStatusInfo();
        try
        {
            status.CurrentUrl = page.Url ?? string.Empty;
        }
        catch
        {
            status.CurrentUrl = string.Empty;
        }

        status.PageType = DeterminePageTypeFromUrl(status.CurrentUrl);
        if (status.PageType == PageType.Unknown && expectedType != PageType.Unknown)
        {
            status.PageType = expectedType;
        }

        var autoPage = PlaywrightAutoFactory.Wrap(page);
        try
        {
            await _pageLoadWaitService.WaitForPageLoadAsync(autoPage, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "页面加载等待失败，继续检测 DOM 特征");
        }

        await DetectPageSpecificElementsAsync(page, status, ct).ConfigureAwait(false);
        status.IsPageReady = DeterminePageReadiness(status);
        return status;
    }

    /// <inheritdoc />
    public async Task<bool> EnsureCommentAreaReadyAsync(IPage page, CancellationToken ct)
    {
        var activeSelectors = _domElementManager.GetSelectors("EngageBarActive");
        foreach (var selector in activeSelectors)
        {
            if (await page.QuerySelectorAsync(selector) is not null)
            {
                return true;
            }
        }

        var triggerSelectors = _domElementManager.GetSelectors("DetailPageCommentButton");
        foreach (var selector in triggerSelectors)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var trigger = await page.QuerySelectorAsync(selector).ConfigureAwait(false);
                if (trigger is null) continue;
                await _humanizedInteraction.HumanClickAsync(trigger).ConfigureAwait(false);
                await Task.Delay(1000, ct).ConfigureAwait(false);

                foreach (var active in activeSelectors)
                {
                    if (await page.QuerySelectorAsync(active).ConfigureAwait(false) is not null)
                    {
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug("使用选择器 {Selector} 激活评论区域失败：{Error}", selector, ex.Message);
            }
        }

        return false;
    }

    /// <inheritdoc />
    public async Task<bool> WaitForLocatorAsync(IAutoPage autoPage, string alias, TimeSpan timeout, CancellationToken ct)
    {
        var selectors = _domElementManager.GetSelectors(alias);
        var start = DateTime.UtcNow;

        while (DateTime.UtcNow - start < timeout)
        {
            ct.ThrowIfCancellationRequested();

            foreach (var selector in selectors)
            {
                try
                {
                    var element = await autoPage.QueryAsync(selector, 1000, ct).ConfigureAwait(false);
                    if (element != null) return true;
                }
                catch
                {
                    // 忽略瞬时查询失败
                }
            }

            await _humanizedInteraction.HumanWaitAsync(HumanWaitType.RetryBackoff, ct).ConfigureAwait(false);
        }

        return false;
    }

    private async Task DetectPageSpecificElementsAsync(IPage page, PageStatusInfo status, CancellationToken ct)
    {
        async Task<int> CountAsync(IEnumerable<string> selectors)
        {
            foreach (var selector in selectors)
            {
                try
                {
                    var elements = await page.QuerySelectorAllAsync(selector).ConfigureAwait(false);
                    if (elements.Count > 0) return elements.Count;
                }
                catch (Exception ex)
                {
                    _logger.LogDebug("统计选择器 {Selector} 时失败: {Error}", selector, ex.Message);
                }
            }
            return 0;
        }

        switch (status.PageType)
        {
            case PageType.Recommend:
                status.ElementsDetected["note_items"] = await CountAsync(_domElementManager.GetSelectors("NoteItem")).ConfigureAwait(false);
                break;
            case PageType.Search:
                status.ElementsDetected["search_results"] = await CountAsync(_domElementManager.GetSelectors("SearchResultItem")).ConfigureAwait(false);
                break;
            case PageType.NoteDetail:
                status.ElementsDetected["note_content"] = await CountAsync(_domElementManager.GetSelectors("NoteContent")).ConfigureAwait(false);
                break;
        }
    }

    private static PageType DeterminePageTypeFromUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return PageType.Unknown;
        if (url.Contains("/explore", StringComparison.OrdinalIgnoreCase)) return PageType.Recommend;
        if (url.Contains("/search_result", StringComparison.OrdinalIgnoreCase)) return PageType.Search;
        if (url.Contains("/notes/", StringComparison.OrdinalIgnoreCase) || url.Contains("/explore/item", StringComparison.OrdinalIgnoreCase)) return PageType.NoteDetail;
        if (url.Contains("/profile", StringComparison.OrdinalIgnoreCase)) return PageType.Profile;
        return PageType.Unknown;
    }

    private static bool DeterminePageReadiness(PageStatusInfo status)
    {
        return status.PageType switch
        {
            PageType.NoteDetail => status.ElementsDetected.TryGetValue("note_content", out var c) && c > 0,
            PageType.Search => status.ElementsDetected.TryGetValue("search_results", out var r) && r > 0,
            PageType.Recommend => status.ElementsDetected.TryGetValue("note_items", out var n) && n > 0,
            _ => true
        };
    }
}
