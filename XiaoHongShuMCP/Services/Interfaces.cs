using Microsoft.Playwright;

namespace XiaoHongShuMCP.Services;

/// <summary>
/// 小红书核心服务接口 - 重构版 v3.0
/// 简化接口，移除多余参数，基于关键词的统一查找模式
/// </summary>
public interface IXiaoHongShuService
{
    /// <summary>
    /// 查找单个笔记详情（基于关键词列表）
    /// 通过关键词列表搜索并定位到第一个匹配的笔记，获取其详细信息
    /// 使用统一的LocateAndOperateNoteAsync架构实现
    /// </summary>
    /// <param name="keywords">搜索关键词列表（必需）</param>
    /// <param name="includeComments">是否包含评论数据</param>
    /// <returns>笔记详情操作结果</returns>
    Task<OperationResult<NoteDetail>> GetNoteDetailAsync(
        List<string> keywords,
        bool includeComments = false);

    /// <summary>
    /// 批量查找笔记详情（重构版） - 三位一体功能
    /// 基于简单关键词列表批量获取笔记详情，并集成统计分析和异步导出功能
    /// 参考 SearchDataService 的设计模式，提供同步统计计算和异步导出
    /// </summary>
    /// <param name="keywords">关键词列表（简化参数）</param>
    /// <param name="maxCount">最大查找数量</param>
    /// <param name="includeComments">是否包含评论数据</param>
    /// <param name="autoExport">是否自动导出到Excel</param>
    /// <param name="exportFileName">导出文件名（可选）</param>
    /// <returns>增强的批量笔记结果，包含统计分析和导出信息</returns>
    Task<OperationResult<EnhancedBatchNoteResult>> BatchGetNoteDetailsAsync(
        List<string> keywords,
        int maxCount = 10,
        bool includeComments = false,
        bool autoExport = true,
        string? exportFileName = null);

    /// <summary>
    /// 基于关键词列表发布评论
    /// 使用新的统一架构，通过关键词列表定位笔记并在详情页发布评论
    /// </summary>
    /// <param name="keywords">搜索关键词列表（匹配任意关键词）</param>
    /// <param name="content">评论内容</param>
    /// <returns>评论操作结果</returns>
    Task<OperationResult<CommentResult>> PostCommentAsync(
        List<string> keywords,
        string content);

    /// <summary>
    /// 暂存笔记并离开编辑页面
    /// 将内容保存为草稿，确保用户完全控制发布时机
    /// </summary>
    /// <param name="title">笔记标题</param>
    /// <param name="content">笔记内容</param>
    /// <param name="noteType">笔记类型</param>
    /// <param name="imagePaths">图片路径列表</param>
    /// <param name="videoPath">视频路径（可选）</param>
    /// <param name="tags">标签列表</param>
    /// <returns>草稿保存操作结果</returns>
    Task<OperationResult<DraftSaveResult>> TemporarySaveAndLeaveAsync(
        string title,
        string content,
        NoteType noteType,
        List<string>? imagePaths,
        string? videoPath,
        List<string>? tags);

    /// <summary>
    /// 基于关键词列表定位并点赞笔记
    /// </summary>
    /// <param name="keywords">关键词列表，匹配任意一个即可</param>
    /// <param name="forceAction">是否强制执行，即使已经点赞</param>
    /// <returns>点赞操作结果</returns>
    Task<OperationResult<InteractionResult>> LikeNoteAsync(
        List<string> keywords,
        bool forceAction = false);

    /// <summary>
    /// 基于关键词列表定位并收藏笔记
    /// </summary>
    /// <param name="keywords">关键词列表，匹配任意一个即可</param>
    /// <param name="forceAction">是否强制执行，即使已经收藏</param>
    /// <returns>收藏操作结果</returns>
    Task<OperationResult<InteractionResult>> FavoriteNoteAsync(
        List<string> keywords,
        bool forceAction = false);
}
/// <summary>
/// 账号管理服务接口 - 简化版
/// </summary>
public interface IAccountManager
{
    /// <summary>
    /// 连接到浏览器并验证登录状态
    /// </summary>
    Task<OperationResult<UserInfo>> ConnectToBrowserAsync();

    /// <summary>
    /// 检查是否已登录
    /// </summary>
    Task<bool> IsLoggedInAsync();

