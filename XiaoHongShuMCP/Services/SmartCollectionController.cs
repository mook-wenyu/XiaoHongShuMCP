using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using System.Collections.Concurrent;
using System.Text.Json;

namespace XiaoHongShuMCP.Services;

/// <summary>
/// 智能收集控制器实现
/// 核心功能：进度跟踪、动态滚动策略、去重处理、性能监控
/// </summary>
public class SmartCollectionController : ISmartCollectionController
{
    private readonly ILogger<SmartCollectionController> _logger;
    private readonly IBrowserManager _browserManager;
    private readonly IHumanizedInteractionService _humanizedInteraction;
    private readonly ISelectorManager _selectorManager;
    private readonly IConfiguration _configuration;

    // 收集状态管理
    private readonly ConcurrentBag<NoteInfo> _collectedNotes = new();
    private readonly HashSet<string> _seenNoteIds = new();
    private readonly object _stateLock = new();
    
    // 性能监控
    private CollectionPerformanceMonitor _performanceMonitor = new();
    private CollectionStatus _currentStatus = new();

    public SmartCollectionController(
        ILogger<SmartCollectionController> logger,
        IBrowserManager browserManager,
        IHumanizedInteractionService humanizedInteraction,
        ISelectorManager selectorManager,
        IConfiguration configuration)
    {
        _logger = logger;
        _browserManager = browserManager;
        _humanizedInteraction = humanizedInteraction;
        _selectorManager = selectorManager;
        _configuration = configuration;
    }

