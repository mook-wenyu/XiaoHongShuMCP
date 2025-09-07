using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using System.Text.Json;

namespace XiaoHongShuMCP.Services;

/// <summary>
/// 推荐服务实现
/// </summary>
public class RecommendService : IRecommendService
{
    private readonly ILogger<RecommendService> _logger;
    private readonly IBrowserManager _browserManager;
    private readonly IDiscoverPageNavigationService _navigationService;
    private readonly ISmartCollectionController _collectionController;

    public RecommendService(
        ILogger<RecommendService> logger,
        IBrowserManager browserManager,
        IDiscoverPageNavigationService navigationService,
        ISmartCollectionController collectionController)
    {
        _logger = logger;
        _browserManager = browserManager;
        _navigationService = navigationService;
        _collectionController = collectionController;
    }

    /// <inheritdoc />
    public async Task<OperationResult<RecommendListResult>> GetRecommendedNotesAsync(int limit = 20, TimeSpan? timeout = null)
    {
        timeout ??= TimeSpan.FromMinutes(5);
        var startTime = DateTime.UtcNow;

        try
        {
            _logger.LogInformation("开始获取推荐笔记，数量：{Limit}，超时：{Timeout}分钟", limit, timeout.Value.TotalMinutes);

            // 1. 验证浏览器连接
            var browserValidation = await ValidateBrowserConnectionAsync();
            if (!browserValidation.Success)
            {
                return OperationResult<RecommendListResult>.Fail(
                    $"浏览器连接验证失败：{browserValidation.ErrorMessage ?? "验证失败"}",
                    ErrorType.BrowserError,
                    "BROWSER_VALIDATION_FAILED");
            }

            var context = await _browserManager.GetBrowserContextAsync();
            var page = context.Pages.FirstOrDefault() ?? await context.NewPageAsync();

            // 2. 导航到发现页面并触发API
            var navigationResult = await _navigationService.NavigateToDiscoverPageAsync(page, TimeSpan.FromSeconds(30));
            if (!navigationResult.Success)
            {
                _logger.LogError("导航到发现页面失败：{Error}", navigationResult.ErrorMessage);
                return OperationResult<RecommendListResult>.Fail(
                    $"导航到发现页面失败：{navigationResult.ErrorMessage}",
                    ErrorType.NavigationError,
                    "NAVIGATION_FAILED");
            }

            _logger.LogInformation("导航成功，方法：{Method}，API触发：{ApiTriggered}", 
                navigationResult.Method, navigationResult.ApiTriggered);

            // 3. 执行智能收集
            var collectionMode = limit <= 10 
                ? RecommendCollectionMode.Fast 
                : limit <= 50 
                    ? RecommendCollectionMode.Standard 
                    : RecommendCollectionMode.Careful;

            var collectionResult = await _collectionController.ExecuteSmartCollectionAsync(
                limit, collectionMode, timeout);

            if (!collectionResult.Success)
            {
                return OperationResult<RecommendListResult>.Fail(
                    $"智能收集失败：{collectionResult.ErrorMessage}",
                    ErrorType.CollectionError,
                    "COLLECTION_FAILED");
            }

            // 4. 转换为推荐结果格式
            var recommendResult = ConvertToRecommendResult(collectionResult, navigationResult);

            var duration = DateTime.UtcNow - startTime;
            _logger.LogInformation("推荐获取完成，收集{Count}/{Target}条笔记，耗时{Duration}ms", 
                recommendResult.Notes.Count, limit, duration.TotalMilliseconds);

            return OperationResult<RecommendListResult>.Ok(recommendResult);
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow - startTime;
            _logger.LogError(ex, "获取推荐笔记时发生异常，耗时{Duration}ms", duration.TotalMilliseconds);
            return OperationResult<RecommendListResult>.Fail(
                $"获取推荐失败：{ex.Message}",
                ErrorType.Unknown,
                "GET_RECOMMENDATIONS_EXCEPTION");
        }
    }