    /// <summary>
    /// 获取指定用户的完整个人页面数据
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <returns>完整的用户信息</returns>
    Task<OperationResult<UserInfo>> GetCompleteUserProfileDataAsync(string userId);
}
/// <summary>
/// 浏览器管理服务接口 - 单用户模式
/// </summary>
public interface IBrowserManager
{
    /// <summary>
    /// 获取或创建浏览器实例
    /// </summary>
    Task<IBrowserContext> GetBrowserContextAsync();

    /// <summary>
    /// 释放浏览器资源
    /// </summary>
    Task ReleaseBrowserAsync();

    /// <summary>
    /// 检查登录状态
    /// </summary>
    Task<bool> IsLoggedInAsync();
}
/// <summary>
/// 选择器管理服务接口 - 支持页面状态感知
/// </summary>
public interface ISelectorManager
{
    /// <summary>
    /// 获取指定别名的选择器列表
    /// </summary>
    List<string> GetSelectors(string alias);

    /// <summary>
    /// 获取指定别名和页面状态的选择器列表
    /// </summary>
    List<string> GetSelectors(string alias, PageState pageState);

    /// <summary>
    /// 获取所有选择器配置
    /// </summary>
    Dictionary<string, List<string>> GetAllSelectors();

    /// <summary>
    /// 检测当前页面状态
    /// </summary>
    Task<PageState> DetectPageStateAsync(IPage page);
}
/// <summary>
/// 延时管理器接口
/// 统一管理所有类型的拟人化延时
/// </summary>
public interface IDelayManager
{
    /// <summary>
    /// 获取思考停顿延时
    /// </summary>
    int GetThinkingPauseDelay();

    /// <summary>
    /// 获取检查停顿延时
    /// </summary>
    int GetReviewPauseDelay();

    /// <summary>
    /// 获取点击延时
    /// </summary>
    int GetClickDelay();

    /// <summary>
    /// 获取滚动延时
    /// </summary>
    int GetScrollDelay();

    /// <summary>
    /// 获取悬停延时
    /// </summary>
    int GetHoverDelay();

    /// <summary>
    /// 获取字符输入延时
    /// </summary>
    int GetCharacterTypingDelay();

    /// <summary>
    /// 获取语义单位间延时
    /// </summary>
    int GetSemanticUnitDelay();

    /// <summary>
    /// 获取重试延时（根据重试次数递增）
    /// </summary>
    int GetRetryDelay(int attemptNumber);

    /// <summary>
    /// 获取动作间延时
    /// </summary>
    int GetBetweenActionsDelay();

    /// <summary>
    /// 统一的拟人化等待控制
    /// </summary>
    Task WaitAsync(HumanWaitType waitType);
}
/// <summary>
/// 元素查找器接口
/// 统一处理元素查找和重试逻辑
/// </summary>
public interface IElementFinder
{
    /// <summary>
    /// 查找元素，支持重试和超时
    /// </summary>
    Task<IElementHandle?> FindElementAsync(IPage page, string selectorAlias, int retries = 3, int timeout = 3000);

    /// <summary>
    /// 批量查找元素
    /// </summary>
    Task<List<IElementHandle>> FindElementsAsync(IPage page, string selectorAlias, int timeout = 3000);

    /// <summary>
    /// 等待元素可见
    /// </summary>
    Task<bool> WaitForElementVisibleAsync(IPage page, string selectorAlias, int timeout = 3000);
}
/// <summary>
/// 文本输入策略接口
/// 定义不同类型元素的输入策略
/// </summary>
public interface ITextInputStrategy
{
    /// <summary>
    /// 检查策略是否适用于指定元素
    /// </summary>
    Task<bool> IsApplicableAsync(IElementHandle element);

    /// <summary>
    /// 执行文本输入
    /// </summary>
    Task InputTextAsync(IPage page, IElementHandle element, string text);
}
/// <summary>
/// 页面滚动信息
/// 用于智能边界检测和滚动控制
/// </summary>
public class ScrollInfo
{
    /// <summary>
    /// 当前滚动位置（距离页面顶部的像素）
    /// </summary>
    public int CurrentScrollTop { get; set; }

    /// <summary>
    /// 页面总滚动高度（像素）
    /// </summary>
    public int ScrollHeight { get; set; }

    /// <summary>
    /// 视窗高度（像素）
    /// </summary>
    public int ViewportHeight { get; set; }

    /// <summary>
    /// 检查是否已接近页面底部
    /// </summary>
    /// <param name="threshold">底部阈值（像素），默认100px</param>
    /// <returns>是否接近底部</returns>
    public bool IsNearBottom(int threshold = 100)
    {
        return CurrentScrollTop + ViewportHeight >= ScrollHeight - threshold;
    }