    /// <summary>
    /// 执行智能分批收集的主入口方法
    /// </summary>
    public async Task<SmartCollectionResult> ExecuteSmartCollectionAsync(
        int targetCount,
        RecommendCollectionMode collectionMode = RecommendCollectionMode.Standard,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        timeout ??= TimeSpan.FromMinutes(5); // 默认5分钟超时
        
        _logger.LogInformation("开始智能分批收集：目标数量={TargetCount}, 模式={Mode}, 超时={Timeout}",
            targetCount, collectionMode, timeout);

        try
        {
            // 重置收集状态
            ResetCollectionState();
            _currentStatus = new CollectionStatus
            {
                TargetCount = targetCount,
                CollectionMode = collectionMode,
                StartTime = startTime,
                Phase = CollectionPhase.Initializing
            };

            // 获取浏览器页面
            var context = await _browserManager.GetBrowserContextAsync();
            var page = context.Pages.FirstOrDefault() ?? await context.NewPageAsync();

            // 设置网络拦截器
            var interceptorConfig = CreateInterceptorConfig(collectionMode);
            await SetupNetworkInterceptorAsync(page, interceptorConfig);

            // 导航到推荐页面
            await NavigateToHomefeedAsync(page);

            // 开始智能收集循环
            var collectionResult = await ExecuteCollectionLoopAsync(
                page, targetCount, collectionMode, timeout.Value, cancellationToken);

            // 处理收集结果
            var duration = DateTime.UtcNow - startTime;
            var result = await ProcessCollectionResultAsync(collectionResult, duration);

            _logger.LogInformation("智能分批收集完成：收集={Collected}/{Target}, 用时={Duration}ms, 请求数={Requests}",
                result.CollectedCount, targetCount, duration.TotalMilliseconds, result.RequestCount);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "智能分批收集失败：目标数量={TargetCount}", targetCount);
            var duration = DateTime.UtcNow - startTime;
            
            return new SmartCollectionResult
            {
                Success = false,
                CollectedNotes = _collectedNotes.ToList(),
                CollectedCount = _collectedNotes.Count,
                TargetCount = targetCount,
                RequestCount = _performanceMonitor.RequestCount,
                Duration = duration,
                ErrorMessage = $"收集失败: {ex.Message}",
                PerformanceMetrics = _performanceMonitor.GetMetrics(),
                CollectionDetails = _currentStatus
            };
        }
    }

    /// <summary>
    /// 执行收集循环的核心逻辑
    /// </summary>
    private async Task<CollectionLoopResult> ExecuteCollectionLoopAsync(
        IPage page, int targetCount, RecommendCollectionMode mode, TimeSpan timeout, CancellationToken cancellationToken)
    {
        _currentStatus.Phase = CollectionPhase.Collecting;
        var strategy = CreateCollectionStrategy(mode);
        var maxScrollAttempts = CalculateMaxScrollAttempts(targetCount, mode);
        var noNewDataCount = 0;
        var maxNoNewDataAttempts = 3;

        _logger.LogInformation("开始收集循环：策略={Strategy}, 最大滚动次数={MaxScrolls}", 
            strategy.GetType().Name, maxScrollAttempts);

        using var timeoutCts = new CancellationTokenSource(timeout);
        using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        for (int scrollAttempt = 0; scrollAttempt < maxScrollAttempts && !combinedCts.Token.IsCancellationRequested; scrollAttempt++)
        {
            var beforeCount = _collectedNotes.Count;
            
            // 等待新数据加载
            await WaitForNewDataAsync(page, strategy);
            
            // 检查是否有新数据
            var afterCount = _collectedNotes.Count;
            if (afterCount == beforeCount)
            {
                noNewDataCount++;
                _logger.LogDebug("第{Attempt}次滚动未获得新数据，连续无新数据次数：{NoNewDataCount}/{MaxAttempts}",
                    scrollAttempt + 1, noNewDataCount, maxNoNewDataAttempts);
                
                if (noNewDataCount >= maxNoNewDataAttempts)
                {
                    _logger.LogInformation("连续{Count}次滚动无新数据，停止收集", noNewDataCount);
                    break;
                }
            }
            else
            {
                noNewDataCount = 0; // 重置无新数据计数
                _logger.LogDebug("第{Attempt}次滚动获得{NewCount}条新数据，总计：{Total}/{Target}",
                    scrollAttempt + 1, afterCount - beforeCount, afterCount, targetCount);
            }

            // 检查是否达到目标数量
            if (_collectedNotes.Count >= targetCount)
            {
                _logger.LogInformation("已达到目标数量{Target}，收集完成", targetCount);
                break;
            }

            // 执行智能滚动
            await ExecuteSmartScrollAsync(page, strategy, scrollAttempt);
            
            // 更新状态
            UpdateCollectionProgress();
        }

        return new CollectionLoopResult
        {
            Success = true,
            FinalCount = _collectedNotes.Count,
            TotalScrollAttempts = Math.Min(maxScrollAttempts, _performanceMonitor.ScrollCount),
            ReachedTarget = _collectedNotes.Count >= targetCount
        };
    }

    /// <summary>
    /// 等待新数据加载
    /// </summary>
    private async Task WaitForNewDataAsync(IPage page, ICollectionStrategy strategy)
    {
        var maxWaitTime = strategy.GetDataLoadWaitTime();
        var checkInterval = TimeSpan.FromMilliseconds(200);
        var elapsed = TimeSpan.Zero;
        var lastCount = _collectedNotes.Count;

        while (elapsed < maxWaitTime)
        {
            await Task.Delay(checkInterval);
            elapsed = elapsed.Add(checkInterval);

            // 如果数据量有增长，说明新数据正在加载
            if (_collectedNotes.Count > lastCount)
            {
                await Task.Delay(strategy.GetDataStabilizeWaitTime());
                break;
            }
        }
    }

    /// <summary>
    /// 执行智能滚动策略
    /// </summary>
    private async Task ExecuteSmartScrollAsync(IPage page, ICollectionStrategy strategy, int scrollAttempt)
    {
        _performanceMonitor.RecordScrollAttempt();
        
        var scrollParams = strategy.CalculateScrollParameters(
            _collectedNotes.Count, _currentStatus.TargetCount, scrollAttempt);

        _logger.LogDebug("执行滚动：距离={Distance}px, 延时={Delay}ms, 进度={Progress:P}",
            scrollParams.Distance, scrollParams.DelayAfterScroll.TotalMilliseconds,
            (double)_collectedNotes.Count / _currentStatus.TargetCount);

        // 执行拟人化滚动
        await _humanizedInteraction.HumanScrollAsync(page, scrollParams.Distance);
        
        // 滚动后延时
        await Task.Delay(scrollParams.DelayAfterScroll);
    }

    /// <summary>
    /// 设置网络拦截器来收集推荐数据
    /// </summary>
    private async Task SetupNetworkInterceptorAsync(IPage page, InterceptorConfig config)
    {
        await page.RouteAsync("**/api/sns/web/v1/homefeed**", async route =>
        {
            var response = await route.FetchAsync();
            
            if (response.Status == 200)
            {
                try
                {
                    var responseText = await response.TextAsync();
                    await ProcessHomefeedResponseAsync(responseText);
                    _performanceMonitor.RecordSuccessfulRequest();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "处理推荐API响应失败");
                    _performanceMonitor.RecordFailedRequest();
                }
            }
            else
            {
                _logger.LogWarning("推荐API请求失败：状态码={StatusCode}", response.Status);
                _performanceMonitor.RecordFailedRequest();
            }

            await route.ContinueAsync();
        });
    }

    /// <summary>
    /// 处理推荐API响应数据
    /// </summary>
    private async Task ProcessHomefeedResponseAsync(string responseText)
    {
        try
        {
            var response = JsonSerializer.Deserialize<HomefeedResponse>(responseText);
            if (response?.Success == true && response.Data?.Items != null)
            {
                var newNotes = new List<NoteInfo>();
                
                foreach (var item in response.Data.Items)
                {
                    var noteInfo = HomefeedConverter.ConvertToNoteInfo(item);
                    if (noteInfo != null && !_seenNoteIds.Contains(noteInfo.Id))
                    {
                        lock (_stateLock)
                        {
                            if (_seenNoteIds.Add(noteInfo.Id))
                            {
                                _collectedNotes.Add(noteInfo);
                                newNotes.Add(noteInfo);
                            }
                        }
                    }
                }

                if (newNotes.Count > 0)
                {
                    _logger.LogDebug("从API响应中提取到{Count}条新笔记，总计：{Total}",
                        newNotes.Count, _collectedNotes.Count);
                }
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "解析推荐API响应JSON失败");
        }
    }

    /// <summary>
    /// 导航到推荐页面并触发发现页面API
    /// </summary>
    private async Task NavigateToHomefeedAsync(IPage page)
    {
        _currentStatus.Phase = CollectionPhase.Navigating;
        
        const string homefeedUrl = "https://www.xiaohongshu.com";
        _logger.LogDebug("导航到推荐页面：{Url}", homefeedUrl);
        
        // 1. 导航到首页
        await page.GotoAsync(homefeedUrl);
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 10000 });
        
        // 2. 等待页面完全加载
        await Task.Delay(2000);
        
        // 3. 查找并点击发现按钮以触发homefeed API
        await TriggerDiscoverPageAsync(page);
    }

    /// <summary>
    /// 触发发现页面以启动homefeed API请求
    /// </summary>
    private async Task TriggerDiscoverPageAsync(IPage page)
    {
        _logger.LogDebug("开始触发发现页面API");
        
        try
        {
            // 获取发现按钮选择器
            var discoverSelectors = _selectorManager.GetSelectors("SidebarDiscoverLink");
            
            IElementHandle? discoverButton = null;
            foreach (var selector in discoverSelectors)
            {
                try
                {
                    _logger.LogDebug("尝试查找发现按钮：{Selector}", selector);
                    discoverButton = await page.QuerySelectorAsync(selector);
                    if (discoverButton != null)
                    {
                        _logger.LogDebug("找到发现按钮：{Selector}", selector);
                        break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "选择器{Selector}查找失败", selector);
                    continue;
                }
            }

            if (discoverButton != null)
            {
                // 执行拟人化点击
                await _humanizedInteraction.HumanClickAsync(page, discoverButton);
                _logger.LogDebug("已点击发现按钮，等待页面响应");
                
                // 等待页面跳转和API触发
                await Task.Delay(3000);
                
                // 验证是否成功进入发现页面
                await ValidateDiscoverPageNavigationAsync(page);
            }
            else
            {
                _logger.LogWarning("未找到发现按钮，尝试直接导航到发现页面");
                await DirectNavigateToDiscoverAsync(page);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "触发发现页面失败，尝试备用方案");
            await DirectNavigateToDiscoverAsync(page);
        }
    }

    /// <summary>
    /// 直接导航到发现页面的备用方案
    /// </summary>
    private async Task DirectNavigateToDiscoverAsync(IPage page)
    {
        try
        {
            const string discoverUrl = "https://www.xiaohongshu.com/explore";
            _logger.LogDebug("直接导航到发现页面：{Url}", discoverUrl);
            
            await page.GotoAsync(discoverUrl);
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 10000 });
            await Task.Delay(2000);
            
            await ValidateDiscoverPageNavigationAsync(page);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "直接导航到发现页面失败");
            throw;
        }
    }

    /// <summary>
    /// 验证发现页面导航是否成功
    /// </summary>
    private async Task ValidateDiscoverPageNavigationAsync(IPage page)
    {
        try
        {
            var currentUrl = page.Url;
            _logger.LogDebug("当前页面URL：{Url}", currentUrl);
            
            // 检查URL是否包含explore相关路径
            if (currentUrl.Contains("/explore") || currentUrl.Contains("channel_id=homefeed_recommend"))
            {
                _logger.LogInformation("成功导航到发现页面，URL：{Url}", currentUrl);
                return;
            }
            
            // 通过DOM检测发现页面元素
            var pageState = await _selectorManager.DetectPageStateAsync(page);
            if (pageState == PageState.Explore)
            {
                _logger.LogInformation("通过DOM检测确认已进入发现页面");
                return;
            }
            
            // 检测特征元素
            var exploreSelectors = new[]
            {
                "#exploreFeeds",
                "[data-testid='explore-page']", 
                ".channel-container"
            };
            
            foreach (var selector in exploreSelectors)
            {
                var element = await page.QuerySelectorAsync(selector);
                if (element != null)
                {
                    _logger.LogInformation("通过特征元素{Selector}确认已进入发现页面", selector);
                    return;
                }
            }
            
            _logger.LogWarning("无法确认是否成功进入发现页面，当前URL：{Url}", currentUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "验证发现页面导航状态失败");
        }
    }

    /// <summary>
    /// 创建拦截器配置
    /// </summary>
    private InterceptorConfig CreateInterceptorConfig(RecommendCollectionMode mode)
    {
        return mode switch
        {
            RecommendCollectionMode.Fast => new InterceptorConfig
            {
                EnableCaching = true,
                MaxCacheSize = 1000,
                RequestTimeout = TimeSpan.FromSeconds(10)
            },
            RecommendCollectionMode.Standard => new InterceptorConfig
            {
                EnableCaching = true,
                MaxCacheSize = 2000,
                RequestTimeout = TimeSpan.FromSeconds(15)
            },
            RecommendCollectionMode.Careful => new InterceptorConfig
            {
                EnableCaching = true,
                MaxCacheSize = 3000,
                RequestTimeout = TimeSpan.FromSeconds(20)
            },
            _ => throw new ArgumentException($"不支持的收集模式: {mode}")
        };
    }

    /// <summary>
    /// 创建收集策略
    /// </summary>
    private ICollectionStrategy CreateCollectionStrategy(RecommendCollectionMode mode)
    {
        return mode switch
        {
            RecommendCollectionMode.Fast => new FastCollectionStrategy(),
            RecommendCollectionMode.Standard => new StandardCollectionStrategy(),
            RecommendCollectionMode.Careful => new CarefulCollectionStrategy(),
            _ => new StandardCollectionStrategy()
        };
    }

    /// <summary>
    /// 计算最大滚动次数
    /// </summary>
    private int CalculateMaxScrollAttempts(int targetCount, RecommendCollectionMode mode)
    {
        // 基础计算：每次滚动期望获得的笔记数
        var expectedNotesPerScroll = mode switch
        {
            RecommendCollectionMode.Fast => 8,      // 快速模式期望每次获得更多
            RecommendCollectionMode.Standard => 6,  // 标准模式
            RecommendCollectionMode.Careful => 4,   // 谨慎模式期望每次获得较少，但更稳定
            _ => 6
        };

        var baseAttempts = (int)Math.Ceiling((double)targetCount / expectedNotesPerScroll);
        var safetyMultiplier = 2.5; // 安全倍数，考虑到可能的重复和失败
        
        var maxAttempts = (int)(baseAttempts * safetyMultiplier);
        
        // 设置合理的上下限
        return Math.Max(5, Math.Min(maxAttempts, 50));
    }

    /// <summary>
    /// 处理最终收集结果
    /// </summary>
    private async Task<SmartCollectionResult> ProcessCollectionResultAsync(
        CollectionLoopResult loopResult, TimeSpan duration)
    {
        _currentStatus.Phase = CollectionPhase.Completed;
        
        var notes = _collectedNotes.ToList();
        var metrics = _performanceMonitor.GetMetrics();
        
        return new SmartCollectionResult
        {
            Success = loopResult.Success,
            CollectedNotes = notes,
            CollectedCount = notes.Count,
            TargetCount = _currentStatus.TargetCount,
            RequestCount = _performanceMonitor.RequestCount,
            Duration = duration,
            PerformanceMetrics = metrics,
            CollectionDetails = _currentStatus,
            ReachedTarget = loopResult.ReachedTarget,
            EfficiencyScore = CalculateEfficiencyScore(notes.Count, _currentStatus.TargetCount, metrics)
        };
    }

    /// <summary>
    /// 计算收集效率分数
    /// </summary>
    private double CalculateEfficiencyScore(int collected, int target, CollectionPerformanceMetrics metrics)
    {
        if (target == 0) return 0;
        
        var completionRate = Math.Min(1.0, (double)collected / target);
        var successRate = metrics.RequestCount > 0 ? (double)metrics.SuccessfulRequests / metrics.RequestCount : 0;
        var scrollEfficiency = metrics.ScrollCount > 0 ? (double)collected / metrics.ScrollCount : 0;
        
        // 综合评分：完成率40% + 成功率30% + 滚动效率30%
        return (completionRate * 0.4 + successRate * 0.3 + Math.Min(1.0, scrollEfficiency / 5) * 0.3) * 100;
    }

    /// <summary>
    /// 更新收集进度
    /// </summary>
    private void UpdateCollectionProgress()
    {
        lock (_stateLock)
        {
            _currentStatus.CurrentCount = _collectedNotes.Count;
            _currentStatus.Progress = _currentStatus.TargetCount > 0 
                ? (double)_currentStatus.CurrentCount / _currentStatus.TargetCount 
                : 0;
            _currentStatus.LastUpdateTime = DateTime.UtcNow;
        }
    }

    public CollectionStatus GetCurrentStatus() => _currentStatus;

    public void ResetCollectionState()
    {
        lock (_stateLock)
        {
            _collectedNotes.Clear();
            _seenNoteIds.Clear();
            _performanceMonitor = new CollectionPerformanceMonitor();
            _currentStatus = new CollectionStatus();
        }
        
        _logger.LogDebug("收集状态已重置");
    }
}

