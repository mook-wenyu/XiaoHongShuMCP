using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using XiaoHongShuMCP.Services;

namespace XiaoHongShuMCP.Tools;

/// <summary>
/// 小红书 MCP 工具集
/// </summary>
[McpServerToolType]
public static class XiaoHongShuTools
{
    /// <summary>
    /// 连接到浏览器并验证登录状态
    /// </summary>
    [McpServerTool]
    [Description("连接到浏览器并验证小红书登录状态，返回连接结果和登录状态")]
    public static async Task<BrowserConnectionResult> ConnectToBrowser(IServiceProvider serviceProvider)
    {
        try
        {
            var accountManager = serviceProvider.GetRequiredService<IAccountManager>();
            var result = await accountManager.ConnectToBrowserAsync();

            if (!result.Success)
            {
                return new BrowserConnectionResult(
                    IsConnected: false,
                    IsLoggedIn: false,
                    Message: result.ErrorMessage ?? "连接失败",
                    ErrorCode: result.ErrorCode
                );
            }

            return new BrowserConnectionResult(
                IsConnected: true,
                IsLoggedIn: result.Data,
                Message: "浏览器连接成功"
            );
        }
        catch (Exception ex)
        {
            return new BrowserConnectionResult(
                IsConnected: false,
                IsLoggedIn: false,
                Message: $"连接异常: {ex.Message}",
                ErrorCode: "CONNECTION_EXCEPTION"
            );
        }
    }

    /// <summary>
    /// 获取推荐笔记
    /// </summary>
    [McpServerTool]
    [Description("获取小红书推荐笔记")]
    public static async Task<RecommendResultMcp> GetRecommendedNotes(
        [Description("获取数量限制，默认20")] int limit = 20,
        [Description("超时时间（分钟），默认5分钟")] int timeoutMinutes = 5,
        IServiceProvider serviceProvider = null!)
    {
        try
        {
            var xiaohongShuService = serviceProvider.GetRequiredService<IXiaoHongShuService>();
            var timeout = TimeSpan.FromMinutes(timeoutMinutes);

            var result = await xiaohongShuService.GetRecommendedNotesAsync(limit, timeout);

            var successMessage = result.Success
                ? $"✅ 获取推荐笔记成功，共{result.Data?.Notes?.Count ?? 0}条"
                : "❌ 获取推荐笔记失败";

            return new RecommendResultMcp(
                Recommendations: result.Data,
                Success: result.Success,
                Message: result.Success ? successMessage : (result.ErrorMessage ?? "获取失败"),
                ErrorCode: result.ErrorCode,
                ValidationDetails: new Dictionary<string, object>()
            );
        }
        catch (Exception ex)
        {
            return new RecommendResultMcp(
                Recommendations: null,
                Success: false,
                Message: $"获取推荐笔记异常: {ex.Message}",
                ErrorCode: "GET_RECOMMENDATIONS_EXCEPTION",
                ValidationDetails: new Dictionary<string, object>
                {
                    ["异常类型"] = ex.GetType().Name,
                    ["异常消息"] = ex.Message,
                    ["发生时间"] = DateTime.UtcNow
                }
            );
        }
    }

    /// <summary>
    /// 获取小红书搜索笔记 - 拟人化操作与API监听结合
    /// </summary>
    [McpServerTool]
    [Description("获取小红书搜索笔记")]
    public static async Task<SearchResultMcp> GetSearchNotes(
        [Description("搜索关键词")] string keyword,
        [Description("最大结果数量，默认20，建议10-50之间")] int maxResults = 20,
        [Description("排序方式：comprehensive(综合), latest(最新), most_liked(最多点赞)")] string sortBy = "comprehensive",
        [Description("笔记类型：all(不限), video(视频), image(图文)")] string noteType = "all",
        [Description("发布时间：all(不限), day(一天内), week(一周内), half_year(半年内)")] string publishTime = "all",
        [Description("是否包含统计分析，默认true")] bool includeAnalytics = true,
        [Description("是否自动导出Excel，默认true")] bool autoExport = true,
        [Description("导出文件名（可选），不指定则自动生成")] string? exportFileName = null,
        IServiceProvider serviceProvider = null!)
    {
        try
        {
            var xiaoHongShuService = serviceProvider.GetRequiredService<IXiaoHongShuService>();
            var result = await xiaoHongShuService.SearchNotesAsync(
                keyword,
                maxResults,
                sortBy,
                noteType,
                publishTime,
                includeAnalytics,
                autoExport,
                exportFileName);

            var successMessage = result.Success
                ? $"✅ 获取搜索笔记成功：关键词='{keyword}'，收集={result.Data?.TotalCount ?? 0}条，API请求={result.Data?.ApiRequests ?? 0}次"
                : "❌ 获取搜索笔记失败";

            return new SearchResultMcp(
                SearchResult: result.Data,
                Success: result.Success,
                Message: result.Success ? successMessage : (result.ErrorMessage ?? "搜索失败"),
                ErrorCode: result.ErrorCode,
                Performance: result.Data != null ? new Dictionary<string, object>
                {
                    ["搜索关键词"] = result.Data.SearchKeyword,
                    ["收集数量"] = result.Data.TotalCount,
                    ["处理耗时"] = $"{result.Data.Duration.TotalMilliseconds:F0}ms",
                    ["API请求次数"] = result.Data.ApiRequests,
                    ["监听响应数"] = result.Data.InterceptedResponses,
                    ["数据完整性"] = result.Data.Statistics.CompleteDataCount > 0 ? "完整" :
                        result.Data.Statistics.PartialDataCount > 0 ? "部分" : "基础"
                } : new Dictionary<string, object>()
            );
        }
        catch (Exception ex)
        {
            return new SearchResultMcp(
                SearchResult: null,
                Success: false,
                Message: $"搜索笔记异常: {ex.Message}",
                ErrorCode: "SEARCH_NOTES_ENHANCED_EXCEPTION",
                Performance: new Dictionary<string, object>
                {
                    ["异常类型"] = ex.GetType().Name,
                    ["异常消息"] = ex.Message,
                    ["发生时间"] = DateTime.UtcNow
                }
            );
        }
    }