    /// <summary>
    /// 获取剩余可滚动距离
    /// </summary>
    /// <returns>剩余滚动距离（像素）</returns>
    public int GetRemainingScrollDistance()
    {
        return Math.Max(0, ScrollHeight - ViewportHeight - CurrentScrollTop);
    }
}
/// <summary>
/// 模拟真人交互服务接口 - 重构版 v4.0
/// 门面模式，协调各个专门服务
/// </summary>
public interface IHumanizedInteractionService
{
    /// <summary>
    /// 模拟真人点击操作
    /// </summary>
    Task HumanClickAsync(IPage page, string selectorAlias);

    /// <summary>
    /// 模拟真人点击操作（直接传入元素）
    /// </summary>
    Task HumanClickAsync(IPage page, IElementHandle element);

    /// <summary>
    /// 模拟真人输入操作
    /// </summary>
    Task HumanTypeAsync(IPage page, string selectorAlias, string text);

    /// <summary>
    /// 模拟真人滚动操作
    /// </summary>
    Task HumanScrollAsync(IPage page);

    /// <summary>
    /// 参数化的人性化滚动操作 - 支持虚拟化列表的滚动搜索需求
    /// </summary>
    /// <param name="page">页面对象</param>
    /// <param name="targetDistance">目标滚动距离（像素），0表示使用随机距离</param>
    /// <param name="waitForLoad">是否等待新内容加载</param>
    /// <returns></returns>
    Task HumanScrollAsync(IPage page, int targetDistance, bool waitForLoad = true);

    /// <summary>
    /// 模拟真人悬停操作
    /// </summary>
    Task HumanHoverAsync(IPage page, string selectorAlias);

    /// <summary>
    /// 模拟真人悬停操作（直接传入元素）
    /// </summary>
    Task HumanHoverAsync(IPage page, IElementHandle element);

    /// <summary>
    /// 查找元素，支持重试、多选择器和自定义超时
    /// </summary>
    Task<IElementHandle?> FindElementAsync(IPage page, string selectorAlias, int retries = 3, int timeout = 3000);

    /// <summary>
    /// 查找元素，支持页面状态感知
    /// </summary>
    Task<IElementHandle?> FindElementAsync(IPage page, string selectorAlias, PageState pageState, int retries = 3, int timeout = 3000);

    /// <summary>
    /// 统一的拟人化等待控制方法
    /// </summary>
    Task HumanWaitAsync(HumanWaitType waitType);

    /// <summary>
    /// 重试延时方法
    /// </summary>
    Task HumanRetryDelayAsync(int attemptNumber);

    /// <summary>
    /// 动作间延时方法
    /// </summary>
    Task HumanBetweenActionsDelayAsync();

    /// <summary>
    /// 拟人化点赞操作
    /// 检测当前点赞状态，执行点赞操作，并验证结果
    /// </summary>
    Task<InteractionResult> HumanLikeAsync(IPage page);

    /// <summary>
    /// 拟人化收藏操作
    /// 检测当前收藏状态，执行收藏操作，并验证结果
    /// </summary>
    Task<InteractionResult> HumanFavoriteAsync(IPage page);
}
#region 数据模型
/// <summary>
/// 页面状态枚举 - 用于页面状态感知的智能搜索架构
/// </summary>
public enum PageState
{
    /// <summary>自动检测页面状态</summary>
    Auto,
    /// <summary>探索页面 (https://www.xiaohongshu.com/explore)</summary>
    Explore,
    /// <summary>搜索结果页面 (https://www.xiaohongshu.com/search_result)</summary>
    SearchResult,
    /// <summary>未知页面状态</summary>
    Unknown
}
/// <summary>
/// 拟人化等待类型枚举
/// 定义不同场景下的等待行为模式
/// </summary>
public enum HumanWaitType
{
    /// <summary>思考停顿 - 用户在思考下一步操作时的自然停顿</summary>
    ThinkingPause,

    /// <summary>检查停顿 - 用户检查页面内容或状态时的停顿</summary>
    ReviewPause,

    /// <summary>动作间隔 - 连续动作之间的自然间隔</summary>
    BetweenActions,

    /// <summary>等待模态窗口 - 等待弹窗或模态窗口出现</summary>
    ModalWaiting,

    /// <summary>等待页面加载 - 等待页面内容完全加载</summary>
    PageLoading,

    /// <summary>等待网络响应 - 等待网络请求完成</summary>
    NetworkResponse,