#region 支持类和接口

/// <summary>
/// 收集策略接口
/// </summary>
public interface ICollectionStrategy
{
    TimeSpan GetDataLoadWaitTime();
    TimeSpan GetDataStabilizeWaitTime();
    ScrollParameters CalculateScrollParameters(int currentCount, int targetCount, int scrollAttempt);
}

/// <summary>
/// 滚动参数
/// </summary>
public record ScrollParameters(
    int Distance,
    TimeSpan DelayAfterScroll
);

/// <summary>
/// 快速收集策略
/// </summary>
public class FastCollectionStrategy : ICollectionStrategy
{
    public TimeSpan GetDataLoadWaitTime() => TimeSpan.FromMilliseconds(500);
    public TimeSpan GetDataStabilizeWaitTime() => TimeSpan.FromMilliseconds(300);
    
    public ScrollParameters CalculateScrollParameters(int currentCount, int targetCount, int scrollAttempt)
    {
        var distance = 600 + (scrollAttempt % 3) * 100; // 600-800px
        var delay = TimeSpan.FromMilliseconds(800 + Random.Shared.Next(0, 400));
        return new ScrollParameters(distance, delay);
    }
}

/// <summary>
/// 标准收集策略
/// </summary>
public class StandardCollectionStrategy : ICollectionStrategy
{
    public TimeSpan GetDataLoadWaitTime() => TimeSpan.FromMilliseconds(1000);
    public TimeSpan GetDataStabilizeWaitTime() => TimeSpan.FromMilliseconds(500);
    
