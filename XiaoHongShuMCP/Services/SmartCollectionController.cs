using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace XiaoHongShuMCP.Services;

/// <summary>
/// 智能收集控制器 - 重构版
/// 集成完善的FeedApiMonitor，删除内嵌的简陋API监听系统
/// </summary>
public class SmartCollectionController : ISmartCollectionController
{
    private readonly ILogger<SmartCollectionController> _logger;
    private readonly IBrowserManager _browserManager;
    private readonly IHumanizedInteractionService _humanizedInteraction;
    private readonly IDomElementManager _domElementManager;
    private readonly IPageLoadWaitService _pageLoadWaitService;
    private readonly UniversalApiMonitor _universalApiMonitor;

    // 智能收集状态管理
    private readonly List<NoteInfo> _collectedNotes;
    private readonly HashSet<string> _seenNoteIds;
    private readonly object _stateLock = new();

    // 性能监控
    private readonly CollectionPerformanceMonitor _performanceMonitor;
    private SmartCollectionStatus _currentStatus;

    public SmartCollectionController(
        ILogger<SmartCollectionController> logger,
        IBrowserManager browserManager,
        IHumanizedInteractionService humanizedInteraction,
        IDomElementManager domElementManager,
        IPageLoadWaitService pageLoadWaitService,
        UniversalApiMonitor universalApiMonitor)
    {
        _logger = logger;
        _browserManager = browserManager;
        _humanizedInteraction = humanizedInteraction;
        _domElementManager = domElementManager;
        _pageLoadWaitService = pageLoadWaitService;
        _universalApiMonitor = universalApiMonitor;

        _collectedNotes = [];
        _seenNoteIds = [];
        _performanceMonitor = new CollectionPerformanceMonitor();
    }