    /// <summary>等待内容加载 - 虚拟化列表新内容渲染时的等待</summary>
    ContentLoading,

    /// <summary>滚动准备 - 滚动前的观察和准备时间</summary>
    ScrollPreparation,

    /// <summary>滚动执行 - 滚动步骤之间的间隔</summary>
    ScrollExecution,

    /// <summary>滚动完成 - 滚动完成后的观察时间</summary>
    ScrollCompletion,

    /// <summary>虚拟列表更新 - 等待虚拟化列表更新DOM的专用延时</summary>
    VirtualListUpdate
}
/// <summary>
/// 笔记类型枚举
/// 用于数据识别和处理，支持平台的所有内容类型
/// </summary>
public enum NoteType
{
    Unknown, // 未知类型
    Image,   // 图文笔记
    Video,   // 视频笔记
    Article  // 长文笔记（仅用于创作和数据识别，搜索筛选中不单独列出）
}
/// <summary>
/// 类型识别置信度
/// </summary>
public enum TypeIdentificationConfidence
{
    Unknown, // 无法识别
    Low,     // 低置信度
    Medium,  // 中等置信度
    High     // 高置信度
}
/// <summary>
/// 笔记基本信息 - 诚实数据模型
/// </summary>
public class NoteInfo
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;

    // 可能为空的数据字段，明确标记为可空
    public int? LikeCount { get; set; }
    public int? CommentCount { get; set; }
    public int? FavoriteCount { get; set; }
    public DateTime? PublishTime { get; set; }

    public string CoverImage { get; set; } = string.Empty;
    public string? Content { get; set; }

    /// <summary>
    /// 笔记类型
    /// </summary>
    public NoteType Type { get; set; } = NoteType.Unknown;

    /// <summary>
    /// 数据质量评级
    /// </summary>
    public DataQuality Quality { get; set; } = DataQuality.Minimal;

    /// <summary>
    /// 缺失字段列表
    /// </summary>
    public List<string> MissingFields { get; set; } = new();

    /// <summary>
    /// 数据提取时间
    /// </summary>
    public DateTime ExtractedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 基于内容特征自动识别笔记类型
    /// </summary>
    public void DetermineType()
    {
        Type = DetermineNoteTypeFromContent();
    }

    /// <summary>
    /// 根据内容特征确定笔记类型
    /// </summary>
    protected virtual NoteType DetermineNoteTypeFromContent()
    {
        // 如果是NoteDetail类型，可以使用更丰富的信息
        if (this is NoteDetail detail)
        {
            // 视频类型：有视频URL
            if (!string.IsNullOrEmpty(detail.VideoUrl))
            {
                return NoteType.Video;
            }

            // 图文类型：有图片
            if (detail.Images.Any())
            {
                return NoteType.Image;
            }

            // 长文类型：纯文字且内容较长
            if (!string.IsNullOrEmpty(detail.Content) &&
                detail.Content.Length > 500 &&
                !detail.Images.Any() &&
                string.IsNullOrEmpty(detail.VideoUrl))
            {
                return NoteType.Article;
            }
        }

        // 基于基础信息的简单判断
        if (!string.IsNullOrEmpty(CoverImage))
        {
            // 如果有封面图，可能是图文或视频，倾向于图文
            return NoteType.Image;
        }

        // 基于内容长度判断
        if (!string.IsNullOrEmpty(Content) && Content.Length > 300)
        {
            return NoteType.Article;
        }

        // 默认返回未知
        return NoteType.Unknown;
    }

    /// <summary>
    /// 检查类型识别的置信度
    /// </summary>
    public TypeIdentificationConfidence GetTypeConfidence()
    {
        if (this is NoteDetail detail)
        {
            if (!string.IsNullOrEmpty(detail.VideoUrl)) return TypeIdentificationConfidence.High;
            if (detail.Images.Any()) return TypeIdentificationConfidence.High;
            if (detail.Content.Length > 500) return TypeIdentificationConfidence.Medium;
        }

        if (!string.IsNullOrEmpty(CoverImage)) return TypeIdentificationConfidence.Low;
        if (!string.IsNullOrEmpty(Content) && Content.Length > 300) return TypeIdentificationConfidence.Low;

        return TypeIdentificationConfidence.Unknown;
    }
}
/// <summary>
/// 笔记详细信息
/// </summary>
public class NoteDetail : NoteInfo
{
    public new string Content { get; set; } = string.Empty;
    public List<string> Images { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public List<CommentInfo> Comments { get; set; } = new();
    public string VideoUrl { get; set; } = string.Empty;
}
/// <summary>
/// 评论信息 - 诚实数据模型
/// </summary>
public class CommentInfo
{
    public string Id { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;

    // 可能为空的数据字段
    public int? LikeCount { get; set; }
    public DateTime? PublishTime { get; set; }

    public List<CommentInfo> Replies { get; set; } = new();

    /// <summary>
    /// 评论数据质量评级
    /// </summary>
    public DataQuality Quality { get; set; } = DataQuality.Minimal;

    /// <summary>
    /// 评论数据提取时间
    /// </summary>
    public DateTime ExtractedAt { get; set; } = DateTime.UtcNow;
}
/// <summary>
/// 统一的操作结果类，支持泛型数据类型
/// </summary>
public class OperationResult<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ErrorCode { get; set; }
    public ErrorType ErrorType { get; set; } = ErrorType.Unknown;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public static OperationResult<T> Ok(T data) => new() {Success = true, Data = data};
    public static OperationResult<T> Fail(string error, ErrorType errorType = ErrorType.Unknown, string? errorCode = null) =>
        new() {Success = false, ErrorMessage = error, ErrorType = errorType, ErrorCode = errorCode};
}
/// <summary>
/// 错误类型枚举 - 简化版
/// </summary>
public enum ErrorType
{
    Unknown,
    NetworkError,
    LoginRequired,
    ElementNotFound,
    BrowserError,
    ValidationError,
    FileOperation
}
/// <summary>
/// 数据质量枚举
/// </summary>
public enum DataQuality
{
    Complete, // 所有字段都成功提取
    Partial,  // 部分字段缺失
    Minimal   // 仅基础信息可用
}
/// <summary>
/// 用户信息类 - 简化版，仅保留核心用户信息
/// 移除拟人化时间配置和数据提取相关属性
/// </summary>
public class UserInfo
{
    // 基础账号信息
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public bool IsLoggedIn { get; set; }
    public DateTime LastActiveTime { get; set; } = DateTime.UtcNow;