    public ScrollParameters CalculateScrollParameters(int currentCount, int targetCount, int scrollAttempt)
    {
        var progress = targetCount > 0 ? (double)currentCount / targetCount : 0;
        var distance = 500 + (int)(progress * 200) + (scrollAttempt % 4) * 50; // 500-750px
        var delay = TimeSpan.FromMilliseconds(1200 + Random.Shared.Next(0, 600));
        return new ScrollParameters(distance, delay);
    }
}

/// <summary>
/// 谨慎收集策略
/// </summary>
public class CarefulCollectionStrategy : ICollectionStrategy
{
    public TimeSpan GetDataLoadWaitTime() => TimeSpan.FromMilliseconds(1500);
    public TimeSpan GetDataStabilizeWaitTime() => TimeSpan.FromMilliseconds(800);
    
    public ScrollParameters CalculateScrollParameters(int currentCount, int targetCount, int scrollAttempt)
    {
        var distance = 400 + (scrollAttempt % 5) * 30; // 400-520px
        var delay = TimeSpan.FromMilliseconds(2000 + Random.Shared.Next(0, 1000));
        return new ScrollParameters(distance, delay);
    }
}

/// <summary>
/// 收集循环结果
/// </summary>
public record CollectionLoopResult
{
    public bool Success { get; init; }
    public int FinalCount { get; init; }
    public int TotalScrollAttempts { get; init; }
    public bool ReachedTarget { get; init; }
}