    /// <summary>
    /// 获取笔记详情
    /// </summary>
    [McpServerTool]
    [Description("基于关键词列表智能定位并获取笔记详情，匹配任意关键词即可")]
    public static async Task<NoteDetailResult> GetNoteDetail(
        [Description("关键词列表，用于在当前页面查找匹配的笔记，匹配任意一个关键词即可")] List<string> keywords,
        [Description("是否包含评论信息，默认false")] bool includeComments = false,
        IServiceProvider serviceProvider = null!)
    {
        try
        {
            var xiaoHongShuService = serviceProvider.GetRequiredService<IXiaoHongShuService>();
            var result = await xiaoHongShuService.GetNoteDetailAsync(keywords, includeComments);

            var successMessage = result.Success
                ? "✅ 基于关键词列表获取笔记详情成功"
                : "❌ 基于关键词列表获取笔记详情失败";

            return new NoteDetailResult(
                Detail: result.Data,
                Success: result.Success,
                Message: result.Success ? successMessage : (result.ErrorMessage ?? "获取失败"),
                ErrorCode: result.ErrorCode
            );
        }
        catch (Exception ex)
        {
            return new NoteDetailResult(
                Detail: null,
                Success: false,
                Message: $"获取笔记详情异常: {ex.Message}",
                ErrorCode: "GET_NOTE_EXCEPTION"
            );
        }
    }

    /// <summary>
    /// 批量获取笔记详情
    /// </summary>
    [McpServerTool]
    [Description("基于关键词列表批量获取笔记详情，集成统计分析和自动导出功能。")]
    public static async Task<BatchNoteResult> BatchGetNoteDetailsOptimized(
        [Description("关键词列表，用于在当前页面查找匹配的笔记，匹配任意关键词即可。系统会智能滚动搜索更多内容")] List<string> keywords,
        [Description("最大获取数量，默认10")] int maxCount = 10,
        [Description("是否包含评论信息，默认false")] bool includeComments = false,
        [Description("是否自动导出Excel，默认true")] bool autoExport = true,
        [Description("导出文件名（可选），不指定则自动生成")] string? exportFileName = null,
        IServiceProvider serviceProvider = null!)
    {
        try
        {
            var xiaoHongShuService = serviceProvider.GetRequiredService<IXiaoHongShuService>();
            var result = await xiaoHongShuService.BatchGetNoteDetailsAsync(
                keywords,
                maxCount,
                includeComments,
                autoExport,
                exportFileName);

            return result.Data ?? CreateEmptyEnhancedBatchResult(keywords, maxCount);
        }
        catch (Exception ex)
        {
            return CreateErrorEnhancedBatchResult(keywords, maxCount, ex);
        }
    }

    /// <summary>
    /// 点赞笔记
    /// </summary>
    [McpServerTool]
    [Description("基于关键词列表定位并点赞笔记")]
    public static async Task<InteractionResult> LikeNote(
        [Description("关键词列表，用于在当前页面查找匹配的笔记，匹配任意一个关键词即可")] List<string> keywords,
        [Description("是否强制执行，即使已经点赞")] bool forceAction = false,
        IServiceProvider serviceProvider = null!)
    {
        try
        {
            var xiaoHongShuService = serviceProvider.GetRequiredService<IXiaoHongShuService>();
            var result = await xiaoHongShuService.LikeNoteAsync(keywords, forceAction);

            return result.Data ?? new InteractionResult(
                Success: false,
                Action: "点赞",
                PreviousState: "未知",
                CurrentState: "未知",
                Message: result.ErrorMessage ?? "点赞失败",
                ErrorCode: result.ErrorCode
            );
        }
        catch (Exception ex)
        {
            return new InteractionResult(
                Success: false,
                Action: "点赞",
                PreviousState: "未知",
                CurrentState: "未知",
                Message: $"点赞操作异常: {ex.Message}",
                ErrorCode: "LIKE_NOTE_EXCEPTION"
            );
        }
    }