    // === 个人页面扩展数据 ===

    /// <summary>
    /// 小红书号（如：27456090856）
    /// </summary>
    public string RedId { get; set; } = string.Empty;

    /// <summary>
    /// 个人简介描述
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 用户头像URL
    /// </summary>
    public string AvatarUrl { get; set; } = string.Empty;

    /// <summary>
    /// 关注数
    /// </summary>
    public int? FollowingCount { get; set; }

    /// <summary>
    /// 粉丝数
    /// </summary>
    public int? FollowersCount { get; set; }

    /// <summary>
    /// 获赞与收藏数
    /// </summary>
    public int? LikesCollectsCount { get; set; }

    /// <summary>
    /// 是否为认证用户
    /// </summary>
    public bool IsVerified { get; set; }

    /// <summary>
    /// 认证类型（如：company-企业认证）
    /// </summary>
    public string VerificationType { get; set; } = string.Empty;

    /// <summary>
    /// 检查是否具有完整的个人页面数据
    /// </summary>
    public bool HasCompleteProfileData()
    {
        return !string.IsNullOrEmpty(RedId) &&
               !string.IsNullOrEmpty(Username) &&
               !string.IsNullOrEmpty(AvatarUrl) &&
               FollowingCount.HasValue &&
               FollowersCount.HasValue &&
               LikesCollectsCount.HasValue;
    }

    /// <summary>
    /// 获取缺失的数据字段列表
    /// </summary>
    public List<string> GetMissingFields()
    {
        var missing = new List<string>();

        if (string.IsNullOrEmpty(RedId)) missing.Add("RedId");
        if (string.IsNullOrEmpty(Username)) missing.Add("Username");
        if (string.IsNullOrEmpty(Description)) missing.Add("Description");
        if (string.IsNullOrEmpty(AvatarUrl)) missing.Add("AvatarUrl");
        if (!FollowingCount.HasValue) missing.Add("FollowingCount");
        if (!FollowersCount.HasValue) missing.Add("FollowersCount");
        if (!LikesCollectsCount.HasValue) missing.Add("LikesCollectsCount");

        return missing;
    }

