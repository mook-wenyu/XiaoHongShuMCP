using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using HushOps.Core.Runtime.Playwright;

namespace XiaoHongShuMCP.Services;

/// <summary>
/// XiaoHongShuService 页面导航与状态检测相关功能分离至局部类。
/// </summary>
public partial class XiaoHongShuService
{
    private const string DiscoverBaseUrl = "https://www.xiaohongshu.com/explore?channel_id=homefeed_recommend";

    /// <summary>
    /// 获取当前页面状态，结合 URL、DOM 与 API 监听信息给出统一判定。
    /// </summary>
    private async Task<PageStatusInfo> GetCurrentPageStatusAsync(IPage page, PageType expectedType, CancellationToken cancellationToken = default)
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
            await _pageLoadWaitService.WaitForPageLoadAsync(autoPage, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "页面加载等待失败，继续进行状态探测");
        }

        await DetectPageSpecificElements(page, status);
        await DetectApiFeatures(page, status);
        status.IsPageReady = DeterminePageReadiness(status);
        return status;
    }

    /// <summary>
    /// 导航至发现页（Explore），并验证页面是否就绪。
    /// </summary>
    private async Task<OperationResult<bool>> NavigateToDiscoverPageInternalAsync(IPage page, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("导航至发现页: {Url}", DiscoverBaseUrl);
            await page.GotoAsync(DiscoverBaseUrl, new PageGotoOptions
            {
                Timeout = (float)timeout.TotalMilliseconds,
                WaitUntil = WaitUntilState.DOMContentLoaded
            });

            var autoPage = PlaywrightAutoFactory.Wrap(page);
            await _pageLoadWaitService.WaitForPageLoadAsync(autoPage, cancellationToken);

            var ensured = await _pageStateGuard.EnsureOnDiscoverOrSearchAsync(autoPage);
            if (!ensured)
            {
                return OperationResult<bool>.Fail(
                    "无法进入发现页或搜索页环境",
                    ErrorType.NavigationError,
                    "DISCOVER_NAVIGATION_FAILED");
            }

            var status = await GetCurrentPageStatusAsync(page, PageType.Recommend, cancellationToken);
            if (!status.IsPageReady)
            {
                return OperationResult<bool>.Fail(
                    "发现页未完成加载",
                    ErrorType.NavigationError,
                    "DISCOVER_NOT_READY");
            }

            return OperationResult<bool>.Ok(true);
        }
        catch (TimeoutException tex)
        {
            _logger.LogWarning(tex, "导航发现页超时");
            return OperationResult<bool>.Fail(
                "导航发现页超时",
                ErrorType.NavigationError,
                "DISCOVER_TIMEOUT");
        }
        catch (PlaywrightException pex)
        {
            _logger.LogWarning(pex, "Playwright 导航异常");
            return OperationResult<bool>.Fail(
                $"Playwright 导航异常: {pex.Message}",
                ErrorType.NavigationError,
                "DISCOVER_PLAYWRIGHT_EXCEPTION");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导航发现页出现未预期异常");
            return OperationResult<bool>.Fail(
                $"导航发现页异常: {ex.Message}",
                ErrorType.Unknown,
                "DISCOVER_UNKNOWN_EXCEPTION");
        }
    }
}
