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
                IsLoggedIn: result.Data?.IsLoggedIn ?? false,
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
    /// 搜索小红书笔记
    /// </summary>
    [McpServerTool]
    [Description("智能搜索小红书笔记，支持排序、类型、时间等多维筛选，返回搜索结果")]
    public static async Task<SearchResult> SearchNotes(
        [Description("搜索关键词")] string keyword,
        [Description("返回数量限制，默认10")] int limit = 10,
        [Description("排序方式：comprehensive(综合), latest(最新), most_liked(最多点赞), most_commented(最多评论), most_favorited(最多收藏)")]
        string sortBy = "comprehensive",
        [Description("笔记类型：all(不限), video(视频), image(图文)")] string noteType = "all",
        [Description("发布时间：all(不限), day(一天内), week(一周内), half_year(半年内)")] string publishTime = "all",
        [Description("搜索范围：all(不限), viewed(已看过), unviewed(未看过), followed(已关注)")]
        string searchScope = "all",
        [Description("位置距离：all(不限), same_city(同城), nearby(附近)")] string locationDistance = "all",
        IServiceProvider serviceProvider = null!)
    {
        try
        {
            var searchDataService = serviceProvider.GetRequiredService<ISearchDataService>();
            var request = new SearchRequest
            {
                Keyword = keyword,
                MaxResults = limit,
                SortBy = sortBy,
                NoteType = noteType,
                PublishTime = publishTime,
                SearchScope = searchScope,
                LocationDistance = locationDistance,
                AutoExport = true,
                ExportFileName = $"搜索结果_{keyword}_{DateTime.Now:yyyyMMdd_HHmmss}",
                ExportOptions = new ExportOptions()
            };

            var result = await searchDataService.SearchWithAnalyticsAsync(request);

            return result.Data ?? new SearchResult(
                new List<NoteInfo>(), 0, keyword, TimeSpan.Zero,
                new SearchStatistics(0, 0, 0, 0, 0, DateTime.UtcNow), null
            );
        }
        catch (Exception)
        {
            return new SearchResult(
                new List<NoteInfo>(), 0, keyword, TimeSpan.Zero,
                new SearchStatistics(0, 0, 0, 0, 0, DateTime.UtcNow), null
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
    public static async Task<EnhancedBatchNoteResult> BatchGetNoteDetailsOptimized(
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
        [Description("笔记类型：Image(图文), Video(视频), Article(长文)，默认为图文")] NoteType noteType = NoteType.Image,
        [Description("图片文件路径列表")] List<string>? imagePaths = null,
        [Description("视频文件路径")] string? videoPath = null,
        [Description("标签列表")] List<string>? tags = null,
        IServiceProvider serviceProvider = null!)
    {
        try
        {
            imagePaths ??= new List<string>();
            tags ??= new List<string>();

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
    /// 获取指定用户的完整个人资料数据
    /// </summary>
    [McpServerTool]
    [Description("获取指定用户的完整个人资料数据，包括粉丝数、关注数、获赞数、个人简介等详细信息")]
    public static async Task<UserProfileResult> GetUserCompleteProfile(
        [Description("用户ID，24-32位十六进制格式，例如：5f8a8c2b8b8a8c2b8b8a8c2b")] string userId,
        IServiceProvider serviceProvider = null!)
    {
        try
        {
            // 参数验证
            if (string.IsNullOrEmpty(userId))
            {
                return new UserProfileResult(
                    UserInfo: null,
                    Success: false,
                    Message: "❌ 用户ID不能为空",
                    ErrorCode: "INVALID_USER_ID"
                );
            }

            // 验证用户ID格式的有效性
            if (!IsValidUserId(userId))
            {
                return new UserProfileResult(
                    UserInfo: null,
                    Success: false,
                    Message: "❌ 用户ID格式无效，应为24-32位十六进制字符",
                    ErrorCode: "INVALID_USER_ID_FORMAT"
                );
            }

            var accountManager = serviceProvider.GetRequiredService<IAccountManager>();
            var result = await accountManager.GetCompleteUserProfileDataAsync(userId);

            if (!result.Success)
            {
                return new UserProfileResult(
                    UserInfo: null,
                    Success: false,
                    Message: result.ErrorMessage ?? "获取用户资料失败",
                    ErrorCode: result.ErrorCode
                );
            }

            // 成功获取用户资料
            return new UserProfileResult(
                UserInfo: result.Data,
                Success: true,
                Message: result.Data?.HasCompleteProfileData() == true
                    ? "✅ 成功获取用户完整资料"
                    : "⚠️ 成功获取用户资料，但部分数据可能缺失",
                ErrorCode: null
            );
        }
        catch (Exception ex)
        {
            return new UserProfileResult(
                UserInfo: null,
                Success: false,
                Message: $"获取用户资料异常: {ex.Message}",
                ErrorCode: "GET_USER_PROFILE_EXCEPTION"
            );
        }
    }

    /// <summary>
    /// 创建空的增强批量结果
    /// </summary>
    private static EnhancedBatchNoteResult CreateEmptyEnhancedBatchResult(List<string> keywords, int maxCount)
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

        return new EnhancedBatchNoteResult(
            NoteDetails: new List<NoteDetail>(),
            FailedNotes: new List<(string, string)> {(string.Join(", ", keywords), "批量获取失败")},
            TotalProcessed: 0,
            ProcessingTime: TimeSpan.Zero,
            OverallQuality: DataQuality.Minimal,
            Statistics: emptyStats,
            ExportInfo: null
        );
    }

    /// <summary>
    /// 创建错误的增强批量结果
    /// </summary>
    private static EnhancedBatchNoteResult CreateErrorEnhancedBatchResult(List<string> keywords, int maxCount, Exception ex)
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

        return new EnhancedBatchNoteResult(
            NoteDetails: new List<NoteDetail>(),
            FailedNotes: new List<(string, string)> {(string.Join(", ", keywords), $"批量获取异常: {ex.Message}")},
            TotalProcessed: 0,
            ProcessingTime: TimeSpan.Zero,
            OverallQuality: DataQuality.Minimal,
            Statistics: errorStats,
            ExportInfo: null
        );
    }

    /// <summary>
    /// 验证用户ID格式的有效性
    /// </summary>
    /// <param name="userId">待验证的用户ID</param>
    /// <returns>是否为有效的用户ID格式</returns>
    private static bool IsValidUserId(string userId)
    {
        if (string.IsNullOrEmpty(userId))
            return false;

        // 小红书用户ID通常特征：
        // 1. 长度在20-40字符之间
        // 2. 包含数字和字母（通常是十六进制格式）
        if (userId.Length is < 20 or > 40)
            return false;

        // 检查是否为有效的十六进制字符或字母数字组合
        return System.Text.RegularExpressions.Regex.IsMatch(userId, @"^[a-fA-F0-9]+$") ||
               System.Text.RegularExpressions.Regex.IsMatch(userId, @"^[a-zA-Z0-9]+$");
    }

    /// <summary>
    /// 验证笔记类型和文件路径的一致性
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
            
            NoteType.Article => hasImages || hasVideo
                ? (false, "❌ 长文笔记不能包含图片或视频文件")
                : (true, string.Empty),
            
            NoteType.Unknown => (false, "❌ 笔记类型不能为未知"),
            
            _ => (false, "❌ 不支持的笔记类型")
        };
    }

}