    /// <summary>
    /// 从小红书号文本中提取纯数字ID
    /// </summary>
    public static string ExtractRedIdFromText(string redIdText)
    {
        if (string.IsNullOrEmpty(redIdText)) return string.Empty;

        // 从"小红书号：27456090856"中提取"27456090856"
        var match = System.Text.RegularExpressions.Regex.Match(redIdText, @"\d+");
        return match.Success ? match.Value : string.Empty;
    }
}
/// <summary>
/// 搜索数据服务接口 - 重构版
/// 集成搜索、统计分析、异步导出功能的统一服务
/// </summary>
public interface ISearchDataService
{
    /// <summary>
    /// 执行搜索并包含统计分析
    /// 集成搜索、统计计算和可选的异步导出功能
    /// </summary>
    Task<OperationResult<SearchResult>> SearchWithAnalyticsAsync(SearchRequest request);

    /// <summary>
    /// 计算搜索统计信息
    /// 基于笔记列表生成详细的统计分析
    /// </summary>
    Task<OperationResult<SearchStatistics>> CalculateSearchStatisticsAsync(List<NoteInfo> notes);

    /// <summary>
    /// 简化导出操作
    /// 只支持Excel格式
    /// </summary>
    OperationResult<SimpleExportInfo> ExportNotesAsync(List<NoteInfo> notes, string fileName, ExportOptions? options = null);
}
/// <summary>
/// 搜索请求 - 增强版，支持小红书的真实筛选选项
/// 基于真实HTML结构中的筛选面板设计
/// </summary>
public class SearchRequest
{
    public string Keyword { get; set; } = string.Empty;
    public int MaxResults { get; set; } = 10;

    /// <summary>
    /// 排序方式：comprehensive(综合), latest(最新), most_liked(最多点赞), most_commented(最多评论), most_favorited(最多收藏)
    /// </summary>
    public string SortBy { get; set; } = "comprehensive";

    /// <summary>
    /// 笔记类型：all(不限), video(视频), image(图文)
    /// 注意：搜索界面只支持这三种筛选选项，虽然平台支持article(长文)类型的创作，
    /// 但搜索筛选中不提供长文单独筛选，长文内容会在"不限"中显示
    /// </summary>
    public string NoteType { get; set; } = "all";

    /// <summary>
    /// 发布时间：all(不限), day(一天内), week(一周内), half_year(半年内)
    /// </summary>
    public string PublishTime { get; set; } = "all";

    /// <summary>
    /// 搜索范围：all(不限), viewed(已看过), unviewed(未看过), followed(已关注)
    /// </summary>
    public string SearchScope { get; set; } = "all";

    /// <summary>
    /// 位置距离：all(不限), same_city(同城), nearby(附近)
    /// </summary>
    public string LocationDistance { get; set; } = "all";

    public bool AutoExport { get; set; } = true;
    public string? ExportFileName { get; set; }
    public ExportOptions? ExportOptions { get; set; }

    /// <summary>
    /// 获取排序参数的中文显示文本
    /// </summary>
    public string GetSortDisplayText()
    {
        return SortBy switch
        {
            "comprehensive" => "综合",
            "latest" => "最新",
            "most_liked" => "最多点赞",
            "most_commented" => "最多评论",
            "most_favorited" => "最多收藏",
            _ => "综合"
        };
    }

    /// <summary>
    /// 获取笔记类型的中文显示文本
    /// 仅支持搜索界面实际提供的筛选选项
    /// </summary>
    public string GetNoteTypeDisplayText()
    {
        return NoteType switch
        {
            "all" => "不限",
            "video" => "视频",
            "image" => "图文",
            _ => "不限"
        };
    }