    /// <summary>
    /// 验证浏览器连接
    /// </summary>
    private async Task<OperationResult<bool>> ValidateBrowserConnectionAsync()
    {
        try
        {
            var context = await _browserManager.GetBrowserContextAsync();
            if (context == null)
            {
                return OperationResult<bool>.Fail(
                    "浏览器上下文为空",
                    ErrorType.BrowserError,
                    "BROWSER_CONTEXT_NULL");
            }

            var pages = context.Pages;
            if (!pages.Any())
            {
                // 尝试创建新页面
                await context.NewPageAsync();
                _logger.LogDebug("创建新浏览器页面");
            }

            return OperationResult<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "验证浏览器连接时发生异常");
            return OperationResult<bool>.Fail(
                $"浏览器连接异常：{ex.Message}",
                ErrorType.BrowserError,
                "BROWSER_VALIDATION_EXCEPTION");
        }
    }

    /// <summary>
    /// 转换收集结果为推荐结果格式
    /// </summary>
    private RecommendListResult ConvertToRecommendResult(
        SmartCollectionResult collectionResult,
        DiscoverNavigationResult navigationResult)
    {
        // 直接使用 NoteInfo，不需要转换为 RecommendNote
        var notes = collectionResult.CollectedNotes;

        // 创建统计数据
        var statistics = new RecommendStatistics(
            VideoNotesCount: notes.Count(n => n.Type == NoteType.Video),
            ImageNotesCount: notes.Count(n => n.Type == NoteType.Image),
            AverageLikes: notes.Where(n => n.LikeCount.HasValue).Average(n => n.LikeCount ?? 0),
            AverageComments: notes.Where(n => n.CommentCount.HasValue).Average(n => n.CommentCount ?? 0),
            AverageCollects: notes.Where(n => n.FavoriteCount.HasValue).Average(n => n.FavoriteCount ?? 0),
            TopCategories: new Dictionary<string, int>(),
            AuthorDistribution: notes.GroupBy(n => n.Author).ToDictionary(g => g.Key, g => g.Count()),
            CalculatedAt: DateTime.UtcNow
        );

        // 创建收集详情
        var collectionDetails = new RecommendCollectionDetails(
            InterceptedRequests: collectionResult.RequestCount,
            SuccessfulRequests: collectionResult.PerformanceMetrics.SuccessfulRequests,
            FailedRequests: collectionResult.PerformanceMetrics.FailedRequests,
            ScrollOperations: collectionResult.PerformanceMetrics.ScrollCount,
            AverageScrollDelay: 1500, // 默认滚动延时
            DataQuality: DataQuality.Complete,
            CollectionMode: RecommendCollectionMode.Standard
        );

        // 使用 record 构造函数创建实例
        return new RecommendListResult(
            Notes: notes,
            TotalCollected: notes.Count,
            RequestCount: collectionResult.RequestCount,
            Duration: collectionResult.Duration,
            Statistics: statistics,
            ExportInfo: null,
            CollectionDetails: collectionDetails
        );
    }

    /// <inheritdoc />
    public async Task<ApiConnectionStatus> ValidateApiConnectionAsync()
    {
        var status = new ApiConnectionStatus();
        
        try
        {
            _logger.LogDebug("开始验证API连接状态");

            // 验证浏览器状态
            try
            {
                var context = await _browserManager.GetBrowserContextAsync();
                if (context != null)
                {
                    status.BrowserStatus = "已连接";
                    status.Details.Add("浏览器上下文正常");

                    var pages = context.Pages;
                    if (pages.Any())
                    {
                        var page = pages.First();
                        status.PageStatus = $"页面URL: {page.Url}";
                        
                        // 获取页面状态
                        var pageStatus = await _navigationService.GetCurrentPageStatusAsync(page);
                        status.Details.Add($"页面状态: {pageStatus.PageState}");
                        status.Details.Add($"发现页面: {pageStatus.IsOnDiscoverPage}");
                        status.Details.Add($"发现元素数量: {pageStatus.DiscoverElementsCount}");

                        // 验证API触发
                        status.ApiTriggered = await _navigationService.ValidateHomefeedApiTriggeredAsync(
                            page, TimeSpan.FromSeconds(5));

                        status.Details.Add($"API触发状态: {status.ApiTriggered}");
                    }
                    else
                    {
                        status.PageStatus = "无活动页面";
                        status.Details.Add("浏览器无活动页面");
                    }
                }
                else
                {
                    status.BrowserStatus = "未连接";
                    status.Details.Add("浏览器上下文为空");
                }
            }
            catch (Exception ex)
            {
                status.BrowserStatus = $"连接异常: {ex.Message}";
                status.Details.Add($"浏览器验证异常: {ex.Message}");
            }

            // 综合评估连接状态
            status.IsConnected = status.BrowserStatus == "已连接" && 
                                !string.IsNullOrEmpty(status.PageStatus) && 
                                !status.PageStatus.Contains("异常");

            _logger.LogDebug("API连接状态验证完成，连接状态：{IsConnected}", status.IsConnected);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "验证API连接状态时发生异常");
            status.IsConnected = false;
            status.BrowserStatus = "验证异常";
            status.Details.Add($"验证过程异常: {ex.Message}");
        }

        return status;
    }
}