    /// <inheritdoc />
    public async Task<SmartCollectionResult> ExecuteSmartCollectionAsync(
        IBrowserContext context, IPage page,
        int targetCount, RecommendCollectionMode mode, TimeSpan? timeout,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        timeout ??= TimeSpan.FromMinutes(5);
        
        _logger.LogInformation("开始智能收集：目标={TargetCount}, 模式={Mode}, 超时={Timeout}分钟", 
            targetCount, mode, timeout.Value.TotalMinutes);

        try
        {
            ResetCollectionState();
            
            // 设置通用API监听器，监听推荐端点
            var endpointsToMonitor = new HashSet<UniversalApiMonitor.ApiEndpointType> 
            { 
                UniversalApiMonitor.ApiEndpointType.Homefeed 
            };
            var setupSuccess = await _universalApiMonitor.SetupMonitorAsync(page, endpointsToMonitor, TimeSpan.FromSeconds(10));
            if (!setupSuccess)
            {
                _logger.LogWarning("通用API监听器设置失败，将使用降级模式");
            }
            else
            {
                _logger.LogInformation("通用API监听器设置成功，开始监听推荐接口");
            }

            // 确保在发现页面
            var navigationResult = await DirectNavigateToDiscoverAsync(page);
            if (!navigationResult.Success)
            {
                return SmartCollectionResult.CreateFailure(
                    navigationResult.ErrorMessage ?? "导航失败",
                    null, 
                    targetCount,
                    0,
                    DateTime.UtcNow - startTime);
            }

            // 创建收集策略
            var strategy = CreateCollectionStrategy(mode);
            
            // 执行收集循环
            var collectionResult = await ExecuteCollectionLoopAsync(page, targetCount, strategy, timeout.Value);
            
            if (collectionResult.Success)
            {
                // 获取监听到的API数据
                var monitoredNoteDetails = _universalApiMonitor.GetMonitoredNoteDetails(UniversalApiMonitor.ApiEndpointType.Homefeed);
                if (monitoredNoteDetails.Count > 0)
                {
                    _logger.LogInformation("从推荐API监听获取到 {ApiNoteCount} 条笔记详情", monitoredNoteDetails.Count);
                    
                    // 将API数据转换为NoteInfo并合并到收集结果
                    var apiNoteInfos = monitoredNoteDetails.Cast<NoteInfo>().ToList();
                    MergeApiDataWithCollectedNotes(apiNoteInfos);
                }
                else
                {
                    _logger.LogWarning("未从推荐API监听获取到任何数据，可能的原因：API端点变更、网络问题或页面未正确触发请求");
                }

                var result = ProcessCollectionResultAsync(collectionResult, startTime, targetCount);
                
                _logger.LogInformation("智能收集完成：收集={ActualCount}/{TargetCount}, 耗时={Duration}ms, 效率={Efficiency:F2}", 
                    _collectedNotes.Count, targetCount, result.Duration.TotalMilliseconds, CalculateEfficiencyScore());

                return result;
            }
            else
            {
                return SmartCollectionResult.CreateFailure(
                    collectionResult.ErrorMessage,
                    _collectedNotes,
                    targetCount,
                    0,
                    DateTime.UtcNow - startTime);
            }
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow - startTime;
            _logger.LogError(ex, "智能收集执行异常，耗时{Duration}ms", duration.TotalMilliseconds);
            
            return SmartCollectionResult.CreateFailure(
                ex.Message,
                _collectedNotes,
                targetCount,
                0,
                duration);
        }
        finally
        {
            // 清理API监听器数据
            try
            {
                await _universalApiMonitor.StopMonitoringAsync();
                _universalApiMonitor.ClearMonitoredData();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "清理通用API监听器失败");
            }
        }
    }

    /// <summary>
    /// 执行收集循环
    /// </summary>
    private async Task<CollectionLoopResult> ExecuteCollectionLoopAsync(
        IPage page, int targetCount, ICollectionStrategy strategy, TimeSpan timeout)
    {
        var startTime = DateTime.UtcNow;
        var maxScrollAttempts = CalculateMaxScrollAttempts(targetCount);
        var scrollAttempts = 0;
        var consecutiveEmptyScrolls = 0;
        var lastCollectedCount = 0;

        _currentStatus = SmartCollectionStatus.Collecting;
        _logger.LogDebug("开始收集循环：目标={Target}, 最大滚动次数={MaxScrolls}", targetCount, maxScrollAttempts);

        while (_collectedNotes.Count < targetCount && 
               scrollAttempts < maxScrollAttempts && 
               DateTime.UtcNow - startTime < timeout)
        {
            try
            {
                UpdateCollectionProgress(scrollAttempts, maxScrollAttempts, _collectedNotes.Count, targetCount);

                // 执行智能滚动
                var scrollResult = await ExecuteSmartScrollAsync(page, strategy.GetScrollParameters());
                if (!scrollResult.Success)
                {
                    _logger.LogWarning("滚动操作失败：{Error}", scrollResult.ErrorMessage);
                    break;
                }

                scrollAttempts++;
                _performanceMonitor.RecordScrollOperation();

                // 等待新数据加载
                var waitResult = await WaitForNewDataAsync(page, strategy.GetWaitDelay());
                if (waitResult.Success)
                {
                    // 等待API监听获取数据 - 使用更长的等待时间
                    var apiWaitResult = await _universalApiMonitor.WaitForResponsesAsync(
                        UniversalApiMonitor.ApiEndpointType.Homefeed, 1, TimeSpan.FromSeconds(8));
                    if (apiWaitResult)
                    {
                        var newApiData = _universalApiMonitor.GetMonitoredNoteDetails(UniversalApiMonitor.ApiEndpointType.Homefeed);
                        if (newApiData.Count > 0)
                        {
                            _logger.LogDebug("API监听获取到 {Count} 条新数据", newApiData.Count);
                            var apiNoteInfos = newApiData.Cast<NoteInfo>().ToList();
                            MergeApiDataWithCollectedNotes(apiNoteInfos);
                            
                            // 清理已处理的数据，避免重复计数
                            _universalApiMonitor.ClearMonitoredData(UniversalApiMonitor.ApiEndpointType.Homefeed);
                        }
                    }
                    else
                    {
                        _logger.LogDebug("等待API数据超时，继续下一轮滚动");
                    }
                    
                    consecutiveEmptyScrolls = 0;
                }
                else
                {
                    consecutiveEmptyScrolls++;
                    if (consecutiveEmptyScrolls >= 3)
                    {
                        _logger.LogInformation("连续{Count}次未获取到新数据，停止收集", consecutiveEmptyScrolls);
                        break;
                    }
                }

                // 检查进度
                if (_collectedNotes.Count > lastCollectedCount)
                {
                    lastCollectedCount = _collectedNotes.Count;
                    _logger.LogDebug("收集进度：{Current}/{Target} ({Percentage:F1}%)", 
                        _collectedNotes.Count, targetCount, 
                        (double)_collectedNotes.Count / targetCount * 100);
                }

                // 应用策略延时
                await Task.Delay(strategy.GetOperationDelay());
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "收集循环迭代异常，继续执行");
                consecutiveEmptyScrolls++;
            }
        }

        var duration = DateTime.UtcNow - startTime;
        var success = _collectedNotes.Count > 0;

        _logger.LogInformation("收集循环完成：收集={Count}, 滚动={Scrolls}, 耗时={Duration}ms, 成功={Success}", 
            _collectedNotes.Count, scrollAttempts, duration.TotalMilliseconds, success);

        return new CollectionLoopResult(
            success, 
            success ? null : "未能收集到数据",
            _collectedNotes.Count,
            scrollAttempts,
            duration);
    }

    /// <summary>
    /// 等待新数据加载
    /// </summary>
    private async Task<(bool Success, string? ErrorMessage)> WaitForNewDataAsync(IPage page, TimeSpan delay)
    {
        try
        {
            await Task.Delay(delay);
            
            // 检查页面是否还在加载
            var isLoading = await _pageLoadWaitService.IsPageLoadingAsync(page);
            if (isLoading)
            {
                _logger.LogDebug("页面仍在加载，等待完成...");
                await _pageLoadWaitService.WaitForLoadCompleteAsync(page, TimeSpan.FromSeconds(10));
            }

            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "等待新数据时发生异常");
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// 执行智能滚动
    /// </summary>
    private async Task<(bool Success, string? ErrorMessage)> ExecuteSmartScrollAsync(IPage page, ScrollParameters parameters)
    {
        try
        {
            await _humanizedInteraction.PerformNaturalScrollAsync(page, parameters.Distance, parameters.Duration);
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// 合并API数据与收集的笔记
    /// </summary>
    private void MergeApiDataWithCollectedNotes(List<NoteInfo> apiNoteInfos)
    {
        if (apiNoteInfos.Count == 0) return;

        lock (_stateLock)
        {
            var newCount = 0;
            foreach (var noteInfo in apiNoteInfos)
            {
                if (!string.IsNullOrEmpty(noteInfo.Id) && _seenNoteIds.Add(noteInfo.Id))
                {
                    _collectedNotes.Add(noteInfo);
                    newCount++;
                }
            }
            
            if (newCount > 0)
            {
                _logger.LogDebug("合并API数据：新增 {NewCount} 条笔记，总计 {Total} 条", newCount, _collectedNotes.Count);
            }
        }
    }

    /// <summary>
    /// 直接导航到发现页面
    /// </summary>
    private async Task<(bool Success, string? ErrorMessage)> DirectNavigateToDiscoverAsync(IPage page)
    {
        try
        {
            _logger.LogDebug("直接导航到发现页面");
            
            var discoverUrl = "https://www.xiaohongshu.com/explore";
            await page.GotoAsync(discoverUrl, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle,
                Timeout = 15000
            });

            await Task.Delay(2000); // 等待页面稳定

            // 验证是否成功到达发现页面
            var currentUrl = page.Url;
            if (!currentUrl.Contains("/explore"))
            {
                _logger.LogWarning("页面URL验证失败: 期望包含 '/explore'，实际URL: {ActualUrl}", currentUrl);
                return (false, $"页面URL不正确: {currentUrl}");
            }

            _logger.LogInformation("成功导航到发现页面: {Url}", currentUrl);
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导航到发现页面失败");
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// 创建收集策略
    /// </summary>
    private ICollectionStrategy CreateCollectionStrategy(RecommendCollectionMode mode)
    {
        return mode switch
        {
            RecommendCollectionMode.Fast => new FastCollectionStrategy(),
            RecommendCollectionMode.Careful => new CarefulCollectionStrategy(),
            _ => new StandardCollectionStrategy()
        };
    }

    /// <summary>
    /// 计算最大滚动尝试次数
    /// </summary>
    private static int CalculateMaxScrollAttempts(int targetCount)
    {
        // 根据目标数量动态调整最大滚动次数
        return targetCount switch
        {
            <= 10 => 15,    // 小量收集，较少滚动
            <= 30 => 25,    // 中量收集，适中滚动  
            <= 50 => 35,    // 大量收集，更多滚动
            _ => 50         // 超大量收集，最大滚动次数
        };
    }

    /// <summary>
    /// 处理收集结果
    /// </summary>
    private SmartCollectionResult ProcessCollectionResultAsync(CollectionLoopResult loopResult, DateTime startTime, int targetCount)
    {
        var duration = DateTime.UtcNow - startTime;
        
        var performanceMetrics = new CollectionPerformanceMetrics(
            _performanceMonitor.SuccessfulRequests,
            _performanceMonitor.FailedRequests, 
            _performanceMonitor.ScrollCount,
            duration);

        return SmartCollectionResult.CreateSuccess(
            _collectedNotes.ToList(),
            targetCount,
            _performanceMonitor.RequestCount,
            duration,
            performanceMetrics);
    }

    /// <summary>
    /// 计算效率评分
    /// </summary>
    private double CalculateEfficiencyScore()
    {
        if (_performanceMonitor.ScrollCount == 0) return 0;
        
        var notesPerScroll = (double)_collectedNotes.Count / _performanceMonitor.ScrollCount;
        var successRate = _performanceMonitor.RequestCount > 0 
            ? (double)_performanceMonitor.SuccessfulRequests / _performanceMonitor.RequestCount 
            : 0;
            
        return (notesPerScroll * 10 + successRate * 100) / 2;
    }

    /// <summary>
    /// 更新收集进度
    /// </summary>
    private void UpdateCollectionProgress(int currentScrolls, int maxScrolls, int collectedCount, int targetCount)
    {
        var scrollProgress = (double)currentScrolls / maxScrolls * 100;
        var collectionProgress = (double)collectedCount / targetCount * 100;
        
        _currentStatus = collectedCount >= targetCount 
            ? SmartCollectionStatus.Completed 
            : SmartCollectionStatus.Collecting;
    }

    /// <summary>
    /// 获取当前状态
    /// </summary>
    public CollectionStatus GetCurrentStatus() 
    {
        return new CollectionStatus
        {
            Phase = _currentStatus switch
            {
                SmartCollectionStatus.Idle => CollectionPhase.Initializing,
                SmartCollectionStatus.Collecting => CollectionPhase.Collecting,
                SmartCollectionStatus.Completed => CollectionPhase.Completed,
                SmartCollectionStatus.Failed => CollectionPhase.Failed,
                _ => CollectionPhase.Initializing
            },
            CurrentCount = _collectedNotes.Count,
            TargetCount = 0, // 这个需要在收集过程中设置
            Progress = 0,    // 这个需要在收集过程中计算
            CollectionMode = RecommendCollectionMode.Standard,
            StartTime = DateTime.UtcNow,
            LastUpdateTime = DateTime.UtcNow
        };
    }

    /// <summary>
    /// 重置收集状态
    /// </summary>
    public void ResetCollectionState()
    {
        lock (_stateLock)
        {
            _collectedNotes.Clear();
            _seenNoteIds.Clear();
            _performanceMonitor.Reset();
            _currentStatus = SmartCollectionStatus.Idle;
            
            _logger.LogDebug("收集状态已重置");
        }
    }
}

/// <summary>
/// 收集循环结果
/// </summary>
public record CollectionLoopResult(
    bool Success,
    string? ErrorMessage,
    int CollectedCount,
    int ScrollAttempts,
    TimeSpan Duration
);

/// <summary>
/// 滚动参数
/// </summary>
public record ScrollParameters(
    int Distance,
    TimeSpan Duration
);

/// <summary>
/// 收集策略接口
/// </summary>
public interface ICollectionStrategy
{
    ScrollParameters GetScrollParameters();
    TimeSpan GetWaitDelay();
    TimeSpan GetOperationDelay();
}

/// <summary>
/// 快速收集策略
/// </summary>
public class FastCollectionStrategy : ICollectionStrategy
{
    public ScrollParameters GetScrollParameters() => new(500, TimeSpan.FromMilliseconds(800));
    public TimeSpan GetWaitDelay() => TimeSpan.FromMilliseconds(1000);
    public TimeSpan GetOperationDelay() => TimeSpan.FromMilliseconds(500);
}

/// <summary>
/// 标准收集策略
/// </summary>
public class StandardCollectionStrategy : ICollectionStrategy
{
    public ScrollParameters GetScrollParameters() => new(400, TimeSpan.FromMilliseconds(1200));
    public TimeSpan GetWaitDelay() => TimeSpan.FromMilliseconds(1500);
    public TimeSpan GetOperationDelay() => TimeSpan.FromMilliseconds(800);
}

/// <summary>
/// 谨慎收集策略
/// </summary>
public class CarefulCollectionStrategy : ICollectionStrategy
{
    public ScrollParameters GetScrollParameters() => new(300, TimeSpan.FromMilliseconds(1500));
    public TimeSpan GetWaitDelay() => TimeSpan.FromMilliseconds(2000);
    public TimeSpan GetOperationDelay() => TimeSpan.FromMilliseconds(1200);
}

/// <summary>
/// 收集性能监控器
/// </summary>
public class CollectionPerformanceMonitor
{
    private int _successfulRequests;
    private int _failedRequests;
    private int _scrollCount;

    public int SuccessfulRequests => _successfulRequests;
    public int FailedRequests => _failedRequests;
    public int ScrollCount => _scrollCount;
    public int RequestCount => _successfulRequests + _failedRequests;

    public void RecordSuccessfulRequest() => Interlocked.Increment(ref _successfulRequests);
    public void RecordFailedRequest() => Interlocked.Increment(ref _failedRequests);
    public void RecordScrollOperation() => Interlocked.Increment(ref _scrollCount);

    public void Reset()
    {
        _successfulRequests = 0;
        _failedRequests = 0;
        _scrollCount = 0;
    }
}

/// <summary>
/// 智能收集状态枚举
/// </summary>
public enum SmartCollectionStatus
{
    Idle,           // 空闲状态
    Collecting,     // 收集中
    Completed,      // 已完成
    Failed          // 失败状态
}