    /// <summary>
    /// 验证搜索请求的有效性
    /// </summary>
    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(Keyword) && MaxResults > 0;
    }
}
/// <summary>
/// 简化导出信息
/// </summary>
public record SimpleExportInfo(
    string FilePath,
    string FileName,
    DateTime ExportedAt,
    bool Success
);
/// <summary>
/// 搜索结果
/// </summary>
public record SearchResult(
    List<NoteInfo> Notes,
    int TotalCount,
    string SearchKeyword,
    TimeSpan Duration,
    SearchStatistics Statistics,
    SimpleExportInfo? ExportInfo = null
);
/// <summary>
/// 搜索统计信息 - 增强版统计数据
/// 取代 BasicStatistics 提供更详细的分析
/// </summary>
public record SearchStatistics(
    int CompleteDataCount,
    int PartialDataCount,
    int MinimalDataCount,
    double AverageLikes,
    double AverageComments,
    DateTime CalculatedAt
);
/// <summary>
/// 增强批量笔记结果 - 三位一体功能设计
/// 参考 SearchResult 的设计模式，集成笔记详情、统计分析和导出信息
/// </summary>
/// <param name="NoteDetails">成功获取的笔记详情列表</param>
/// <param name="FailedNotes">失败的关键词及错误信息</param>
/// <param name="TotalProcessed">总处理数量</param>
/// <param name="ProcessingTime">处理总耗时</param>
/// <param name="OverallQuality">整体数据质量</param>
/// <param name="Statistics">批量处理统计数据</param>
/// <param name="ExportInfo">导出信息（如果启用自动导出）</param>
public record EnhancedBatchNoteResult(
    List<NoteDetail> NoteDetails,
    List<(string Keyword, string ErrorMessage)> FailedNotes,
    int TotalProcessed,
    TimeSpan ProcessingTime,
    DataQuality OverallQuality,
    BatchProcessingStatistics Statistics,
    SimpleExportInfo? ExportInfo = null
);
/// <summary>
/// 批量处理统计数据 - 增强版统计信息
/// 参考 SearchStatistics 的设计，提供更详细的批量处理分析
/// </summary>
/// <param name="CompleteDataCount">完整数据笔记数量</param>
/// <param name="PartialDataCount">部分数据笔记数量</param>
/// <param name="MinimalDataCount">最少数据笔记数量</param>
/// <param name="AverageProcessingTime">平均处理时间（毫秒）</param>
/// <param name="AverageLikes">平均点赞数</param>
/// <param name="AverageComments">平均评论数</param>
/// <param name="TypeDistribution">笔记类型分布统计</param>
/// <param name="ProcessingModeStats">处理模式使用统计</param>
/// <param name="CalculatedAt">统计计算时间</param>
public record BatchProcessingStatistics(
    int CompleteDataCount,
    int PartialDataCount,
    int MinimalDataCount,
    double AverageProcessingTime,
    double AverageLikes,
    double AverageComments,
    Dictionary<NoteType, int> TypeDistribution,
    Dictionary<ProcessingMode, int> ProcessingModeStats,
    DateTime CalculatedAt
);
/// <summary>
/// 批量笔记结果
/// 包含详细的处理统计和性能指标
/// 支持关键词组的多对一匹配模式
/// </summary>
public record BatchNoteResult(
    List<NoteDetail> SuccessfulNotes,
    List<(string KeywordGroup, string ErrorMessage)> FailedNotes,
    int ProcessedCount,
    TimeSpan ProcessingTime,
    DataQuality OverallQuality,
    ProcessingStatistics Statistics
);
/// <summary>
/// 处理统计信息 - 批量处理的性能指标
/// </summary>
public record ProcessingStatistics(
    double AverageProcessingTimePerNote,
    int FastModeCount,
    int StandardModeCount,
    int CarefulModeCount
);
/// <summary>
/// 处理模式枚举 - 智能处理策略
/// 根据内容复杂度和系统负载动态调整处理速度
/// </summary>
public enum ProcessingMode
{
    /// <summary>快速模式：2-3秒，适用于基础信息完整的笔记</summary>
    Fast,
    /// <summary>标准模式：3-5秒，标准处理流程</summary>
    Standard,
    /// <summary>谨慎模式：5-8秒，复杂内容或每5个笔记使用</summary>
    Careful
}
/// <summary>
/// 导出选项 - 极简版
/// 只支持Excel格式，固定配置
/// </summary>
public record ExportOptions(
    bool IncludeImages = true,
    bool IncludeComments = false
);
#endregion
#region 页面内笔记查找数据模型

/// <summary>
/// 页面内查找结果
/// </summary>
public class InPageFindResult
{
    /// <summary>
    /// 成功找到的笔记详情列表
    /// </summary>
    public List<NoteDetail> FoundNotes { get; set; } = new();

    /// <summary>
    /// 失败的笔记处理记录
    /// </summary>
    public List<InPageFindError> Errors { get; set; } = new();

    /// <summary>
    /// 处理统计信息
    /// </summary>
    public InPageFindStatistics Statistics { get; set; } = new();

    /// <summary>
    /// 跳过的笔记数量（重复处理）
    /// </summary>
    public int SkippedCount { get; set; }

    /// <summary>
    /// 查找开始时间
    /// </summary>
    public DateTime StartTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 查找结束时间
    /// </summary>
    public DateTime EndTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 总处理时长
    /// </summary>
    public TimeSpan Duration => EndTime - StartTime;
}
/// <summary>
/// 页面内查找错误记录
/// </summary>
public class InPageFindError
{
    /// <summary>
    /// 笔记ID（如果能获取到）
    /// </summary>
    public string? NoteId { get; set; }

