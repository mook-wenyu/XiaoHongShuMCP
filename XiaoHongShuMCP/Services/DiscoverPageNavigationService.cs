using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace XiaoHongShuMCP.Services;

/// <summary>
/// 发现页面导航服务实现
/// </summary>
public class DiscoverPageNavigationService : IDiscoverPageNavigationService
{
    private readonly ILogger<DiscoverPageNavigationService> _logger;
    private readonly IDomElementManager _domElementManager;
    private readonly IHumanizedInteractionService _humanizedInteraction;
    private readonly IPageLoadWaitService _pageLoadWaitService;

    /// <summary>
    /// 构造函数
    /// </summary>
    public DiscoverPageNavigationService(
        ILogger<DiscoverPageNavigationService> logger,
        IDomElementManager domElementManager,
        IHumanizedInteractionService humanizedInteraction,
        IPageLoadWaitService pageLoadWaitService)
    {
        _logger = logger;
        _domElementManager = domElementManager;
        _humanizedInteraction = humanizedInteraction;
        _pageLoadWaitService = pageLoadWaitService;
    }

    /// <inheritdoc />
    public async Task<DiscoverNavigationResult> NavigateToDiscoverPageAsync(IPage page, TimeSpan? timeout = null)
    {
        var startTime = DateTime.UtcNow;
        timeout ??= TimeSpan.FromSeconds(30);
        
        var result = new DiscoverNavigationResult
        {
            NavigationLog = []
        };

        try
        {
            result.NavigationLog.Add($"开始导航到发现页面，超时时间：{timeout.Value.TotalSeconds}秒");

            // 方法1：尝试点击发现按钮
            var buttonClickResult = await TryClickDiscoverButtonAsync(page);
            if (buttonClickResult.Success)
            {
                result.Success = true;
                result.Method = DiscoverNavigationMethod.ClickButton;
                result.FinalUrl = page.Url;
                result.NavigationLog.AddRange(buttonClickResult.Log);
                
                // 验证API是否被触发
                result.ApiTriggered = await ValidateHomefeedApiTriggeredAsync(page, TimeSpan.FromSeconds(5));
                
                _logger.LogInformation("通过点击发现按钮成功导航，API触发状态：{ApiTriggered}", result.ApiTriggered);
                return result;
            }

            result.NavigationLog.AddRange(buttonClickResult.Log);

            // 方法2：直接URL导航
            var urlNavigationResult = await TryDirectUrlNavigationAsync(page);
            if (urlNavigationResult.Success)
            {
                result.Success = true;
                result.Method = DiscoverNavigationMethod.DirectUrl;
                result.FinalUrl = page.Url;
                result.NavigationLog.AddRange(urlNavigationResult.Log);
                
                // 验证API是否被触发
                result.ApiTriggered = await ValidateHomefeedApiTriggeredAsync(page, TimeSpan.FromSeconds(5));
                
                _logger.LogInformation("通过直接URL导航成功，API触发状态：{ApiTriggered}", result.ApiTriggered);
                return result;
            }

            result.NavigationLog.AddRange(urlNavigationResult.Log);

            // 方法3：JavaScript强制导航
            var jsNavigationResult = await TryJavaScriptNavigationAsync(page);
            if (jsNavigationResult.Success)
            {
                result.Success = true;
                result.Method = DiscoverNavigationMethod.JavaScript;
                result.FinalUrl = page.Url;
                result.NavigationLog.AddRange(jsNavigationResult.Log);
                
                // 验证API是否被触发
                result.ApiTriggered = await ValidateHomefeedApiTriggeredAsync(page, TimeSpan.FromSeconds(5));
                
                _logger.LogInformation("通过JavaScript导航成功，API触发状态：{ApiTriggered}", result.ApiTriggered);
                return result;
            }

            result.NavigationLog.AddRange(jsNavigationResult.Log);

            // 所有方法都失败
            result.Success = false;
            result.Method = DiscoverNavigationMethod.Failed;
            result.ErrorMessage = "所有导航方法都失败";
            result.NavigationLog.Add("所有导航方法都失败");

            _logger.LogError("导航到发现页面失败，已尝试所有方法");
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Method = DiscoverNavigationMethod.Failed;
            result.ErrorMessage = ex.Message;
            result.NavigationLog.Add($"导航过程中发生异常：{ex.Message}");
            
            _logger.LogError(ex, "导航到发现页面时发生异常");
        }
        finally
        {
            result.DurationMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;
        }

        return result;
    }