    /// <summary>
    /// 收藏笔记
    /// </summary>
    [McpServerTool]
    [Description("基于关键词列表定位并收藏笔记")]
    public static async Task<InteractionResult> FavoriteNote(
        [Description("关键词列表，用于在当前页面查找匹配的笔记，匹配任意一个关键词即可")] List<string> keywords,
        [Description("是否强制执行，即使已经收藏")] bool forceAction = false,
        IServiceProvider serviceProvider = null!)
    {
        try
        {
            var xiaoHongShuService = serviceProvider.GetRequiredService<IXiaoHongShuService>();
            var result = await xiaoHongShuService.FavoriteNoteAsync(keywords, forceAction);

            return result.Data ?? new InteractionResult(
                Success: false,
                Action: "收藏",
                PreviousState: "未知",
                CurrentState: "未知",
                Message: result.ErrorMessage ?? "收藏失败",
                ErrorCode: result.ErrorCode
            );
        }
        catch (Exception ex)
        {
            return new InteractionResult(
                Success: false,
                Action: "收藏",
                PreviousState: "未知",
                CurrentState: "未知",
                Message: $"收藏操作异常: {ex.Message}",
                ErrorCode: "FAVORITE_NOTE_EXCEPTION"
            );
        }
    }

    /// <summary>
    /// 发布评论
    /// </summary>
    [McpServerTool]
    [Description("基于关键词列表定位笔记并发布评论")]
    public static async Task<CommentResult> PostComment(
        [Description("关键词列表，用于在当前页面查找匹配的笔记，匹配任意一个关键词即可")] List<string> keywords,
        [Description("评论内容")] string content,
        IServiceProvider serviceProvider = null!)
    {
        try
        {
            var xiaoHongShuService = serviceProvider.GetRequiredService<IXiaoHongShuService>();
            var result = await xiaoHongShuService.PostCommentAsync(keywords, content);

            return result.Data ?? new CommentResult(
                Success: false,
                Message: result.ErrorMessage ?? "发布失败",
                CommentId: string.Empty,
                ErrorCode: result.ErrorCode
            );
        }
        catch (Exception ex)
        {
            return new CommentResult(
                Success: false,
                Message: $"发布评论异常: {ex.Message}",
                CommentId: string.Empty,
                ErrorCode: "POST_COMMENT_EXCEPTION"
            );
        }
    }

    /// <summary>
    /// 保存笔记为草稿
    /// </summary>
    [McpServerTool]
    [Description("保存笔记内容为草稿，支持文本、图片、视频和标签。图文和视频类型互斥：有图片不能有视频，有视频不能有图片")]
    public static async Task<DraftSaveResult> SaveContentDraft(
        [Description("笔记标题")] string title,
        [Description("笔记内容")] string content,
        [Description("笔记类型：Image(图文), Video(视频)，默认为图文。注意：长文内容请使用图文类型")] NoteType noteType = NoteType.Image,
        [Description("图片文件路径列表")] List<string>? imagePaths = null,
        [Description("视频文件路径")] string? videoPath = null,
        [Description("标签列表")] List<string>? tags = null,
        IServiceProvider serviceProvider = null!)
    {
        try
        {
            imagePaths ??= [];
            tags ??= [];

            // 参数验证：检查笔记类型和文件路径的一致性
            var validationResult = ValidateNoteTypeConsistency(noteType, imagePaths, videoPath);
            if (!validationResult.IsValid)
            {
                return new DraftSaveResult(
                    Success: false,
                    Message: validationResult.ErrorMessage,
                    ErrorCode: "INVALID_NOTE_TYPE_COMBINATION"
                );
            }

            var xiaoHongShuService = serviceProvider.GetRequiredService<IXiaoHongShuService>();
            var result = await xiaoHongShuService.TemporarySaveAndLeaveAsync(title, content, noteType, imagePaths, videoPath, tags);

            return result.Data ?? new DraftSaveResult(
                Success: false,
                Message: result.ErrorMessage ?? "保存失败",
                ErrorCode: result.ErrorCode
            );
        }
        catch (Exception ex)
        {
            return new DraftSaveResult(
                Success: false,
                Message: $"保存草稿异常: {ex.Message}",
                ErrorCode: "SAVE_DRAFT_EXCEPTION"
            );
        }
    }

