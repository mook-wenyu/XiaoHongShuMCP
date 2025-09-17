using Microsoft.Extensions.Logging;
using HushOps.Core.Automation.Abstractions;

namespace XiaoHongShuMCP.Services;

/// <summary>
/// 页面状态守护实现（确保不在笔记详情页）。
/// 破坏性变更：仅保留“点击详情页关闭按钮”作为唯一退出方式；不再点击遮罩，不再发送 ESC。
/// 在每次动作后等待页面稳定并复检页面类型；只要页面不再是 NoteDetail 即判定成功。
/// </summary>
public class PageStateGuard : IPageStateGuard
{
    private readonly ILogger<PageStateGuard> _logger;
    private readonly IBrowserManager _browserManager;
    private readonly IDomElementManager _domElementManager;
    private readonly IHumanizedInteractionService _humanizedInteraction;
    private readonly IPageLoadWaitService _pageLoadWaitService;

    public PageStateGuard(
        ILogger<PageStateGuard> logger,
        IBrowserManager browserManager,
        IDomElementManager domElementManager,
        IHumanizedInteractionService humanizedInteraction,
        IPageLoadWaitService pageLoadWaitService)
    {
        _logger = logger;
        _browserManager = browserManager;
        _domElementManager = domElementManager;
        _humanizedInteraction = humanizedInteraction;
        _pageLoadWaitService = pageLoadWaitService;
    }

    /// <inheritdoc />
    public async Task<bool> EnsureExitNoteDetailIfPresentAsync(IAutoPage page)
    {
        try
        {
            var type = await DetectPageTypeAsync();
            if (type != PageType.NoteDetail) return true;

            _logger.LogInformation("检测到处于笔记详情页，开始执行退出流程...");

            // 1) 关闭按钮集合（详情页专用 + 通用关闭）
            var selectors = new List<string>();
            selectors.AddRange(_domElementManager.GetSelectors("NoteDetailCloseButton"));
            selectors.AddRange(_domElementManager.GetSelectors("CloseButton"));

            foreach (var sel in selectors.Distinct())
            {
                try
                {
                    var el = await page.QueryAsync(sel, 2000);
                    if (el == null) continue;
                    var visible = await el.IsVisibleAsync();
                    if (!visible) continue;

                    await _humanizedInteraction.HumanClickAsync(el);
                    await _pageLoadWaitService.WaitForPageLoadAsync(page);
                    var st = await DetectPageTypeAsync();
                    if (st != PageType.NoteDetail) return true;
                }
                catch
                {
                    // 尝试下一候选
                }
            }

            // 不再点击遮罩或发送 ESC（遵循“无遮罩处理/无 ESC”要求）
            return await DetectPageTypeAsync() != PageType.NoteDetail;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "退出详情页流程异常");
            // 保守返回 false，以便上层决策
            return false;
        }
    }

    /// <summary>
    /// 确保在发现/搜索入口页：若不满足则尝试点击侧边栏“发现”链接，失败再回退为直接URL导航。
    /// </summary>
    public async Task<bool> EnsureOnDiscoverOrSearchAsync(IAutoPage page)
    {
        try
        {
            // 1) 若在详情页，先退出
            var type = await DetectPageTypeAsync();
            if (type == PageType.NoteDetail)
            {
                var ok = await EnsureExitNoteDetailIfPresentAsync(page);
                if (!ok) return false;
                type = await DetectPageTypeAsync();
            }

            _logger.LogInformation("当前页面 类型：{Type}", type);
            // 2) 已在发现或搜索，直接通过
            if (type is PageType.Home or PageType.Recommend or PageType.Search)
                return true;

            // 3) 尝试点击侧边栏“发现”链接
            var discoverSel = _domElementManager.GetSelectors("SidebarDiscoverLink");
            foreach (var sel in discoverSel)
            {
                try
                {
                    var link = await page.QueryAsync(sel, 2000);
                    if (link == null) continue;
                    var visible = await link.IsVisibleAsync();
                    if (!visible) continue;
                    await _humanizedInteraction.HumanClickAsync(link);
                    var wait = await _pageLoadWaitService.WaitForPageLoadAsync(page);
                    _logger.LogDebug("点击发现链接后页面加载：{Ok}", wait.Success);
                    type = await DetectPageTypeAsync();
                    if (type is PageType.Recommend or PageType.Search)
                        return true;
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "点击发现链接失败，尝试下一候选");
                }
            }

            // 4) 回退为直接URL导航
            try
            {
                var url = "https://www.xiaohongshu.com/explore?channel_id=homefeed_recommend";
                await page.Navigator.GoToAsync(url, new HushOps.Core.Automation.Abstractions.PageGotoOptions { WaitUntil = HushOps.Core.Automation.Abstractions.WaitUntilState.DomContentLoaded, TimeoutMs = 120_000 });
                var wait = await _pageLoadWaitService.WaitForPageLoadAsync(page);
                _logger.LogDebug("直接URL导航发现页：{Ok}", wait.Success);
                type = await DetectPageTypeAsync();
                return type is PageType.Recommend or PageType.Search;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "URL 导航发现页失败");
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "EnsureOnDiscoverOrSearchAsync 异常");
            return false;
        }
    }

    /// <summary>
    /// 轻量页面类型检测：复用 DomElementManager 的 URL/DOM 识别能力，返回通用 PageType。
    /// </summary>
    private async Task<PageType> DetectPageTypeAsync()
    {
        try
        {
            var page = await _browserManager.GetAutoPageAsync();
            // 优先使用驱动原生 URL（避免 Evaluate），若不可用再退回只读 Evaluate 并计量
            string path = await page.GetUrlAsync();

            if (path.Contains("/explore/", StringComparison.OrdinalIgnoreCase))
                return PageType.NoteDetail;
            if (path.Contains("/search_result?keyword=", StringComparison.OrdinalIgnoreCase))
                return PageType.Search;
            if (path.Contains("/explore?channel_id=homefeed_recommend", StringComparison.OrdinalIgnoreCase))
                return PageType.Recommend;
            if (path.Contains("/explore", StringComparison.OrdinalIgnoreCase))
                return PageType.Home;

            // DOM 回退：尝试查找详情容器
            var masks = _domElementManager.GetSelectors("NoteDetailModal");
            foreach (var maskSel in masks)
            {
                try
                {
                    var mask = await page.QueryAsync(maskSel, 1000);
                    if (mask != null && await mask.IsVisibleAsync()) return PageType.NoteDetail;
                }
                catch { }
            }

            return PageType.Unknown;
        }
        catch
        {
            return PageType.Unknown;
        }
    }
}