    /// <summary>
    /// 尝试通过点击发现按钮导航
    /// </summary>
    private async Task<(bool Success, List<string> Log)> TryClickDiscoverButtonAsync(IPage page)
    {
        var log = new List<string>();
        
        try
        {
            log.Add("尝试方法1：点击发现按钮");
            
            var discoverSelectors = _domElementManager.GetSelectors("SidebarDiscoverLink");
            log.Add($"获取到{discoverSelectors.Count}个发现按钮选择器");

            foreach (var selector in discoverSelectors)
            {
                try
                {
                    log.Add($"尝试选择器：{selector}");
                    
                    var button = await page.QuerySelectorAsync(selector);
                    if (button != null)
                    {
                        log.Add($"找到发现按钮：{selector}");
                        
                        // 检查按钮是否可见和可点击
                        var isVisible = await button.IsVisibleAsync();
                        var isEnabled = await button.IsEnabledAsync();
                        
                        log.Add($"按钮状态 - 可见：{isVisible}，可点击：{isEnabled}");
                        
                        if (isVisible && isEnabled)
                        {
                            // 执行拟人化点击
                            await _humanizedInteraction.HumanClickAsync(page, button);
                            log.Add("已执行拟人化点击");
                            
                            // 等待页面响应 - 使用新的等待策略
                            var waitResult = await _pageLoadWaitService.WaitForPageLoadAsync(page);
                            if (!waitResult.Success)
                            {
                                log.Add($"页面加载等待失败：{waitResult.ErrorMessage}");
                                log.AddRange(waitResult.ExecutionLog);
                                continue;
                            }
                            log.AddRange(waitResult.ExecutionLog);
                            
                            // 验证导航结果
                            var currentUrl = page.Url;
                            log.Add($"点击后页面URL：{currentUrl}");
                            
                            if (currentUrl.Contains("/explore") || currentUrl.Contains("channel_id=homefeed_recommend"))
                            {
                                log.Add("通过URL验证导航成功");
                                return (true, log);
                            }
                        }
                        else
                        {
                            log.Add("按钮不可点击，跳过");
                        }
                    }
                    else
                    {
                        log.Add($"选择器未找到元素：{selector}");
                    }
                }
                catch (Exception ex)
                {
                    log.Add($"选择器{selector}处理失败：{ex.Message}");
                }
            }
            
            log.Add("所有发现按钮选择器都失败");
            return (false, log);
        }
        catch (Exception ex)
        {
            log.Add($"点击发现按钮方法异常：{ex.Message}");
            return (false, log);
        }
    }

    /// <summary>
    /// 尝试直接URL导航
    /// </summary>
    private async Task<(bool Success, List<string> Log)> TryDirectUrlNavigationAsync(IPage page)
    {
        var log = new List<string>();
        
        try
        {
            log.Add("尝试方法2：直接URL导航");
            
            var discoverUrls = new[]
            {
                "https://www.xiaohongshu.com/explore?channel_id=homefeed_recommend",
                "https://www.xiaohongshu.com/explore",
                "https://www.xiaohongshu.com/discovery"
            };

            foreach (var url in discoverUrls)
            {
                try
                {
                    log.Add($"尝试导航到：{url}");
                    
                    await page.GotoAsync(url);
                    
                    // 使用新的等待策略
                    var waitResult = await _pageLoadWaitService.WaitForPageLoadAsync(page);
                    if (!waitResult.Success)
                    {
                        log.Add($"页面加载等待失败：{waitResult.ErrorMessage}");
                        if (waitResult.WasDegraded)
                        {
                            log.Add("已使用降级策略");
                        }
                        log.AddRange(waitResult.ExecutionLog);
                        continue;
                    }
                    log.AddRange(waitResult.ExecutionLog);
                    
                    var currentUrl = page.Url;
                    log.Add($"导航后页面URL：{currentUrl}");
                    
                    // 验证页面状态
                    var pageStatus = await GetCurrentPageStatusAsync(page);
                    if (pageStatus.IsOnDiscoverPage)
                    {
                        log.Add("通过页面状态验证导航成功");
                        return (true, log);
                    }
                }
                catch (Exception ex)
                {
                    log.Add($"URL导航{url}失败：{ex.Message}");
                }
            }
            
            log.Add("所有URL导航尝试都失败");
            return (false, log);
        }
        catch (Exception ex)
        {
            log.Add($"URL导航方法异常：{ex.Message}");
            return (false, log);
        }
    }

