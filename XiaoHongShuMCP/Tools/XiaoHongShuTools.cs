using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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
    [Description("连接到浏览器并验证小红书登录状态；返回连接与登录状态 | Connects to the browser and verifies Xiaohongshu login; returns connection and login status")]
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
    [Description("获取推荐流笔记（数量与超时可配） | Fetch feed recommendations (configurable limit and timeout)")]
    public static async Task<RecommendResultMcp> GetRecommendedNotes(
        [Description("获取数量上限（默认20）| Max items to fetch (default 20)")] int limit = 20,
        [Description("超时时间（分钟，默认10）| Timeout in minutes (default 10)")] int timeoutMinutes = 10,
        IServiceProvider serviceProvider = null!)
    {
        try
        {
            var xiaohongShuService = serviceProvider.GetRequiredService<IXiaoHongShuService>();
            var xhs = serviceProvider.GetService<IOptions<XhsSettings>>();
            // 统一策略：CLI 明确给定则优先使用；否则回退 McpSettings；再否则回退 10 分钟
            var cfgMs = xhs?.Value?.McpSettings?.WaitTimeoutMs ?? 600_000;
            var cliMs = (int)TimeSpan.FromMinutes(timeoutMinutes).TotalMilliseconds;
            var effectiveMs = cliMs > 0 ? cliMs : (cfgMs > 0 ? cfgMs : 600_000);
            var timeout = TimeSpan.FromMilliseconds(effectiveMs);

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
    [Description("按关键词搜索笔记；支持排序/类型/时间筛选与统计/导出 | Search notes by keyword; supports sort/type/time filters plus analytics/export")]
    public static async Task<SearchResultMcp> GetSearchNotes(
        [Description("搜索关键词 | Search keyword")] string keyword,
        [Description("最大结果数量（默认20，建议10–50）| Maximum results (default 20; 10–50 recommended)")]
        int maxResults = 20,
        [Description("排序：comprehensive(综合), latest(最新), most_liked(最多点赞) | Sort: comprehensive, latest, most_liked")]
        string sortBy = "comprehensive",
        [Description("笔记类型：all(不限), video(视频), image(图文) | Note type: all, video, image")]
        string noteType = "all",
        [Description("发布时间：all(不限), day(一天内), week(一周内), half_year(半年内) | Publish time: all, day, week, half_year")]
        string publishTime = "all",
        [Description("是否包含统计分析（默认true）| Include analytics (default true)")] bool includeAnalytics = true,
        [Description("是否自动导出Excel（默认true）| Auto export to Excel (default true)")]
        bool autoExport = true,
        [Description("导出文件名（可选）；未指定则自动生成 | Optional export filename; autogenerated if omitted")]
        string? exportFileName = null,
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
    /// 关闭笔记详情弹窗（工具版）：尝试点击遮罩→按 ESC→点击关闭按钮。
    /// </summary>
    [McpServerTool]
    [Description("关闭当前的笔记详情弹窗（尝试：点击遮罩→按ESC→点击关闭按钮） | Close note detail modal by clicking mask, pressing ESC, or clicking close buttons")]
    public static async Task<InteractionResult> CloseNoteDetail(
        IServiceProvider serviceProvider = null!)
    {
        try
        {
            var browser = serviceProvider.GetRequiredService<IBrowserManager>();
            var dom = serviceProvider.GetRequiredService<IDomElementManager>();
            var human = serviceProvider.GetRequiredService<IHumanizedInteractionService>();
            var wait = serviceProvider.GetRequiredService<IPageLoadWaitService>();
            var page = await browser.GetPageAsync();

            async Task<bool> IsDetailOpen()
            {
                try
                {
                    // URL 快速判断 + DOM 遮罩检测
                    var url = page.Url ?? string.Empty;
                    if (url.Contains("/explore/item") || url.Contains("/explore/")) return true;
                    var masks = dom.GetSelectors("NoteDetailModal");
                    foreach (var sel in masks)
                    {
                        try
                        {
                            var el = await page.QuerySelectorAsync(sel);
                            if (el != null && await el.IsVisibleAsync()) return true;
                        }
                        catch { }
                    }
                    return false;
                }
                catch { return false; }
            }

            if (!await IsDetailOpen())
            {
                return new InteractionResult(true, "关闭详情", "非详情", "非详情", "当前不在详情页，无需关闭");
            }

            // 1) 点击遮罩
            try
            {
                var masks = dom.GetSelectors("NoteDetailModal");
                foreach (var sel in masks)
                {
                    var mask = await page.QuerySelectorAsync(sel);
                    if (mask == null) continue;
                    if (!await mask.IsVisibleAsync()) continue;
                    await human.HumanClickAsync(mask);
                    await wait.WaitForPageLoadAsync(page);
                    if (!await IsDetailOpen())
                    {
                        return new InteractionResult(true, "关闭详情", "详情", "非详情", "已通过点击遮罩关闭");
                    }
                }
            }
            catch { /* 忽略进入下一策略 */ }

            // 2) 按 ESC
            try
            {
                await page.Keyboard.PressAsync("Escape");
                await wait.WaitForPageLoadAsync(page);
                if (!await IsDetailOpen())
                {
                    return new InteractionResult(true, "关闭详情", "详情", "非详情", "已通过 ESC 关闭");
                }
            }
            catch { /* 忽略进入下一策略 */ }

            // 3) 点击关闭按钮（详情专用 & 通用）
            try
            {
                var selectors = new List<string>();
                selectors.AddRange(dom.GetSelectors("NoteDetailCloseButton"));
                selectors.AddRange(dom.GetSelectors("CloseButton"));
                foreach (var sel in selectors.Distinct())
                {
                    var btn = await page.QuerySelectorAsync(sel);
                    if (btn == null) continue;
                    if (!await btn.IsEnabledAsync()) continue;
                    await human.HumanClickAsync(btn);
                    await wait.WaitForPageLoadAsync(page);
                    if (!await IsDetailOpen())
                    {
                        return new InteractionResult(true, "关闭详情", "详情", "非详情", "已通过关闭按钮关闭");
                    }
                }
            }
            catch { }

            // 仍未关闭
            return new InteractionResult(false, "关闭详情", "详情", "详情", "未能关闭详情弹窗", "DETAIL_CLOSE_FAILED");
        }
        catch (Exception ex)
        {
            return new InteractionResult(false, "关闭详情", "未知", "未知", $"关闭详情异常: {ex.Message}", "DETAIL_CLOSE_EXCEPTION");
        }
    }

    /// <summary>
    /// 查看并获取笔记详情
    /// </summary>
    [McpServerTool]
    [Description("基于关键词在当前页定位并打开笔记，返回详情 | Locate and open a note on the current page by keyword")]
    public static async Task<NoteDetailResult> GetNoteDetail(
        [Description("关键词；在当前页面进行匹配 | Keyword; match on the current page")] string keyword,
        [Description("是否包含评论（默认false）| Include comments (default false)")] bool includeComments = false,
        IServiceProvider serviceProvider = null!)
    {
        try
        {
            var xiaoHongShuService = serviceProvider.GetRequiredService<IXiaoHongShuService>();
            var result = await xiaoHongShuService.GetNoteDetailAsync(keyword, includeComments);

            var successMessage = result.Success
                ? "✅ 基于关键词获取笔记详情成功"
                : "❌ 基于关键词获取笔记详情失败";

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
    /// 批量查看并获取笔记详情
    /// </summary>
    [McpServerTool]
    [Description("批量定位并打开多条笔记，自动滚动加载并可导出 | Batch open notes by keyword with auto-scroll; optional export")]
    public static async Task<BatchNoteResult> BatchGetNoteDetails(
        [Description("关键词；支持自动滚动加载更多 | Keyword; auto-scroll to load more")] string keyword,
        [Description("最大获取数量（默认10）| Max notes to fetch (default 10)")] int maxCount = 10,
        [Description("是否包含评论（默认false）| Include comments (default false)")] bool includeComments = false,
        [Description("是否自动导出Excel（默认true）| Auto export to Excel (default true)")]
        bool autoExport = true,
        [Description("导出文件名（可选）；未指定则自动生成 | Optional export filename; autogenerated if omitted")]
        string? exportFileName = null,
        IServiceProvider serviceProvider = null!)
    {
        try
        {
            var xiaoHongShuService = serviceProvider.GetRequiredService<IXiaoHongShuService>();
            var result = await xiaoHongShuService.BatchGetNoteDetailsAsync(
                keyword,
                maxCount,
                includeComments,
                autoExport,
                exportFileName);

            return result.Data ?? CreateEmptyEnhancedBatchResult(keyword, maxCount);
        }
        catch (Exception ex)
        {
            return CreateErrorEnhancedBatchResult(keyword, maxCount, ex);
        }
    }

    // 兼容包装工具 LikeNote/FavoriteNote 已删除：请使用统一的 InteractNote(keyword, like, favorite)

    /// <summary>
    /// 统一交互：定位匹配的笔记，并按需执行点赞/收藏，可组合。
    /// 新行为：若启动前已打开详情弹窗，将先关闭再执行，以确保 API 监听可靠。
    /// </summary>
    [McpServerTool]
    [Description("定位匹配笔记并执行点赞/收藏（可组合） | Interact with a matched note: like and/or favorite")]
    public static async Task<InteractionBundleResult> InteractNote(
        [Description("关键词；在当前页面进行匹配 | Keyword; match on the current page")] string keyword,
        [Description("是否点赞 | Whether to like")] bool like,
        [Description("是否收藏 | Whether to favorite")] bool favorite,
        IServiceProvider serviceProvider = null!)
    {
        try
        {
            var xiaoHongShuService = serviceProvider.GetRequiredService<IXiaoHongShuService>();
            var result = await xiaoHongShuService.InteractNoteAsync(keyword, like, favorite);

            return result.Data ?? new InteractionBundleResult(
                Success: false,
                Like: null,
                Favorite: null,
                Message: result.ErrorMessage ?? "交互失败",
                ErrorCode: result.ErrorCode
            );
        }
        catch (Exception ex)
        {
            return new InteractionBundleResult(
                Success: false,
                Like: null,
                Favorite: null,
                Message: $"交互异常: {ex.Message}",
                ErrorCode: "INTERACT_NOTE_EXCEPTION"
            );
        }
    }

    /// <summary>
    /// 发布评论
    /// </summary>
    [McpServerTool]
    [Description("定位匹配的笔记并发布评论 | Locate a matched note by keyword and post a comment")]
    public static async Task<CommentResult> PostComment(
        [Description("关键词；在当前页面进行匹配 | Keyword; match on the current page")] string keyword,
        [Description("评论内容 | Comment text")] string content,
        IServiceProvider serviceProvider = null!)
    {
        try
        {
            var xiaoHongShuService = serviceProvider.GetRequiredService<IXiaoHongShuService>();
            var result = await xiaoHongShuService.PostCommentAsync(keyword, content);

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
    [Description("保存笔记为草稿（图文或视频），不发布 | Save content as a draft (image/text or video) without publishing")]
    public static async Task<DraftSaveResult> SaveContentDraft(
        [Description("笔记标题 | Note title")] string title,
        [Description("笔记内容 | Note body/content")] string content,
        [Description("笔记类型：Image(图文)/Video(视频)；默认Image。长文建议用Image | Note type: Image (text+images) or Video; default Image")]
        NoteType noteType = NoteType.Image,
        [Description("图片文件路径列表 | Image file paths")] List<string>? imagePaths = null,
        [Description("视频文件路径 | Video file path")] string? videoPath = null,
        [Description("标签列表 | Tags")] List<string>? tags = null,
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
    private static BatchNoteResult CreateEmptyEnhancedBatchResult(string keyword, int maxCount)
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
            FailedNotes: [(keyword, "批量获取失败")],
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
    private static BatchNoteResult CreateErrorEnhancedBatchResult(string keyword, int maxCount, Exception ex)
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
            FailedNotes: [(keyword, $"批量获取异常: {ex.Message}")],
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