/// <summary>
/// 性能监控器 - 内部实现类，不在接口中暴露
/// </summary>
internal class CollectionPerformanceMonitor
{
    private int _requestCount;
    private int _successfulRequests;
    private int _failedRequests;
    private int _scrollCount;
    private readonly DateTime _startTime = DateTime.UtcNow;

    public int RequestCount => _requestCount;
    public int SuccessfulRequests => _successfulRequests;
    public int FailedRequests => _failedRequests;
    public int ScrollCount => _scrollCount;

    public void RecordSuccessfulRequest() => Interlocked.Increment(ref _successfulRequests);
    public void RecordFailedRequest() => Interlocked.Increment(ref _failedRequests);
    public void RecordScrollAttempt() => Interlocked.Increment(ref _scrollCount);

    private void UpdateRequestCount()
    {
        _requestCount = _successfulRequests + _failedRequests;
    }

    public CollectionPerformanceMetrics GetMetrics()
    {
        UpdateRequestCount();
        var duration = DateTime.UtcNow - _startTime;
        
        return new CollectionPerformanceMetrics
        {
            RequestCount = _requestCount,
            SuccessfulRequests = _successfulRequests,
            FailedRequests = _failedRequests,
            ScrollCount = _scrollCount,
            Duration = duration
        };
    }
}

#endregion