    /// <summary>
    /// 尝试JavaScript强制导航
    /// </summary>
    private async Task<(bool Success, List<string> Log)> TryJavaScriptNavigationAsync(IPage page)
    {
        var log = new List<string>();
        
        try
        {
            log.Add("尝试方法3：JavaScript强制导航");
            
            var jsScripts = new[]
            {
                "window.location.href = 'https://www.xiaohongshu.com/explore?channel_id=homefeed_recommend';",
                "window.location.replace('https://www.xiaohongshu.com/explore');",
                "document.querySelector(\"a[href*='/explore']\")?.click();"
            };

            foreach (var script in jsScripts)
            {
                try
                {
                    log.Add($"执行JavaScript：{script}");
                    
                    await page.EvaluateAsync(script);
                    
                    // 使用新的等待策略
                    var waitResult = await _pageLoadWaitService.WaitForPageLoadAsync(page);
                    if (!waitResult.Success)
                    {
                        log.Add($"页面加载等待失败：{waitResult.ErrorMessage}");
                        log.AddRange(waitResult.ExecutionLog);
                        continue;
                    }
                    log.AddRange(waitResult.ExecutionLog);
                    
                    var currentUrl = page.Url;
                    log.Add($"JavaScript执行后页面URL：{currentUrl}");
                    
                    var pageStatus = await GetCurrentPageStatusAsync(page);
                    if (pageStatus.IsOnDiscoverPage)
                    {
                        log.Add("通过JavaScript导航成功");
                        return (true, log);
                    }
                }
                catch (Exception ex)
                {
                    log.Add($"JavaScript执行失败：{ex.Message}");
                }
            }
            
            log.Add("所有JavaScript导航尝试都失败");
            return (false, log);
        }
        catch (Exception ex)
        {
            log.Add($"JavaScript导航方法异常：{ex.Message}");
            return (false, log);
        }
    }

    /// <inheritdoc />
    public async Task<bool> ValidateHomefeedApiTriggeredAsync(IPage page, TimeSpan? waitTime = null)
    {
        waitTime ??= TimeSpan.FromSeconds(10);
        
        try
        {
            _logger.LogDebug("验证homefeed API是否被触发，等待时间：{WaitTime}秒", waitTime.Value.TotalSeconds);
            
            var apiTriggered = false;
            var endTime = DateTime.UtcNow.Add(waitTime.Value);
            
            // 设置网络监听来检测API请求
            page.Request += (sender, e) =>
            {
                var url = e.Url;
                if (url.Contains("/api/sns/web/v1/homefeed"))
                {
                    apiTriggered = true;
                    _logger.LogDebug("检测到homefeed API请求：{Url}", url);
                }
            };
            
            // 等待API请求或超时
            while (DateTime.UtcNow < endTime && !apiTriggered)
            {
                await Task.Delay(500);
            }
            
            _logger.LogDebug("API触发验证结果：{ApiTriggered}", apiTriggered);
            return apiTriggered;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "验证homefeed API触发状态时发生异常");
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<DiscoverPageStatus> GetCurrentPageStatusAsync(IPage page)
    {
        var status = new DiscoverPageStatus
        {
            CurrentUrl = page.Url
        };

        try
        {
            // URL检测
            if (status.CurrentUrl.Contains("/explore") || 
                status.CurrentUrl.Contains("channel_id=homefeed_recommend"))
            {
                status.IsOnDiscoverPage = true;
            }

            // 页面状态检测
            status.PageState = await _domElementManager.DetectPageStateAsync(page);
            if (status.PageState == PageState.Explore)
            {
                status.IsOnDiscoverPage = true;
            }

            // DOM元素检测
            var discoverElements = new[]
            {
                "#exploreFeeds",
                "[data-testid='explore-page']",
                ".channel-container",
                ".note-item"
            };

            var elementCount = 0;
            foreach (var selector in discoverElements)
            {
                try
                {
                    var elements = await page.QuerySelectorAllAsync(selector);
                    elementCount += elements.Count;
                }
                catch
                {
                    // 忽略选择器错误
                }
            }

            status.DiscoverElementsCount = elementCount;
            if (elementCount > 0)
            {
                status.IsOnDiscoverPage = true;
            }

            // API特征检测
            var apiFeatures = new List<string>();
            
            // 检测网络请求特征
            var networkRequests = await page.EvaluateAsync<string[]>(@"
                Array.from(performance.getEntriesByType('resource'))
                    .map(entry => entry.name)
                    .filter(name => name.includes('homefeed') || name.includes('explore'))
            ");
            
            apiFeatures.AddRange(networkRequests);
            status.ApiFeatures = apiFeatures;

            _logger.LogDebug("页面状态检测完成 - URL检测：{UrlMatch}，DOM元素：{ElementCount}，页面状态：{PageState}", 
                status.CurrentUrl.Contains("/explore"), status.DiscoverElementsCount, status.PageState);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取页面状态时发生异常");
        }

        return status;
    }
}