    /// <summary>
    /// 错误消息
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>
    /// 错误类型
    /// </summary>
    public InPageErrorType ErrorType { get; set; }

    /// <summary>
    /// 错误发生时间
    /// </summary>
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 重试次数
    /// </summary>
    public int RetryCount { get; set; }
}
/// <summary>
/// 页面内查找错误类型
/// </summary>
public enum InPageErrorType
{
    /// <summary>元素不可点击</summary>
    ElementNotClickable,
    /// <summary>模态窗口打开失败</summary>
    ModalOpenFailed,
    /// <summary>模态窗口关闭失败</summary>
    ModalCloseFailed,
    /// <summary>数据提取失败</summary>
    DataExtractionFailed,
    /// <summary>网络超时</summary>
    NetworkTimeout,
    /// <summary>页面状态异常</summary>
    PageStateError,
    /// <summary>重复处理</summary>
    DuplicateProcessing
}
/// <summary>
/// 页面内查找统计信息
/// </summary>
public class InPageFindStatistics
{
    /// <summary>
    /// 尝试处理的笔记总数
    /// </summary>
    public int AttemptedCount { get; set; }

    /// <summary>
    /// 成功处理的笔记数
    /// </summary>
    public int SuccessfulCount { get; set; }

    /// <summary>
    /// 失败的笔记数
    /// </summary>
    public int FailedCount { get; set; }

    /// <summary>
    /// 平均处理时间（毫秒）
    /// </summary>
    public double AverageProcessingTime { get; set; }

    /// <summary>
    /// 模态窗口打开成功率
    /// </summary>
    public double ModalOpenSuccessRate { get; set; }

    /// <summary>
    /// 数据提取完整率
    /// </summary>
    public double DataCompletenessRate { get; set; }
}
/// <summary>
/// 批量页面内查找结果
/// </summary>
public class BatchFindNotesResult
{
    /// <summary>
    /// 所有找到的笔记详情
    /// </summary>
    public List<NoteDetail> AllNotes { get; set; } = new();

    /// <summary>
    /// 按页面分组的结果
    /// </summary>
    public List<InPageFindResult> PageResults { get; set; } = new();

    /// <summary>
    /// 整体统计信息
    /// </summary>
    public BatchFindStatistics OverallStatistics { get; set; } = new();

    /// <summary>
    /// 查找关键词
    /// </summary>
    public List<string> SearchKeywords { get; set; } = new();

    /// <summary>
    /// 是否达到目标数量
    /// </summary>
    public bool TargetReached { get; set; }
}
/// <summary>
/// 批量查找统计信息
/// </summary>
public class BatchFindStatistics
{
    /// <summary>
    /// 处理的页面数量
    /// </summary>
    public int ProcessedPages { get; set; }

    /// <summary>
    /// 总找到笔记数
    /// </summary>
    public int TotalNotesFound { get; set; }

    /// <summary>
    /// 总处理时间
    /// </summary>
    public TimeSpan TotalDuration { get; set; }

    /// <summary>
    /// 平均每页处理时间
    /// </summary>
    public TimeSpan AverageTimePerPage { get; set; }

    /// <summary>
    /// 整体成功率
    /// </summary>
    public double OverallSuccessRate { get; set; }
}
#endregion
#region MCP工具强类型返回值定义
/// <summary>
/// 用户资料获取结果
/// </summary>
public record UserProfileResult(
    UserInfo? UserInfo,
    bool Success,
    string Message,
    string? ErrorCode = null
);
/// <summary>
/// 浏览器连接结果
/// </summary>
public record BrowserConnectionResult(
    bool IsConnected,
    bool IsLoggedIn,
    string Message,
    string? ErrorCode = null
);
/// <summary>
/// 笔记详情结果
/// </summary>
public record NoteDetailResult(
    NoteDetail? Detail,
    bool Success,
    string Message,
    string? ErrorCode = null
);
/// <summary>
/// 评论结果
/// </summary>
public record CommentResult(
    bool Success,
    string Message,
    string CommentId,
    string? ErrorCode = null
);
/// <summary>
/// 草稿保存结果 - 强类型版本
/// </summary>
public record DraftSaveResult(
    bool Success,
    string Message,
    string? DraftId = null,
    string? ErrorCode = null
);
/// <summary>
/// 笔记互动操作结果
/// 用于点赞、收藏、评论等操作的统一返回类型
/// </summary>
public record InteractionResult(
    bool Success,
    string Action,
    string PreviousState,
    string CurrentState,
    string Message,
    string? ErrorCode = null
);
#endregion