    /// <summary>
    /// 创建空的增强批量结果
    /// </summary>
    private static BatchNoteResult CreateEmptyEnhancedBatchResult(List<string> keywords, int maxCount)
    {
        var emptyStats = new BatchProcessingStatistics(
            CompleteDataCount: 0,
            PartialDataCount: 0,
            MinimalDataCount: 0,
            AverageProcessingTime: 0,
            AverageLikes: 0,
            AverageComments: 0,
            TypeDistribution: new Dictionary<NoteType, int>(),
            ProcessingModeStats: new Dictionary<ProcessingMode, int>
            {
                [ProcessingMode.Fast] = 0,
                [ProcessingMode.Standard] = 0,
                [ProcessingMode.Careful] = 0
            },
            CalculatedAt: DateTime.UtcNow
        );

        return new BatchNoteResult(
            SuccessfulNotes: [],
            FailedNotes: [(string.Join(", ", keywords), "批量获取失败")],
            ProcessedCount: 0,
            ProcessingTime: TimeSpan.Zero,
            OverallQuality: DataQuality.Minimal,
            Statistics: emptyStats,
            ExportInfo: null
        );
    }

    /// <summary>
    /// 创建错误的增强批量结果
    /// </summary>
    private static BatchNoteResult CreateErrorEnhancedBatchResult(List<string> keywords, int maxCount, Exception ex)
    {
        var errorStats = new BatchProcessingStatistics(
            CompleteDataCount: 0,
            PartialDataCount: 0,
            MinimalDataCount: 0,
            AverageProcessingTime: 0,
            AverageLikes: 0,
            AverageComments: 0,
            TypeDistribution: new Dictionary<NoteType, int>(),
            ProcessingModeStats: new Dictionary<ProcessingMode, int>
            {
                [ProcessingMode.Fast] = 0,
                [ProcessingMode.Standard] = 0,
                [ProcessingMode.Careful] = 0
            },
            CalculatedAt: DateTime.UtcNow
        );

        return new BatchNoteResult(
            SuccessfulNotes: [],
            FailedNotes: [(string.Join(", ", keywords), $"批量获取异常: {ex.Message}")],
            ProcessedCount: 0,
            ProcessingTime: TimeSpan.Zero,
            OverallQuality: DataQuality.Minimal,
            Statistics: errorStats,
            ExportInfo: null
        );
    }

    /// <summary>
    /// 验证笔记类型和文件路径的一致性
    /// 简化验证逻辑，只支持Image和Video两种主要类型
    /// </summary>
    /// <param name="noteType">笔记类型</param>
    /// <param name="imagePaths">图片路径列表</param>
    /// <param name="videoPath">视频路径</param>
    /// <returns>验证结果</returns>
    private static (bool IsValid, string ErrorMessage) ValidateNoteTypeConsistency(
        NoteType noteType, List<string> imagePaths, string? videoPath)
    {
        bool hasImages = imagePaths.Count > 0;
        bool hasVideo = !string.IsNullOrEmpty(videoPath);

        return noteType switch
        {
            NoteType.Image => hasVideo
                ? (false, "❌ 图文笔记不能包含视频文件")
                : (true, string.Empty),

            NoteType.Video => hasImages
                ? (false, "❌ 视频笔记不能包含图片文件")
                : !hasVideo
                    ? (false, "❌ 视频笔记必须包含视频文件")
                    : (true, string.Empty),

            NoteType.Unknown => (false, "❌ 笔记类型不能为未知"),

            _ => (false, "❌ 不支持的笔记类型")
        };
    }
    
}

/// <summary>
/// 推荐结果MCP返回值 - 统一的MCP工具返回结果
/// </summary>
/// <param name="Recommendations">推荐数据</param>
/// <param name="Success">是否成功</param>
/// <param name="Message">结果消息</param>
/// <param name="ErrorCode">错误代码</param>
/// <param name="ValidationDetails">验证详情</param>
public record RecommendResultMcp(
    RecommendListResult? Recommendations,
    bool Success,
    string Message,
    string? ErrorCode = null,
    Dictionary<string, object>? ValidationDetails = null
);
/// <summary>
/// 搜索结果MCP返回值 - 统一的MCP工具返回结果
/// 为SearchNotesEnhanced工具设计
/// </summary>
/// <param name="SearchResult">搜索结果数据</param>
/// <param name="Success">是否成功</param>
/// <param name="Message">结果消息</param>
/// <param name="ErrorCode">错误代码</param>
/// <param name="Performance">性能指标和详情</param>
public record SearchResultMcp(
    SearchResult? SearchResult,
    bool Success,
    string Message,
    string? ErrorCode = null,
    Dictionary<string, object>? Performance = null
);
