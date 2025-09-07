using Microsoft.Extensions.Logging;

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

            // 3. 执行智能收集
            var collectionMode = limit <= 10 
                ? RecommendCollectionMode.Fast 
                : limit <= 50 
                    ? RecommendCollectionMode.Standard 
                    : RecommendCollectionMode.Careful;

            var collectionResult = await _collectionController.ExecuteSmartCollectionAsync(
                context, page,
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
    /// 转换收集结果为推荐结果格式
    /// </summary>
    private RecommendListResult ConvertToRecommendResult(
        SmartCollectionResult collectionResult,
        DiscoverNavigationResult navigationResult)
    {
        // 直接使用 NoteInfo，不需要转换为 RecommendNote
        var notes = collectionResult.CollectedNotes;
        
        _logger.LogDebug("开始转换收集结果，笔记总数：{Count}", notes.Count);

        // 创建统计数据，使用安全的平均值计算
        var notesWithLikes = notes.Where(n => n.LikeCount.HasValue && n.LikeCount > 0).ToList();
        var notesWithComments = notes.Where(n => n.CommentCount.HasValue && n.CommentCount >= 0).ToList();
        var notesWithCollects = notes.Where(n => n.FavoriteCount.HasValue && n.FavoriteCount >= 0).ToList();

        _logger.LogDebug("统计数据过滤结果 - 有点赞数据：{LikeCount}，有评论数据：{CommentCount}，有收藏数据：{CollectCount}", 
            notesWithLikes.Count, notesWithComments.Count, notesWithCollects.Count);

        // 安全计算平均值，避免空序列异常
        var avgLikes = notesWithLikes.Count > 0 ? notesWithLikes.Average(n => n.LikeCount ?? 0) : 0;
        var avgComments = notesWithComments.Count > 0 ? notesWithComments.Average(n => n.CommentCount ?? 0) : 0;
        var avgCollects = notesWithCollects.Count > 0 ? notesWithCollects.Average(n => n.FavoriteCount ?? 0) : 0;

        var statistics = new RecommendStatistics(
            VideoNotesCount: notes.Count(n => n.Type == NoteType.Video),
            ImageNotesCount: notes.Count(n => n.Type == NoteType.Image),
            AverageLikes: avgLikes,
            AverageComments: avgComments,
            AverageCollects: avgCollects,
            TopCategories: new Dictionary<string, int>(),
            AuthorDistribution: notes.Count != 0 ? 
                notes.Where(n => !string.IsNullOrEmpty(n.Author))
                     .GroupBy(n => n.Author)
                     .ToDictionary(g => g.Key, g => g.Count()) : 
                new Dictionary<string, int>(),
            CalculatedAt: DateTime.UtcNow
        );

        _logger.LogDebug("统计数据计算完成 - 平均点赞：{AvgLikes:F1}，平均评论：{AvgComments:F1}，平均收藏：{AvgCollects:F1}", 
            avgLikes, avgComments, avgCollects);

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
}