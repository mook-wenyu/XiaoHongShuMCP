using System.Text.RegularExpressions;
using System.Text.Json.Serialization;
using Microsoft.Playwright;

namespace XiaoHongShuMCP.Services;

/// <summary>
/// 小红书核心服务接口
/// </summary>
public interface IXiaoHongShuService
{
    /// <summary>
    /// 查找单个笔记详情（基于关键词字符串）
    /// 通过单一关键词搜索并定位到第一个匹配的笔记，获取其详细信息
    /// 直接实现：在详情页命中则就地处理；否则从列表定位后打开详情。
    /// </summary>
    /// <param name="keyword">搜索关键词（必需）</param>
    /// <param name="includeComments">是否包含评论数据</param>
    /// <returns>笔记详情操作结果</returns>
    Task<OperationResult<NoteDetail>> GetNoteDetailAsync(
        string keyword,
        bool includeComments = false);

    /// <summary>
    /// 批量查找笔记详情（重构版） - 三位一体功能
    /// 基于单一关键词批量获取笔记详情，并集成统计分析和异步导出功能
    /// 参考 SearchDataService 的设计模式，提供同步统计计算和异步导出
    /// </summary>
    /// <param name="keyword">关键词（简化参数）</param>
    /// <param name="maxCount">最大查找数量</param>
    /// <param name="includeComments">是否包含评论数据</param>
    /// <param name="autoExport">是否自动导出到Excel</param>
    /// <param name="exportFileName">导出文件名（可选）</param>
    /// <returns>增强的批量笔记结果，包含统计分析和导出信息</returns>
    Task<OperationResult<BatchNoteResult>> BatchGetNoteDetailsAsync(
        string keyword,
        int maxCount = 10,
        bool includeComments = false,
        bool autoExport = true,
        string? exportFileName = null);

    /// <summary>
    /// 基于关键词发布评论
    /// 使用新的统一架构，通过单一关键词定位笔记并在详情页发布评论
    /// </summary>
    /// <param name="keyword">搜索关键词</param>
    /// <param name="content">评论内容</param>
    /// <returns>评论操作结果</returns>
    Task<OperationResult<CommentResult>> PostCommentAsync(
        string keyword,
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
    /// 基于关键词定位并点赞笔记
    /// </summary>
    /// <param name="keyword">关键词</param>
    /// <returns>点赞操作结果</returns>
    Task<OperationResult<InteractionResult>> LikeNoteAsync(string keyword);

    /// <summary>
    /// 基于关键词定位并收藏笔记
    /// </summary>
    /// <param name="keyword">关键词</param>
    /// <returns>收藏操作结果</returns>
    Task<OperationResult<InteractionResult>> FavoriteNoteAsync(string keyword);

    /// <summary>
    /// 统一交互：基于关键词定位并执行点赞/收藏（可组合）。
    /// </summary>
    Task<OperationResult<InteractionBundleResult>> InteractNoteAsync(string keyword, bool doLike, bool doFavorite);

    /// <summary>
    /// 基于关键词定位并取消点赞（新）
    /// </summary>
    /// <param name="keyword">关键词</param>
    /// <returns>取消点赞操作结果</returns>
    Task<OperationResult<InteractionResult>> UnlikeNoteAsync(string keyword);

    /// <summary>
    /// 基于关键词定位并取消收藏（新）
    /// </summary>
    /// <param name="keyword">关键词</param>
    /// <returns>取消收藏操作结果</returns>
    Task<OperationResult<InteractionResult>> UncollectNoteAsync(string keyword);


    /// <summary>
    /// 执行搜索笔记功能 - 拟人化操作与API监听结合
    /// 先导航到探索页面，模拟用户输入搜索关键词，然后使用网络监听捕获API响应数据
    /// 实现真实用户行为与结构化数据获取的完美结合
    /// </summary>
    /// <param name="keyword">搜索关键词</param>
    /// <param name="maxResults">最大结果数量，默认20</param>
    /// <param name="sortBy">排序方式：comprehensive(综合), latest(最新), most_liked(最多点赞)</param>
    /// <param name="noteType">笔记类型：all(不限), video(视频), image(图文)</param>
    /// <param name="publishTime">发布时间：all(不限), day(一天内), week(一周内), half_year(半年内)</param>
    /// <param name="includeAnalytics">是否包含数据分析，默认true</param>
    /// <param name="autoExport">是否自动导出Excel，默认true</param>
    /// <param name="exportFileName">导出文件名，默认自动生成</param>
    /// <returns>包含搜索结果、统计分析和导出信息的增强搜索结果</returns>
    Task<OperationResult<SearchResult>> SearchNotesAsync(
        string keyword,
        int maxResults = 20,
        string sortBy = "comprehensive",
        string noteType = "all",
        string publishTime = "all",
        bool includeAnalytics = true,
        bool autoExport = true,
        string? exportFileName = null);

    /// <summary>
    /// 获取推荐笔记，确保API被正确触发
    /// 合并自 IRecommendService.GetRecommendedNotesAsync
    /// </summary>
    /// <param name="limit">获取数量限制</param>
    /// <param name="timeout">超时时间</param>
    /// <returns>推荐结果</returns>
    Task<OperationResult<RecommendListResult>> GetRecommendedNotesAsync(int limit = 20, TimeSpan? timeout = null);

    /// <summary>
    /// 导航到发现页面并确保API被正确触发
    /// 合并自 IDiscoverPageNavigationService.NavigateToDiscoverPageAsync
    /// </summary>
    /// <param name="page">浏览器页面实例</param>
    /// <param name="timeout">超时时间</param>
    /// <returns>导航结果</returns>
    Task<DiscoverNavigationResult> NavigateToDiscoverPageAsync(IPage page, TimeSpan? timeout = null);

    /// <summary>
    /// 获取当前页面状态 - 通用版本
    /// 支持多种页面类型的检测和状态分析
    /// </summary>
    /// <param name="page">浏览器页面实例</param>
    /// <param name="expectedPageType">期望的页面类型（可选，用于优化检测）</param>
    /// <returns>通用页面状态信息</returns>
    Task<PageStatusInfo> GetCurrentPageStatusAsync(IPage page, PageType? expectedPageType = null);

}
/// <summary>
/// 账号管理服务接口
/// </summary>
public interface IAccountManager
{
    /// <summary>
    /// 连接到浏览器并验证登录状态
    /// </summary>
    Task<OperationResult<bool>> ConnectToBrowserAsync();

    /// <summary>
    /// 检查是否已登录
    /// </summary>
    Task<bool> IsLoggedInAsync();

    /// <summary>
    /// 获取指定用户的完整个人页面数据
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <returns>完整的用户信息</returns>
    Task<OperationResult<UserInfo>> GetUserProfileDataAsync(string userId);

    // === 全局用户信息管理方法（合并自 GlobalUserInfo） ===

    /// <summary>
    /// 全局当前用户信息
    /// </summary>
    UserInfo? CurrentUser { get; }

    /// <summary>
    /// 最后更新时间
    /// </summary>
    DateTime? LastUpdated { get; }

    /// <summary>
    /// 是否有有效的全局用户信息
    /// </summary>
    bool HasValidUserInfo { get; }

    /// <summary>
    /// 更新全局用户信息
    /// </summary>
    /// <param name="userInfo">新的用户信息</param>
    void UpdateUserInfo(UserInfo? userInfo);

    /// <summary>
    /// 从API响应JSON更新全局用户信息
    /// </summary>
    /// <param name="responseJson">API响应的JSON字符串</param>
    /// <returns>是否成功更新</returns>
    bool UpdateFromApiResponse(string responseJson);

    /// <summary>
    /// 获取全局用户信息的简要描述
    /// </summary>
    /// <returns>用户信息描述</returns>
    string GetUserInfoSummary();
}
/// <summary>
/// 浏览器管理服务接口
/// </summary>
public interface IBrowserManager
{
    /// <summary>
    /// 获取或创建浏览器实例
    /// </summary>
    Task<IBrowserContext> GetBrowserContextAsync();

    /// <summary>
    /// 获取页面
    /// </summary>
    Task<IPage> GetPageAsync();

    /// <summary>
    /// 释放浏览器资源
    /// </summary>
    Task ReleaseBrowserAsync();

    /// <summary>
    /// 检查登录状态
    /// </summary>
    Task<bool> IsLoggedInAsync();

    /// <summary>
    /// 检查浏览器连接是否健康
    /// </summary>
    Task<bool> IsConnectionHealthyAsync();

    /// <summary>
    /// 标记开始一段关键浏览器操作，健康检查将暂缓重连
    /// </summary>
    void BeginOperation();

    /// <summary>
    /// 标记结束关键浏览器操作
    /// </summary>
    void EndOperation();
}
/// <summary>
/// DOM元素管理服务接口 - 支持页面状态感知
/// </summary>
public interface IDomElementManager
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
    /// 统一的拟人化等待控制（加速版）。
    /// - waitType：等待场景类型（通过枚举统一管理）；
    /// - attemptNumber：重试次数（用于指数/线性退避，默认 1）。
    /// </summary>
    Task WaitAsync(HumanWaitType waitType, int attemptNumber = 1, CancellationToken cancellationToken = default);
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
    Task HumanClickAsync(string selectorAlias);

    /// <summary>
    /// 模拟真人点击操作（直接传入元素）
    /// </summary>
    Task HumanClickAsync(IElementHandle element);

    /// <summary>
    /// 模拟真人输入操作
    /// </summary>
    Task HumanTypeAsync(IPage page, string selectorAlias, string text);

    /// <summary>
    /// 模拟真人滚动操作
    /// </summary>
    Task HumanScrollAsync(IPage page, CancellationToken cancellationToken = default);

    /// <summary>
    /// 参数化的人性化滚动操作 - 支持虚拟化列表的滚动搜索需求
    /// </summary>
    /// <param name="page">页面对象</param>
    /// <param name="targetDistance">目标滚动距离（像素），0表示使用随机距离</param>
    /// <param name="waitForLoad">是否等待新内容加载</param>
    /// <returns></returns>
    Task HumanScrollAsync(IPage page, int targetDistance, bool waitForLoad = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// 模拟真人悬停操作
    /// </summary>
    Task HumanHoverAsync(string selectorAlias);

    /// <summary>
    /// 模拟真人悬停操作（直接传入元素）
    /// </summary>
    Task HumanHoverAsync(IElementHandle element);

    /// <summary>
    /// 查找元素，支持重试、多选择器和自定义超时
    /// </summary>
    Task<IElementHandle?> FindElementAsync(IPage page, string selectorAlias, int retries = 3, int timeout = 3000);

    /// <summary>
    /// 查找元素，支持页面状态感知
    /// </summary>
    Task<IElementHandle?> FindElementAsync(IPage page, string selectorAlias, PageState pageState, int retries = 3, int timeout = 3000, CancellationToken cancellationToken = default);

    /// <summary>
    /// 统一的拟人化等待控制方法
    /// </summary>
    Task HumanWaitAsync(HumanWaitType waitType, CancellationToken cancellationToken = default);

    /// <summary>
    /// 重试延时方法
    /// </summary>
    Task HumanRetryDelayAsync(int attemptNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// 动作间延时方法
    /// </summary>
    Task HumanBetweenActionsDelayAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 拟人化点赞操作
    /// 检测当前点赞状态，执行点赞操作，并验证结果
    /// </summary>
    Task<InteractionResult> HumanLikeAsync();

    /// <summary>
    /// 拟人化“取消点赞”操作（新）
    /// 检测当前点赞状态，若已点赞则点击取消；若本就未点赞则返回成功（幂等）。
    /// </summary>
    Task<InteractionResult> HumanUnlikeAsync(IPage page);

    /// <summary>
    /// 拟人化收藏操作
    /// 检测当前收藏状态，执行收藏操作，并验证结果
    /// </summary>
    Task<InteractionResult> HumanFavoriteAsync(IPage page);

    /// <summary>
    /// 拟人化“取消收藏”操作（新）
    /// 检测当前收藏状态，若已收藏则点击取消；若本就未收藏则返回成功（幂等）。
    /// </summary>
    Task<InteractionResult> HumanUnfavoriteAsync(IPage page);

    // 破坏性变更：移除 Direct* 直击交互（已废弃）。仅保留拟人化交互接口。

    /// <summary>
    /// 执行自然滚动操作
    /// </summary>
    /// <param name="page">页面对象</param>
    /// <param name="distance">滚动距离</param>
    /// <param name="duration">滚动时长</param>
    Task PerformNaturalScrollAsync(IPage page, int distance, TimeSpan duration, CancellationToken cancellationToken = default);
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
/// 通用页面类型枚举 - 支持多种页面类型检测
/// </summary>
public enum PageType
{
    /// <summary>发现页面 (https://www.xiaohongshu.com/explore?channel_id=homefeed_recommend)</summary>
    Recommend,
    /// <summary>搜索页面 (https://www.xiaohongshu.com/search_result)</summary>
    Search,
    /// <summary>个人页面 (https://www.xiaohongshu.com/user/profile)</summary>
    Profile,
    /// <summary>笔记详情页面 (https://www.xiaohongshu.com/explore/item)</summary>
    NoteDetail,
    /// <summary>首页 (https://www.xiaohongshu.com/)</summary>
    Home,
    /// <summary>未知页面类型</summary>
    Unknown
}
/// <summary>
/// 拟人化等待类型枚举
/// 定义不同场景下的等待行为模式
/// </summary>
public enum HumanWaitType
{
    /// <summary>思考停顿：决定下一步前的最短思考</summary>
    ThinkingPause,
    /// <summary>检查停顿：浏览/确认元素与内容</summary>
    ReviewPause,
    /// <summary>动作间隔：连续动作之间的最小停顿</summary>
    BetweenActions,
    /// <summary>点击准备：Hover/聚焦后到点击前的最短停顿</summary>
    ClickPreparation,
    /// <summary>悬停停顿：Hover 后的短暂停留</summary>
    HoverPause,
    /// <summary>字符输入：单字符之间的停顿</summary>
    TypingCharacter,
    /// <summary>语义单位间：输入语义片段后的检查停顿</summary>
    TypingSemanticUnit,
    /// <summary>重试退避：基于 attempt 计算的退避等待</summary>
    RetryBackoff,
    /// <summary>等待模态：模态/弹窗渲染或消失</summary>
    ModalWaiting,
    /// <summary>页面加载：页面主资源完成渲染</summary>
    PageLoading,
    /// <summary>网络响应：等待请求/响应完成</summary>
    NetworkResponse,
    /// <summary>内容加载：虚拟化/懒加载内容渲染</summary>
    ContentLoading,
    /// <summary>滚动准备：滚动前的观察准备</summary>
    ScrollPreparation,
    /// <summary>滚动执行：滚动步骤间的节奏</summary>
    ScrollExecution,
    /// <summary>滚动完成：滚动后的观察</summary>
    ScrollCompletion,
    /// <summary>虚拟列表更新：等待虚拟列表 DOM 更新</summary>
    VirtualListUpdate
}
/// <summary>
/// 详情页关键词匹配配置（权重/阈值/拼音）。
/// 可通过配置节 DetailMatchConfig 或环境变量 XHS__DetailMatchConfig__* 覆盖。
/// </summary>
public class DetailMatchConfig
{
    public double WeightedThreshold { get; set; } = 0.5; // 命中阈值（加权分/总权重）
    public int TitleWeight { get; set; } = 4;
    public int AuthorWeight { get; set; } = 3;
    public int ContentWeight { get; set; } = 2;
    public int HashtagWeight { get; set; } = 2;
    public int ImageAltWeight { get; set; } = 1;

    // 近似匹配相关（透传到 KeywordMatcher）
    public bool UseFuzzy { get; set; } = true;
    public int MaxDistanceCap { get; set; } = 3;
    public double TokenCoverageThreshold { get; set; } = 0.7;
    public bool IgnoreSpaces { get; set; } = true;

    // 拼音匹配（无外部依赖，使用 GB2312 首字母启发式）
    public bool UsePinyin { get; set; } = true;
    public bool PinyinInitialsOnly { get; set; } = true; // 仅首字母匹配
}
/// <summary>
/// 笔记类型枚举
/// 用于数据识别和处理，支持平台的主要内容类型
/// 注意：长文类型本质上是字数更多的图文笔记，在搜索和处理中统一归类为图文类型
/// </summary>
public enum NoteType
{
    Unknown, // 未知类型
    Image,   // 图文笔记（包含长文，因为长文本质就是字数更多的图文）
    Video    // 视频笔记
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
/// 笔记基本信息
/// </summary>
public class NoteInfo
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// 顶层模型类型（如：note）。来源：item.model_type
    /// </summary>
    public string ModelType { get; set; } = string.Empty;

    // 可能为空的数据字段，明确标记为可空
    public int? LikeCount { get; set; }
    public int? CommentCount { get; set; }
    public int? FavoriteCount { get; set; }
    public DateTime? PublishTime { get; set; }

    public string CoverImage { get; set; } = string.Empty;
    public string? Content { get; set; }
    public List<string> Images { get; set; } = [];

    /// <summary>
    /// 描述/内容预览（来自搜索结果 note_card.desc）
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 原始笔记类型字符串（来自 note_card.type，示例：normal、video）
    /// </summary>
    public string? RawNoteType { get; set; }

    /// <summary>
    /// 视频直链（如果是视频笔记）
    /// </summary>
    public string VideoUrl { get; set; } = string.Empty;

    /// <summary>
    /// 视频时长（秒）
    /// </summary>
    public int? VideoDuration { get; set; }

    /// <summary>
    /// 是否已点赞（若可用）
    /// </summary>
    public bool IsLiked { get; set; }

    /// <summary>
    /// 是否已收藏（若可用）
    /// </summary>
    public bool IsCollected { get; set; }

    /// <summary>
    /// 分享数（若可用）
    /// </summary>
    public int? ShareCount { get; set; }

    /// <summary>
    /// 页面令牌（若可用）
    /// </summary>
    public string? PageToken { get; set; }

    /// <summary>
    /// 搜索ID（若可用）
    /// </summary>
    public string? SearchId { get; set; }

    /// <summary>
    /// 视频详细信息（若可用）
    /// </summary>
    public RecommendedVideoInfo? VideoInfo { get; set; }

    /// <summary>
    /// 封面详细信息（若可用）
    /// </summary>
    public RecommendedCoverInfo? CoverInfo { get; set; }

    /// <summary>
    /// 互动详细信息（若可用）
    /// </summary>
    public RecommendedInteractInfo? InteractInfo { get; set; }

    /// <summary>
    /// 用户详细信息（若可用）
    /// </summary>
    public RecommendedUserInfo? UserInfo { get; set; }

    /// <summary>
    /// 扩展字段：作者用户ID
    /// </summary>
    public string AuthorId { get; set; } = string.Empty;

    /// <summary>
    /// 扩展字段：作者头像URL
    /// </summary>
    public string AuthorAvatar { get; set; } = string.Empty;

    /// <summary>
    /// 扩展字段：作者级 xsec_token（来源 note_card.user.xsec_token）
    /// </summary>
    public string? AuthorXsecToken { get; set; }

    /// <summary>
    /// 扩展字段：跟踪ID，用于分析和后续请求
    /// </summary>
    public string? TrackId { get; set; }

    /// <summary>
    /// 扩展字段：安全令牌
    /// </summary>
    public string? XsecToken { get; set; }

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
    public List<string> MissingFields { get; set; } = [];

    /// <summary>
    /// 角标标签（如发布日角标）。来源：note_card.corner_tag_info
    /// </summary>
    public List<CornerTag> CornerTags { get; set; } = [];

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
    /// 确定数据质量
    /// </summary>
    public void DetermineDataQuality()
    {
        var hasBasicInfo = !string.IsNullOrEmpty(Title) && !string.IsNullOrEmpty(Author);
        var hasStats = LikeCount.HasValue && CommentCount.HasValue;
        var hasTime = PublishTime.HasValue;

        if (hasBasicInfo && hasStats && hasTime)
            Quality = DataQuality.Complete;
        else if (hasBasicInfo && (hasStats || hasTime))
            Quality = DataQuality.Partial;
        else
            Quality = DataQuality.Minimal;
    }

    /// <summary>
    /// 根据内容特征确定笔记类型 - 简化的类型识别逻辑
    /// 长文类型统一归类为图文类型，因为本质上是字数更多的图文笔记
    /// </summary>
    protected virtual NoteType DetermineNoteTypeFromContent()
    {
        // 如果是NoteDetail类型，可以使用更丰富的信息
        if (this is NoteDetail detail)
        {
            // 优先级1：视频时长信息（最可靠的视频标识）
            if (detail.VideoDuration is > 0)
            {
                return NoteType.Video;
            }

            // 优先级2：视频URL（直接视频标识）
            if (!string.IsNullOrEmpty(detail.VideoUrl))
            {
                return NoteType.Video;
            }

            // 优先级3：图片存在或有内容时，统一归类为图文类型
            if (detail.Images.Count != 0 || !string.IsNullOrEmpty(detail.Content))
            {
                return NoteType.Image;
            }
        }

        // 基于基础信息的简单判断
        if (!string.IsNullOrEmpty(CoverImage))
        {
            // 如果有封面图，可能是图文或视频，无法准确区分时倾向于图文
            return NoteType.Image;
        }

        // 基于内容长度判断 - 有内容的都归为图文类型
        if (!string.IsNullOrEmpty(Content))
        {
            return NoteType.Image;
        }

        // 默认返回未知
        return NoteType.Unknown;
    }

    /// <summary>
    /// 检查类型识别的置信度 - 增强视频识别置信度评估
    /// </summary>
    public TypeIdentificationConfidence GetTypeConfidence()
    {
        if (this is NoteDetail detail)
        {
            // 视频时长信息 - 最高置信度
            if (detail.VideoDuration is > 0)
                return TypeIdentificationConfidence.High;

            // 视频URL - 高置信度
            if (!string.IsNullOrEmpty(detail.VideoUrl))
                return TypeIdentificationConfidence.High;

            // 图片存在 - 高置信度
            if (detail.Images.Count != 0)
                return TypeIdentificationConfidence.High;

            // 长文本 - 中等置信度
            if (detail.Content.Length > 500)
                return TypeIdentificationConfidence.Medium;
        }

        // 基于基础信息的低置信度判断
        if (!string.IsNullOrEmpty(CoverImage))
            return TypeIdentificationConfidence.Low;
        if (!string.IsNullOrEmpty(Content) && Content.Length > 300)
            return TypeIdentificationConfidence.Low;

        return TypeIdentificationConfidence.Unknown;
    }
}
/// <summary>
/// 笔记详细信息 - 扩展支持视频信息
/// </summary>
public class NoteDetail : NoteInfo
{
    public new string Content { get; set; } = string.Empty;
    public new List<string> Images { get; set; } = [];
    public List<string> Tags { get; set; } = [];
    public List<CommentInfo> Comments { get; set; } = [];

    /// <summary>
    /// IP属地（如果可用，来源：feed.note_card.ip_location）
    /// </summary>
    public string IpLocation { get; set; } = string.Empty;

    /// <summary>
    /// 最后更新时间（如果可用，来源：feed.note_card.last_update_time）
    /// </summary>
    public DateTime? LastUpdateTime { get; set; }

    /// <summary>
    /// @的用户列表（如果可用，来源：feed.note_card.at_user_list）
    /// </summary>
    public List<AtUserInfo> AtUsers { get; set; } = [];

    /// <summary>
    /// 分享是否被禁止（如果可用，来源：feed.note_card.share_info.un_share）
    /// </summary>
    public bool? ShareDisabled { get; set; }

    /// <summary>
    /// 是否为视频笔记
    /// </summary>
    public bool IsVideo => !string.IsNullOrEmpty(VideoUrl) || VideoDuration.HasValue;

    /// <summary>
    /// 数据来源端点（用于去重机制）
    /// </summary>
    public ApiEndpointType? SourceEndpoint { get; set; }

    /// <summary>
    /// 获取格式化的视频时长文本
    /// </summary>
    /// <returns>格式化的时长，如"1:23"或"0:45"</returns>
    public string GetFormattedVideoDuration()
    {
        if (!VideoDuration.HasValue || VideoDuration.Value <= 0)
            return string.Empty;

        int minutes = VideoDuration.Value / 60;
        int seconds = VideoDuration.Value % 60;
        return $"{minutes}:{seconds:D2}";
    }
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

    public List<CommentInfo> Replies { get; set; } = [];

    // 扩展字段（来自评论API）
    public string NoteId { get; set; } = string.Empty;
    public string AuthorId { get; set; } = string.Empty;
    public string AuthorAvatar { get; set; } = string.Empty;
    public string? AuthorXsecToken { get; set; }
    public string IpLocation { get; set; } = string.Empty;
    public bool? Liked { get; set; }
    public List<string> PictureUrls { get; set; } = [];
    public List<string> ShowTags { get; set; } = [];

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
/// 页面状态守护接口（IPageStateGuard）。
/// - 职责：在执行搜索、推荐、批量操作等需要处于“列表/发现/搜索”上下文的流程前，
///         检查是否处于“笔记详情”页；若是，则尝试优雅退出（点击关闭按钮/遮罩/ESC）。
/// - 设计：独立于业务服务，遵循单一职责，便于复用与单元测试。
/// </summary>
public interface IPageStateGuard
{
    /// <summary>
    /// 若当前处于“笔记详情”页面，则尝试退出；否则直接返回 true。
    /// 成功判定：退出后页面不再是 NoteDetail。
    /// </summary>
    /// <param name="page">浏览器页面</param>
    /// <returns>是否保证当前不在笔记详情页</returns>
    Task<bool> EnsureExitNoteDetailIfPresentAsync(IPage page);

    /// <summary>
    /// 确保当前处于“发现/搜索”入口上下文：
    /// 1) 若在详情页，先尝试退出；
    /// 2) 若已在 发现(Recommend)/搜索(Search) 则直接通过；
    /// 3) 否则尝试点击侧边栏发现链接；失败则回退为直接URL导航；
    /// 4) 导航后再次检测，判定成功与否。
    /// </summary>
    /// <param name="page">浏览器页面</param>
    /// <returns>是否处于发现/搜索入口上下文</returns>
    Task<bool> EnsureOnDiscoverOrSearchAsync(IPage page);
}
/// <summary>
/// 被@的用户信息（简化版）
/// </summary>
public class AtUserInfo
{
    public string UserId { get; set; } = string.Empty;
    public string Nickname { get; set; } = string.Empty;
    public string? XsecToken { get; set; }
}
/// <summary>
/// 卡片角标信息（如发布时间角标）
/// </summary>
public class CornerTag
{
    /// <summary>
    /// 角标类型（示例：publish_time）
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// 文本（示例：08-30、昨天、今天）
    /// </summary>
    public string Text { get; set; } = string.Empty;
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
    FileOperation,
    NavigationError,
    CollectionError,
    OperationCancelled
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
/// <summary>
/// 基础用户信息类，包含所有用户信息类的共同字段
/// </summary>
public abstract class BaseUserInfo
{
    /// <summary>
    /// 用户ID
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// 用户昵称
    /// </summary>
    public string Nickname { get; set; } = string.Empty;

    /// <summary>
    /// 用户头像URL
    /// </summary>
    public string Avatar { get; set; } = string.Empty;
}
/// <summary>
/// 完整的用户信息类（继承自BaseUserInfo）
/// </summary>
public class UserInfo : BaseUserInfo
{
    public bool IsLoggedIn { get; set; }
    public DateTime LastActiveTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    // Username 兼容别名已删除，请使用 Nickname

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
    /// 用户头像URL（重命名以避免与基类冲突）
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
               !string.IsNullOrEmpty(Nickname) &&
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
        if (string.IsNullOrEmpty(Nickname)) missing.Add("Nickname");
        if (string.IsNullOrEmpty(Description)) missing.Add("Description");
        if (string.IsNullOrEmpty(AvatarUrl)) missing.Add("AvatarUrl");
        if (!FollowingCount.HasValue) missing.Add("FollowingCount");
        if (!FollowersCount.HasValue) missing.Add("FollowersCount");
        if (!LikesCollectsCount.HasValue) missing.Add("LikesCollectsCount");

        return missing;
    }

    // === API 兼容性字段（用于支持原 UserApiData 的字段） ===

    /// <summary>
    /// 用户ID的替代字段（API兼容性）
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// 用户名称的替代字段（API兼容性）
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// 粉丝数的替代字段（API兼容性）
    /// </summary>
    public int? FansCount { get; set; }

    /// <summary>
    /// 关注数的替代字段（API兼容性）
    /// </summary>
    public int? FollowCount { get; set; }

    /// <summary>
    /// 笔记数的替代字段（API兼容性）
    /// </summary>
    public int? NoteCount { get; set; }

    /// <summary>
    /// 笔记数的另一个替代字段（API兼容性）
    /// </summary>
    public int? NotesCount { get; set; }

    /// <summary>
    /// 描述的替代字段（API兼容性）
    /// </summary>
    public string? Desc { get; set; }

    /// <summary>
    /// 从API响应创建UserInfo实例
    /// </summary>
    public static UserInfo FromApiData(UserInfo apiData)
    {
        return new UserInfo
        {
            UserId = apiData.UserId,
            Nickname = apiData.Nickname,
            IsLoggedIn = true,
            RedId = apiData.RedId,
            AvatarUrl = apiData.Avatar,
            Avatar = apiData.Avatar,
            FollowersCount = apiData.FansCount ?? apiData.FollowersCount,
            FollowingCount = apiData.FollowCount ?? apiData.FollowingCount,
            LikesCollectsCount = apiData.NoteCount ?? apiData.NotesCount ?? apiData.LikesCollectsCount,
            Description = apiData.Desc ?? apiData.Description,
            LastActiveTime = DateTime.UtcNow
        };
    }

    /// <summary>
    /// 从小红书号文本中提取纯数字ID
    /// </summary>
    public static string ExtractRedIdFromText(string redIdText)
    {
        if (string.IsNullOrEmpty(redIdText)) return string.Empty;

        // 从"小红书号：27456090856"中提取"27456090856"
        var match = Regex.Match(redIdText, @"\d+");
        return match.Success ? match.Value : string.Empty;
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
/// 搜索结果 - 统一的搜索结果数据模型
/// 整合了基础搜索和增强搜索的所有功能
/// </summary>
/// <param name="Notes">搜索到的笔记列表（支持基础NoteInfo和扩展RecommendedNote）</param>
/// <param name="TotalCount">总计数量（TotalCollected的别名，保持向后兼容）</param>
/// <param name="SearchKeyword">搜索关键词</param>
/// <param name="Duration">搜索耗时</param>
/// <param name="Statistics">搜索统计信息</param>
/// <param name="ExportInfo">导出信息（可选）</param>
/// <param name="ApiRequests">API请求次数（可选，增强功能）</param>
/// <param name="InterceptedResponses">成功监听的API响应数量（可选，增强功能）</param>
/// <param name="RawApiData">原始API响应数据（可选，增强功能）</param>
/// <param name="SearchParameters">搜索参数详情（可选，增强功能）</param>
public record SearchResult(
    List<NoteInfo> Notes,
    int TotalCount,
    string SearchKeyword,
    TimeSpan Duration,
    SearchStatistics Statistics,
    SimpleExportInfo? ExportInfo = null,
    int ApiRequests = 0,
    int InterceptedResponses = 0,
    List<SearchNotesApiResponse>? RawApiData = null,
    SearchParametersInfo? SearchParameters = null
);
/// <summary>
/// 搜索统计信息 - 统一的搜索统计数据模型
/// 整合了基础统计和增强统计的所有功能
/// </summary>
/// <param name="CompleteDataCount">完整数据数量</param>
/// <param name="PartialDataCount">部分数据数量</param>
/// <param name="MinimalDataCount">最少数据数量</param>
/// <param name="AverageLikes">平均点赞数</param>
/// <param name="AverageComments">平均评论数</param>
/// <param name="CalculatedAt">统计计算时间</param>
/// <param name="VideoNotesCount">视频笔记数量（可选，增强功能）</param>
/// <param name="ImageNotesCount">图文笔记数量（可选，增强功能）</param>
/// <param name="AverageCollects">平均收藏数（可选，增强功能）</param>
/// <param name="AuthorDistribution">作者分布统计（可选，增强功能）</param>
/// <param name="TypeDistribution">笔记类型分布统计（可选，增强功能）</param>
/// <param name="DataQualityDistribution">数据质量分布统计（可选，增强功能）</param>
public record SearchStatistics(
    int CompleteDataCount,
    int PartialDataCount,
    int MinimalDataCount,
    double AverageLikes,
    double AverageComments,
    DateTime CalculatedAt,
    int VideoNotesCount = 0,
    int ImageNotesCount = 0,
    double AverageCollects = 0.0,
    Dictionary<string, int>? AuthorDistribution = null,
    Dictionary<NoteType, int>? TypeDistribution = null,
    Dictionary<DataQuality, int>? DataQualityDistribution = null
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
/// 批量笔记结果 - 统一的批量处理结果模型
/// 整合了基础批量处理和增强批量处理的所有功能
/// 支持关键词组的多对一匹配模式
/// </summary>
/// <param name="SuccessfulNotes">成功获取的笔记详情列表（NoteDetails的别名，保持向后兼容）</param>
/// <param name="FailedNotes">失败的关键词及错误信息（支持Keyword和KeywordGroup）</param>
/// <param name="ProcessedCount">总处理数量（TotalProcessed的别名，保持向后兼容）</param>
/// <param name="ProcessingTime">处理总耗时</param>
/// <param name="OverallQuality">整体数据质量</param>
/// <param name="Statistics">批量处理统计数据</param>
/// <param name="ExportInfo">导出信息（可选，增强功能）</param>
public record BatchNoteResult(
    List<NoteDetail> SuccessfulNotes,
    List<(string KeywordGroup, string ErrorMessage)> FailedNotes,
    int ProcessedCount,
    TimeSpan ProcessingTime,
    DataQuality OverallQuality,
    BatchProcessingStatistics Statistics,
    SimpleExportInfo? ExportInfo = null
)
{
    // NoteDetails/TotalProcessed 兼容属性已删除，请直接使用 SuccessfulNotes/ProcessedCount
}
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
/// <summary>
/// 搜索请求模型（用于测试与参数验证）
/// </summary>
public class SearchRequest
{
    public string Keyword { get; set; } = string.Empty;
    public int MaxResults { get; set; }

    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(Keyword) && MaxResults > 0;
    }
}
#region 推荐服务接口
/// <summary>
/// 增强的推荐服务接口
/// 负责管理小红书推荐API的调用和数据收集
/// </summary>
public interface IRecommendService
{
    /// <summary>
    /// 获取推荐笔记，确保API被正确触发
    /// </summary>
    /// <param name="limit">获取数量限制</param>
    /// <param name="timeout">超时时间</param>
    /// <returns>推荐结果</returns>
    Task<OperationResult<RecommendListResult>> GetRecommendedNotesAsync(int limit = 20, TimeSpan? timeout = null);
}
/// <summary>
/// 发现页面导航服务接口
/// 负责管理到发现页面的导航和API触发验证
/// </summary>
public interface IDiscoverPageNavigationService
{
    /// <summary>
    /// 导航到发现页面并确保API被正确触发
    /// </summary>
    /// <param name="page">浏览器页面实例</param>
    /// <param name="timeout">超时时间</param>
    /// <returns>导航结果</returns>
    Task<DiscoverNavigationResult> NavigateToDiscoverPageAsync(IPage page, TimeSpan? timeout = null);
}
/// <summary>
/// 智能收集控制器接口
/// 负责管理分批收集推荐笔记的整体流程，包括进度跟踪、滚动策略和性能监控
/// </summary>
public interface ISmartCollectionController
{
    /// <summary>
    /// 执行智能分批收集
    /// </summary>
    /// <param name="context">浏览器上下文</param>
    /// <param name="page">浏览器页面实例</param>
    /// <param name="targetCount">目标收集数量</param>
    /// <param name="collectionMode">收集模式</param>
    /// <param name="timeout">超时时间</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>智能收集结果</returns>
    Task<SmartCollectionResult> ExecuteSmartCollectionAsync(
        IBrowserContext context,
        IPage page,
        int targetCount,
        RecommendCollectionMode collectionMode = RecommendCollectionMode.Standard,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);


    /// <summary>
    /// 执行纯数据收集（不包含API监听）
    /// </summary>
    /// <param name="context">浏览器上下文</param>
    /// <param name="page">浏览器页面实例</param>
    /// <param name="targetCount">目标收集数量</param>
    /// <param name="collectionMode">收集模式</param>
    /// <param name="timeout">超时时间</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>数据收集结果</returns>
    // 原 ExecuteDataCollectionAsync（DOM 收集）已移除：统一改为通过 API 监听获取数据。

    /// <summary>
    /// 获取当前收集状态
    /// </summary>
    CollectionStatus GetCurrentStatus();

    /// <summary>
    /// 重置收集状态
    /// </summary>
    void ResetCollectionState();
}
/// <summary>
/// API连接状态
/// 用于验证推荐API的连接和触发状态
/// </summary>
public class ApiConnectionStatus
{
    /// <summary>
    /// 连接是否正常
    /// </summary>
    public bool IsConnected { get; set; }

    /// <summary>
    /// 浏览器状态
    /// </summary>
    public string BrowserStatus { get; set; } = string.Empty;

    /// <summary>
    /// 页面状态
    /// </summary>
    public string PageStatus { get; set; } = string.Empty;

    /// <summary>
    /// API触发状态
    /// </summary>
    public bool ApiTriggered { get; set; }

    /// <summary>
    /// 详细信息
    /// </summary>
    public List<string> Details { get; set; } = [];
}
/// <summary>
/// 发现页面导航结果
/// </summary>
public class DiscoverNavigationResult
{
    /// <summary>
    /// 导航是否成功
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 最终页面URL
    /// </summary>
    public string FinalUrl { get; set; } = string.Empty;

    /// <summary>
    /// 导航方式
    /// </summary>
    public DiscoverNavigationMethod Method { get; set; }

    /// <summary>
    /// API是否被触发
    /// </summary>
    public bool ApiTriggered { get; set; }

    /// <summary>
    /// 耗时（毫秒）
    /// </summary>
    public long DurationMs { get; set; }

    /// <summary>
    /// 错误消息
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 详细日志
    /// </summary>
    public List<string> NavigationLog { get; set; } = [];
}
// DiscoverPageStatus 兼容类已删除，请改用通用 PageStatusInfo。
/// <summary>
/// 通用页面状态信息
/// 支持多种页面类型的状态检测和信息记录
/// </summary>
public class PageStatusInfo
{
    /// <summary>
    /// 页面类型
    /// </summary>
    public PageType PageType { get; set; } = PageType.Unknown;

    /// <summary>
    /// 当前页面URL
    /// </summary>
    public string CurrentUrl { get; set; } = string.Empty;

    /// <summary>
    /// 传统页面状态（兼容性）
    /// </summary>
    public PageState PageState { get; set; } = PageState.Unknown;

    /// <summary>
    /// 检测到的页面元素信息
    /// Key: 元素类型名称, Value: 元素数量
    /// </summary>
    public Dictionary<string, int> ElementsDetected { get; set; } = new();

    /// <summary>
    /// API功能特征列表
    /// </summary>
    public List<string> ApiFeatures { get; set; } = new();

    /// <summary>
    /// 页面是否就绪
    /// </summary>
    public bool IsPageReady { get; set; }

    /// <summary>
    /// 检测时间
    /// </summary>
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 检测详细日志
    /// </summary>
    public List<string> DetectionLog { get; set; } = new();

    /// <summary>
    /// 检查指定页面类型是否匹配
    /// </summary>
    /// <param name="expectedType">期望的页面类型</param>
    /// <returns>是否匹配</returns>
    public bool IsPageType(PageType expectedType) => PageType == expectedType && IsPageReady;

    /// <summary>
    /// 获取指定类型元素的数量
    /// </summary>
    /// <param name="elementType">元素类型</param>
    /// <returns>元素数量</returns>
    public int GetElementCount(string elementType) => ElementsDetected.GetValueOrDefault(elementType, 0);

    /// <summary>
    /// 添加检测日志
    /// </summary>
    /// <param name="logEntry">日志条目</param>
    public void AddDetectionLog(string logEntry)
    {
        DetectionLog.Add($"[{DateTime.UtcNow:HH:mm:ss.fff}] {logEntry}");
    }
}
/// <summary>
/// 导航方式枚举
/// </summary>
public enum DiscoverNavigationMethod
{
    /// <summary>通过点击发现按钮</summary>
    ClickButton,
    /// <summary>直接URL导航</summary>
    DirectUrl,
    /// <summary>JavaScript执行</summary>
    JavaScript,
    /// <summary>失败</summary>
    Failed
}
/// <summary>
/// 智能收集结果
/// </summary>
public record SmartCollectionResult
{
    public bool Success { get; init; }
    public List<NoteInfo> CollectedNotes { get; init; } = [];
    public int CollectedCount { get; init; }
    public int TargetCount { get; init; }
    public int RequestCount { get; init; }
    public TimeSpan Duration { get; init; }
    public string? ErrorMessage { get; init; }
    public CollectionPerformanceMetrics PerformanceMetrics { get; init; } = new();
    public CollectionStatus CollectionDetails { get; init; } = new();
    public bool ReachedTarget { get; init; }
    public double EfficiencyScore { get; init; }

    /// <summary>
    /// 创建自定义结果（用于数据合并场景）
    /// </summary>
    public SmartCollectionResult(
        bool success,
        string? errorMessage,
        List<NoteInfo> collectedNotes,
        int targetCount,
        int actuallyCollected,
        TimeSpan duration,
        bool apiDataAvailable = false,
        double efficiencyScore = 0.0)
    {
        Success = success;
        ErrorMessage = errorMessage;
        CollectedNotes = collectedNotes;
        CollectedCount = actuallyCollected;
        TargetCount = targetCount;
        RequestCount = 0; // 合并场景下不计算请求数
        Duration = duration;
        ReachedTarget = actuallyCollected >= targetCount;
        EfficiencyScore = efficiencyScore;

        // 为合并场景设置特殊的性能指标
        PerformanceMetrics = new CollectionPerformanceMetrics
        {
            ApiDataAvailable = apiDataAvailable,
            TotalDuration = duration,
            EfficiencyRating = efficiencyScore > 80 ? "High" :
                efficiencyScore > 60 ? "Medium" : "Low"
        };

        CollectionDetails = new CollectionStatus
        {
            Phase = CollectionPhase.Completed,
            Progress = actuallyCollected,
            TargetCount = targetCount,
            LastUpdateTime = DateTime.UtcNow
        };
    }

    /// <summary>
    /// 创建成功结果
    /// </summary>
    public static SmartCollectionResult CreateSuccess(
        List<NoteInfo> collectedNotes,
        int targetCount,
        int requestCount,
        TimeSpan duration,
        CollectionPerformanceMetrics performanceMetrics)
    {
        return new SmartCollectionResult(
            success: true,
            errorMessage: null,
            collectedNotes: collectedNotes,
            targetCount: targetCount,
            actuallyCollected: collectedNotes.Count,
            duration: duration,
            apiDataAvailable: false,
            efficiencyScore: targetCount > 0 ? (double)collectedNotes.Count / targetCount * 100 : 100.0
        );
    }

    /// <summary>
    /// 创建失败结果
    /// </summary>
    public static SmartCollectionResult CreateFailure(
        string errorMessage,
        List<NoteInfo>? partialResults = null,
        int targetCount = 0,
        int requestCount = 0,
        TimeSpan duration = default)
    {
        var collected = partialResults ?? [];
        return new SmartCollectionResult(
            success: false,
            errorMessage: errorMessage,
            collectedNotes: collected,
            targetCount: targetCount,
            actuallyCollected: collected.Count,
            duration: duration,
            apiDataAvailable: false,
            efficiencyScore: 0.0
        );
    }
}
/// <summary>
/// 收集状态
/// </summary>
public class CollectionStatus
{
    public int TargetCount { get; set; }
    public int CurrentCount { get; set; }
    public double Progress { get; set; }
    public RecommendCollectionMode CollectionMode { get; set; }
    public CollectionPhase Phase { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime LastUpdateTime { get; set; }
}
/// <summary>
/// 收集阶段
/// </summary>
public enum CollectionPhase
{
    Initializing,
    Navigating,
    Collecting,
    Completed,
    Failed
}
/// <summary>
/// 收集性能指标
/// </summary>
public class CollectionPerformanceMetrics
{
    public int RequestCount { get; set; }
    public int SuccessfulRequests { get; set; }
    public int FailedRequests { get; set; }
    public int ScrollCount { get; set; }
    public TimeSpan Duration { get; set; }
    public double RequestSuccessRate => RequestCount > 0 ? (double)SuccessfulRequests / RequestCount : 0;
    public double ScrollEfficiency => ScrollCount > 0 ? (double)SuccessfulRequests / ScrollCount : 0;
    public TimeSpan TotalDuration { get; set; }
    public string EfficiencyRating { get; set; } = "Unknown";
    public bool ApiDataAvailable { get; set; }

    /// <summary>
    /// 默认构造函数
    /// </summary>
    public CollectionPerformanceMetrics()
    {
    }

    /// <summary>
    /// 带参数的构造函数
    /// </summary>
    public CollectionPerformanceMetrics(int successfulRequests, int failedRequests, int scrollCount, TimeSpan duration)
    {
        SuccessfulRequests = successfulRequests;
        FailedRequests = failedRequests;
        RequestCount = successfulRequests + failedRequests;
        ScrollCount = scrollCount;
        Duration = duration;
    }
}
#endregion
#region GetRecommendedNotes
/// <summary>
/// 推荐笔记数据模型 - 基于API监听的完整笔记信息
/// 包含从小红书搜索API中提取的所有可用数据
/// </summary>
public class RecommendedNote : NoteInfo
{
    /// <summary>
    /// 图片列表信息
    /// </summary>
    public new List<RecommendedImageInfo> Images { get; set; } = [];

    /// <summary>
    /// 获取格式化的视频时长文本（重写基类方法以支持新的视频数据结构）
    /// </summary>
    public string GetFormattedVideoDurationEnhanced()
    {
        if (VideoInfo?.Duration > 0)
        {
            int minutes = VideoInfo.Duration / 60;
            int seconds = VideoInfo.Duration % 60;
            return $"{minutes}:{seconds:D2}";
        }

        if (VideoDuration is > 0)
        {
            int minutes = VideoDuration.Value / 60;
            int seconds = VideoDuration.Value % 60;
            return $"{minutes}:{seconds:D2}";
        }

        return string.Empty;
    }

    /// <summary>
    /// 判断是否为视频笔记（增强版判断逻辑）
    /// </summary>
    public bool IsVideoEnhanced => VideoInfo?.Duration > 0 || !string.IsNullOrEmpty(VideoUrl) || VideoDuration.HasValue;
}
/// <summary>
/// 推荐图片信息
/// 基于API返回的图片数据结构
/// </summary>
public class RecommendedImageInfo
{
    /// <summary>
    /// 图片URL
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// 图片宽度
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// 图片高度
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// 图片场景信息列表（不同尺寸的图片URL）
    /// </summary>
    public List<ImageSceneInfo> Scenes { get; set; } = [];
}
/// <summary>
/// 图片场景信息
/// 对应不同尺寸和质量的图片版本
/// </summary>
public class ImageSceneInfo
{
    /// <summary>
    /// 场景标识（如：WB_DFT, WB_PRV等）
    /// </summary>
    public string SceneType { get; set; } = string.Empty;

    /// <summary>
    /// 场景对应的图片URL
    /// </summary>
    public string Url { get; set; } = string.Empty;
}
/// <summary>
/// 推荐视频信息
/// 基于API返回的视频数据结构
/// </summary>
public class RecommendedVideoInfo
{
    /// <summary>
    /// 视频时长（秒）
    /// </summary>
    public int Duration { get; set; }

    /// <summary>
    /// 视频封面URL
    /// </summary>
    public string Cover { get; set; } = string.Empty;

    /// <summary>
    /// 视频URL
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// 视频宽度
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// 视频高度
    /// </summary>
    public int Height { get; set; }
}
/// <summary>
/// 推荐封面信息
/// 基于API返回的封面数据结构
/// </summary>
public class RecommendedCoverInfo
{
    /// <summary>
    /// 默认封面URL
    /// </summary>
    public string DefaultUrl { get; set; } = string.Empty;

    /// <summary>
    /// 预览封面URL
    /// </summary>
    public string PreviewUrl { get; set; } = string.Empty;

    /// <summary>
    /// 封面宽度
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// 封面高度
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// 文件ID
    /// </summary>
    public string FileId { get; set; } = string.Empty;

    /// <summary>
    /// 封面场景信息列表
    /// </summary>
    public List<ImageSceneInfo> Scenes { get; set; } = [];
}
/// <summary>
/// 推荐互动信息
/// 基于API返回的互动数据结构
/// </summary>
/// <summary>
/// 推荐内容的交互信息（继承自BaseInteractInfo）
/// </summary>
public class RecommendedInteractInfo : BaseInteractInfo
{
    /// <summary>
    /// 点赞数（原始字符串格式）
    /// </summary>
    public string LikedCountRaw { get; set; } = "0";

    /// <summary>
    /// 分享数
    /// </summary>
    public int ShareCount { get; set; }
}
/// <summary>
/// 推荐用户信息
/// 基于API返回的用户数据结构
/// </summary>
public class RecommendedUserInfo
{
    /// <summary>
    /// 用户ID
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// 昵称
    /// </summary>
    public string Nickname { get; set; } = string.Empty;

    /// <summary>
    /// 头像URL
    /// </summary>
    public string Avatar { get; set; } = string.Empty;

    /// <summary>
    /// 是否认证用户
    /// </summary>
    public bool IsVerified { get; set; }

    /// <summary>
    /// 用户简介
    /// </summary>
    public string Description { get; set; } = string.Empty;
}
/// <summary>
/// 搜索参数信息
/// 记录本次搜索使用的所有参数
/// </summary>
/// <param name="Keyword">搜索关键词</param>
/// <param name="SortBy">排序方式</param>
/// <param name="NoteType">笔记类型</param>
/// <param name="PublishTime">发布时间</param>
/// <param name="MaxResults">最大结果数</param>
/// <param name="RequestedAt">请求时间</param>
public record SearchParametersInfo(
    string Keyword,
    string SortBy,
    string NoteType,
    string PublishTime,
    int MaxResults,
    DateTime RequestedAt
);
/// <summary>
/// 搜索API监听器配置
/// 用于配置网络请求监听的参数
/// </summary>
/// <summary>
/// 搜索监听器配置类（继承自BaseMonitorConfig）
/// </summary>
public class SearchMonitorConfig : BaseMonitorConfig
{
    /// <summary>
    /// 分页等待时间（毫秒）
    /// </summary>
    public int PageWaitMs { get; set; } = 2000;

    public SearchMonitorConfig()
    {
        ApiUrlPattern = "https://edith.xiaohongshu.com/api/sns/web/v1/search/notes";
        TimeoutMs = 15000;
    }
}
#endregion
#region MCP工具强类型返回值定义
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

/// <summary>
/// 组合交互结果（点赞/收藏可组合执行的统一返回）。
/// - Success：当且仅当所有被请求的交互均成功时为 true。
/// - Like/Favorite：分别为具体交互结果，未请求则为 null。
/// </summary>
public record InteractionBundleResult(
    bool Success,
    InteractionResult? Like,
    InteractionResult? Favorite,
    string Message,
    string? ErrorCode = null
);
#endregion
#region 推荐列表数据模型
/// <summary>
/// 推荐列表结果 - 集成统计分析和导出功能
/// 参考 SearchResult 的设计模式
/// </summary>
/// <param name="Notes">推荐的笔记列表</param>
/// <param name="TotalCollected">总收集数量</param>
/// <param name="RequestCount">API请求次数</param>
/// <param name="Duration">收集总耗时</param>
/// <param name="Statistics">推荐统计数据</param>
/// <param name="ExportInfo">导出信息（如果启用自动导出）</param>
/// <param name="CollectionDetails">收集过程详细信息</param>
public record RecommendListResult(
    List<NoteInfo> Notes,
    int TotalCollected,
    int RequestCount,
    TimeSpan Duration,
    RecommendStatistics Statistics,
    SimpleExportInfo? ExportInfo = null,
    RecommendCollectionDetails? CollectionDetails = null
);
/// <summary>
/// 推荐统计数据
/// 基于 SearchStatistics 的设计模式
/// </summary>
/// <param name="VideoNotesCount">视频笔记数量</param>
/// <param name="ImageNotesCount">图文笔记数量</param>
/// <param name="AverageLikes">平均点赞数</param>
/// <param name="AverageComments">平均评论数</param>
/// <param name="AverageCollects">平均收藏数</param>
/// <param name="TopCategories">热门分类统计</param>
/// <param name="AuthorDistribution">作者分布统计</param>
/// <param name="CalculatedAt">统计计算时间</param>
public record RecommendStatistics(
    int VideoNotesCount,
    int ImageNotesCount,
    double AverageLikes,
    double AverageComments,
    double AverageCollects,
    Dictionary<string, int> TopCategories,
    Dictionary<string, int> AuthorDistribution,
    DateTime CalculatedAt
);
/// <summary>
/// 推荐收集详细信息
/// </summary>
/// <param name="InterceptedRequests">监听的请求数量</param>
/// <param name="SuccessfulRequests">成功处理的请求数量</param>
/// <param name="FailedRequests">失败的请求数量</param>
/// <param name="ScrollOperations">滚动操作次数</param>
/// <param name="AverageScrollDelay">平均滚动延时（毫秒）</param>
/// <param name="DataQuality">数据质量评估</param>
/// <param name="CollectionMode">收集模式</param>
public record RecommendCollectionDetails(
    int InterceptedRequests,
    int SuccessfulRequests,
    int FailedRequests,
    int ScrollOperations,
    double AverageScrollDelay,
    DataQuality DataQuality,
    RecommendCollectionMode CollectionMode
);
/// <summary>
/// 推荐收集模式
/// </summary>
public enum RecommendCollectionMode
{
    /// <summary>快速收集：最小延时，适用于小量数据</summary>
    Fast,
    /// <summary>标准收集：平衡性能和防检测</summary>
    Standard,
    /// <summary>谨慎收集：最大防检测，适用于大量数据</summary>
    Careful
}
/// <summary>
/// 网络监听器配置
/// </summary>
/// <summary>
/// 基础监听器配置类，包含所有配置类的共同字段
/// </summary>
public abstract class BaseMonitorConfig
{
    /// <summary>
    /// API URL 模式
    /// </summary>
    public string ApiUrlPattern { get; set; } = string.Empty;

    /// <summary>
    /// 请求超时时间（毫秒）
    /// </summary>
    public int TimeoutMs { get; set; } = 15000;

    /// <summary>
    /// 最大重试次数
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// 是否记录详细日志
    /// </summary>
    public bool EnableDetailedLogging { get; set; } = true;
}
/// <summary>
/// 通用监听器配置类（继承自BaseMonitorConfig）
/// </summary>
public class MonitorConfig : BaseMonitorConfig
{
    /// <summary>
    /// 目标URL模式
    /// </summary>
    public string UrlPattern { get; set; } = "https://edith.xiaohongshu.com/api/sns/web/v1/homefeed";

    /// <summary>
    /// 是否启用缓存
    /// </summary>
    public bool EnableCaching { get; set; } = true;

    /// <summary>
    /// 最大缓存大小
    /// </summary>
    public int MaxCacheSize { get; set; } = 1000;

    /// <summary>
    /// 请求超时时间
    /// </summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(15);

    public MonitorConfig()
    {
        ApiUrlPattern = UrlPattern;
        TimeoutMs = 10000;
    }
}
#endregion
#region 小红书搜索API真实响应数据模型（用于拟人化操作+API监听）
/// <summary>
/// 小红书搜索API响应的根级数据结构
/// 对应 /api/sns/web/v1/search/notes 接口的真实响应格式
/// 用于拟人化操作与API监听结合的推荐笔记获取
/// </summary>
public class SearchNotesApiResponse
{
    /// <summary>
    /// 响应状态码，0表示成功
    /// </summary>
    public int Code { get; set; }

    /// <summary>
    /// 响应消息
    /// </summary>
    public string Msg { get; set; } = string.Empty;

    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 搜索数据
    /// </summary>
    public SearchNotesData? Data { get; set; }
}
/// <summary>
/// 搜索笔记API数据部分
/// </summary>
public class SearchNotesData
{
    /// <summary>
    /// 笔记列表
    /// </summary>
    public List<SearchNoteItem> Items { get; set; } = [];

    /// <summary>
    /// 是否有更多数据
    /// </summary>
    public bool HasMore { get; set; }

    /// <summary>
    /// 游标，用于分页
    /// </summary>
    public string? Cursor { get; set; }

    /// <summary>
    /// 分页令牌
    /// </summary>
    public string? PageToken { get; set; }

    /// <summary>
    /// 搜索ID，用于跟踪
    /// </summary>
    public string? SearchId { get; set; }
}
/// <summary>
/// 搜索笔记项目数据
/// </summary>
public class SearchNoteItem
{
    /// <summary>
    /// 笔记ID
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 笔记类型（normal、video等）
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// 笔记基础信息
    /// </summary>
    public SearchNoteInfo? NoteCard { get; set; }

    /// <summary>
    /// 跟踪ID
    /// </summary>
    public string? TrackId { get; set; }

    /// <summary>
    /// 安全令牌
    /// </summary>
    public string? XsecToken { get; set; }

    /// <summary>
    /// 显示标记
    /// </summary>
    public Dictionary<string, object> DisplayTags { get; set; } = new();
}
/// <summary>
/// 搜索笔记基础信息
/// </summary>
public class SearchNoteInfo
{
    /// <summary>
    /// 显示标题
    /// </summary>
    public string DisplayTitle { get; set; } = string.Empty;

    /// <summary>
    /// 笔记描述
    /// </summary>
    public string Desc { get; set; } = string.Empty;

    /// <summary>
    /// 封面信息
    /// </summary>
    public SearchCoverInfo? Cover { get; set; }

    /// <summary>
    /// 用户信息
    /// </summary>
    public SearchUserInfo? User { get; set; }

    /// <summary>
    /// 互动信息
    /// </summary>
    public SearchInteractInfo? InteractInfo { get; set; }

    /// <summary>
    /// 视频信息（如果是视频笔记）
    /// </summary>
    public SearchVideoInfo? Video { get; set; }

    /// <summary>
    /// 笔记类型
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// 图片列表信息
    /// </summary>
    public List<SearchImageInfo> ImageList { get; set; } = [];
}
/// <summary>
/// 搜索封面信息
/// </summary>
public class SearchCoverInfo
{
    /// <summary>
    /// 封面URL
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// 默认URL
    /// </summary>
    public string UrlDefault { get; set; } = string.Empty;

    /// <summary>
    /// 预览URL
    /// </summary>
    public string UrlPre { get; set; } = string.Empty;

    /// <summary>
    /// 图片宽度
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// 图片高度
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// 文件ID
    /// </summary>
    public string FileId { get; set; } = string.Empty;

    /// <summary>
    /// 图片场景列表
    /// </summary>
    public List<SearchImageSceneInfo> InfoList { get; set; } = [];
}
/// <summary>
/// 搜索图片场景信息
/// </summary>
public class SearchImageSceneInfo
{
    /// <summary>
    /// 图片场景标识
    /// </summary>
    public string ImageScene { get; set; } = string.Empty;

    /// <summary>
    /// 场景对应的URL
    /// </summary>
    public string Url { get; set; } = string.Empty;
}
/// <summary>
/// 搜索用户信息
/// </summary>
/// <summary>
/// 搜索用户信息（继承自BaseUserInfo）
/// </summary>
public class SearchUserInfo : BaseUserInfo
{
    /// <summary>
    /// 是否认证用户
    /// </summary>
    public bool Verified { get; set; }
}
/// <summary>
/// 搜索互动信息
/// </summary>
/// <summary>
/// 搜索结果的交互信息（继承自BaseInteractInfo）
/// </summary>
public class SearchInteractInfo : BaseInteractInfo
{
    /// <summary>
    /// 点赞数（字符串格式，用于处理API返回）
    /// </summary>
    public string LikedCountRaw { get; set; } = "0";

    /// <summary>
    /// 分享数
    /// </summary>
    public int ShareCount { get; set; }
}
/// <summary>
/// 搜索视频信息
/// </summary>
public class SearchVideoInfo
{
    /// <summary>
    /// 视频时长（秒）
    /// </summary>
    public int Duration { get; set; }

    /// <summary>
    /// 视频封面URL
    /// </summary>
    public string Cover { get; set; } = string.Empty;

    /// <summary>
    /// 视频URL
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// 视频宽度
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// 视频高度
    /// </summary>
    public int Height { get; set; }
}
/// <summary>
/// 搜索图片信息
/// </summary>
public class SearchImageInfo
{
    /// <summary>
    /// 图片URL
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// 图片宽度
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// 图片高度
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// 图片场景信息列表
    /// </summary>
    public List<SearchImageSceneInfo> InfoList { get; set; } = [];
}
#endregion
#region 小红书推荐API真实响应数据模型
/// <summary>
/// 小红书推荐API响应的根级数据结构
/// 对应 /api/sns/web/v1/homefeed 接口的真实响应格式
/// </summary>
/// <param name="Code">响应状态码</param>
/// <param name="Data">响应数据</param>
/// <param name="Msg">响应消息</param>
/// <param name="Success">是否成功</param>
public record HomefeedResponse(
    [property: JsonPropertyName("code")] int Code,
    [property: JsonPropertyName("data")] HomefeedData? Data,
    [property: JsonPropertyName("msg")] string Msg,
    [property: JsonPropertyName("success")] bool Success
);
/// <summary>
/// 推荐API数据部分
/// </summary>
/// <param name="CursorScore">分页游标标识</param>
/// <param name="Items">推荐项目列表</param>
public record HomefeedData(
    [property: JsonPropertyName("cursor_score")] string CursorScore,
    [property: JsonPropertyName("items")] List<HomefeedItem> Items
);
/// <summary>
/// 单个推荐项目数据
/// </summary>
/// <param name="Id">项目ID</param>
/// <param name="Ignore">是否忽略</param>
/// <param name="ModelType">模型类型，通常为"note"</param>
/// <param name="NoteCard">笔记卡片数据</param>
/// <param name="TrackId">跟踪ID，用于分析</param>
/// <param name="XsecToken">安全令牌</param>
public record HomefeedItem(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("ignore")] bool Ignore,
    [property: JsonPropertyName("model_type")] string ModelType,
    [property: JsonPropertyName("note_card")] NoteCard? NoteCard,
    [property: JsonPropertyName("track_id")] string? TrackId,
    [property: JsonPropertyName("xsec_token")] string? XsecToken
);
/// <summary>
/// 笔记卡片核心数据
/// </summary>
/// <param name="User">用户信息</param>
/// <param name="Cover">封面图片信息</param>
/// <param name="DisplayTitle">显示标题</param>
/// <param name="InteractInfo">交互信息</param>
/// <param name="Type">笔记类型，如"normal"、"video"</param>
/// <param name="Video">视频信息（视频笔记专用）</param>
/// <param name="NoteId">笔记ID</param>
public record NoteCard(
    [property: JsonPropertyName("user")] UserCard User,
    [property: JsonPropertyName("cover")] CoverInfo Cover,
    [property: JsonPropertyName("display_title")] string DisplayTitle,
    [property: JsonPropertyName("interact_info")] InteractInfo InteractInfo,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("video")] VideoInfo? Video = null,
    [property: JsonPropertyName("note_id")] string? NoteId = null
);
/// <summary>
/// 视频信息 - 视频笔记专用数据模型
/// </summary>
/// <param name="Capa">视频能力信息，包含时长等元数据</param>
public record VideoInfo(
    [property: JsonPropertyName("capa")] VideoCapa Capa
);
/// <summary>
/// 视频能力信息 - 包含视频时长等元数据
/// </summary>
/// <param name="Duration">视频时长（秒）</param>
public record VideoCapa(
    [property: JsonPropertyName("duration")] int Duration
);
/// <summary>
/// 用户卡片信息
/// </summary>
/// <param name="Nickname">昵称</param>
/// <param name="UserId">用户ID</param>
/// <param name="XsecToken">用户安全令牌</param>
/// <param name="Avatar">头像URL</param>
public record UserCard(
    [property: JsonPropertyName("nickname")] string Nickname,
    [property: JsonPropertyName("user_id")] string UserId,
    [property: JsonPropertyName("xsec_token")] string? XsecToken,
    [property: JsonPropertyName("avatar")] string Avatar
)
{
    // 兼容另一种字段名：nick_name
    [JsonPropertyName("nick_name")]
    public string? NicknameAlt { get; init; }
}
/// <summary>
/// 封面图片信息
/// </summary>
/// <param name="InfoList">图片信息列表，包含不同场景的图片</param>
/// <param name="Url">默认URL（通常为空）</param>
/// <param name="UrlDefault">默认URL</param>
/// <param name="UrlPre">预览URL</param>
/// <param name="Width">图片宽度</param>
/// <param name="Height">图片高度</param>
/// <param name="FileId">文件ID</param>
public record CoverInfo(
    [property: JsonPropertyName("info_list")] List<ImageInfo> InfoList,
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("url_default")] string UrlDefault,
    [property: JsonPropertyName("url_pre")] string UrlPre,
    [property: JsonPropertyName("width")] int Width,
    [property: JsonPropertyName("height")] int Height,
    [property: JsonPropertyName("file_id")] string FileId
);
/// <summary>
/// 图片信息，包含不同场景的URL
/// </summary>
/// <param name="ImageScene">图片场景，如"WB_PRV"、"WB_DFT"</param>
/// <param name="Url">图片URL</param>
public record ImageInfo(
    [property: JsonPropertyName("image_scene")] string ImageScene,
    [property: JsonPropertyName("url")] string Url
);
/// <summary>
/// 基础交互信息类，包含所有交互信息的共同字段
/// </summary>
public abstract class BaseInteractInfo
{
    /// <summary>
    /// 点赞数
    /// </summary>
    public int LikedCount { get; set; }

    /// <summary>
    /// 评论数
    /// </summary>
    public int CommentCount { get; set; }

    /// <summary>
    /// 收藏数
    /// </summary>
    public int CollectedCount { get; set; }

    /// <summary>
    /// 是否已点赞
    /// </summary>
    public bool Liked { get; set; }

    /// <summary>
    /// 是否已收藏
    /// </summary>
    public bool Collected { get; set; }
}
public record InteractInfo(
    [property: JsonPropertyName("liked")] bool Liked,
    [property: JsonPropertyName("liked_count")] string LikedCount
);
#endregion
#region 数据转换器和映射逻辑
/// <summary>
/// 推荐API响应到NoteInfo的转换器
/// 处理真实API数据到现有模型的映射
/// </summary>
public static class HomefeedConverter
{
    private static bool TryParseCount(string? raw, out int value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(raw)) return false;

        raw = raw.Trim();

        // 处理中文“万”/“亿”以及带小数的形式，如 "1.5万"
        try
        {
            if (raw.EndsWith("万", StringComparison.Ordinal))
            {
                if (double.TryParse(raw.TrimEnd('万'), out var num))
                {
                    value = (int)Math.Round(num * 10000);
                    return true;
                }
                return false;
            }
            if (raw.EndsWith("亿", StringComparison.Ordinal))
            {
                if (double.TryParse(raw.TrimEnd('亿'), out var num))
                {
                    value = (int)Math.Round(num * 100000000);
                    return true;
                }
                return false;
            }

            // 纯数字
            if (int.TryParse(raw, out var n))
            {
                value = n;
                return true;
            }
        }
        catch
        {
            // 忽略解析异常
        }
        return false;
    }

    /// <summary>
    /// 将单个推荐项目转换为NoteInfo - 增强视频数据支持
    /// </summary>
    /// <param name="item">推荐项目数据</param>
    /// <returns>转换后的笔记信息</returns>
    public static NoteInfo? ConvertToNoteInfo(HomefeedItem item)
    {
        if (item.NoteCard == null)
            return null;

        try
        {
            var noteCard = item.NoteCard;
            var missingFields = new List<string>();

            // 创建NoteInfo实例
            // 选取昵称（nickname 或 nick_name）
            var authorName = !string.IsNullOrWhiteSpace(noteCard.User.Nickname)
                ? noteCard.User.Nickname
                : (noteCard.User.NicknameAlt ?? string.Empty);

            var noteInfo = new NoteInfo
            {
                Id = item.Id,
                Title = noteCard.DisplayTitle,
                Author = !string.IsNullOrWhiteSpace(authorName) ? authorName : "未知用户",
                AuthorId = noteCard.User.UserId,
                AuthorAvatar = noteCard.User.Avatar,
                Url = $"https://www.xiaohongshu.com/explore/{item.Id}",
                TrackId = item.TrackId,
                XsecToken = item.XsecToken,
                AuthorXsecToken = noteCard.User.XsecToken,
                ModelType = item.ModelType,
                ExtractedAt = DateTime.UtcNow
            };

            // 解析点赞数（增加空值保护）
            var likedRaw = noteCard.InteractInfo?.LikedCount;
            if (TryParseCount(likedRaw, out int likeCount))
            {
                noteInfo.LikeCount = likeCount;
            }
            else
            {
                missingFields.Add("LikeCount");
            }

            // 选择最佳封面图片
            noteInfo.CoverImage = SelectBestCoverImage(noteCard.Cover);

            // 映射封面结构
            noteInfo.CoverInfo = new RecommendedCoverInfo
            {
                DefaultUrl = noteCard.Cover.UrlDefault,
                PreviewUrl = noteCard.Cover.UrlPre,
                Width = noteCard.Cover.Width,
                Height = noteCard.Cover.Height,
                FileId = noteCard.Cover.FileId,
                Scenes = (noteCard.Cover.InfoList ?? new List<ImageInfo>())
                    .Select(i => new ImageSceneInfo {SceneType = i.ImageScene, Url = i.Url})
                    .ToList()
            };

            // 缺失字段标记
            if (noteInfo.CommentCount == null) missingFields.Add("CommentCount");
            if (noteInfo.FavoriteCount == null) missingFields.Add("FavoriteCount");
            if (noteInfo.PublishTime == null) missingFields.Add("PublishTime");

            // 设置数据质量
            noteInfo.Quality = missingFields.Count switch
            {
                0 => DataQuality.Complete,
                <= 2 => DataQuality.Partial,
                _ => DataQuality.Minimal
            };

            noteInfo.MissingFields = missingFields;

            // 根据可用信息推断笔记类型（增强的视频识别）
            noteInfo.Type = DetermineNoteType(noteCard);
            noteInfo.RawNoteType = noteCard.Type;

            // 视频时长（若能从推荐API拿到）
            if (noteCard.Video?.Capa.Duration > 0)
            {
                noteInfo.VideoDuration = noteCard.Video.Capa.Duration;
            }

            return noteInfo;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// 从封面信息中选择最佳图片URL
    /// 优先级：WB_DFT > WB_PRV > 第一个可用
    /// </summary>
    /// <param name="cover">封面信息</param>
    /// <returns>最佳图片URL</returns>
    private static string SelectBestCoverImage(CoverInfo cover)
    {
        if (cover.InfoList.Count == 0)
            return cover.UrlDefault;

        // 优先选择WB_DFT（默认场景）
        var defaultImage = cover.InfoList.FirstOrDefault(x => x.ImageScene == "WB_DFT");
        if (defaultImage != null && !string.IsNullOrEmpty(defaultImage.Url))
            return defaultImage.Url;

        // 其次选择WB_PRV（预览场景）
        var previewImage = cover.InfoList.FirstOrDefault(x => x.ImageScene == "WB_PRV");
        if (previewImage != null && !string.IsNullOrEmpty(previewImage.Url))
            return previewImage.Url;

        // 最后选择第一个可用的
        var firstAvailable = cover.InfoList.FirstOrDefault(x => !string.IsNullOrEmpty(x.Url));
        return firstAvailable?.Url ?? cover.UrlDefault;
    }

    /// <summary>
    /// 根据笔记卡片信息推断笔记类型 - 增强视频识别逻辑
    /// </summary>
    /// <param name="noteCard">笔记卡片</param>
    /// <returns>推断的笔记类型</returns>
    private static NoteType DetermineNoteType(NoteCard noteCard)
    {
        // 优先级1：检查type字段是否明确标识为video
        if (string.Equals(noteCard.Type, "video", StringComparison.OrdinalIgnoreCase))
        {
            return NoteType.Video;
        }

        // 优先级2：检查是否包含视频信息（最可靠）
        if (noteCard.Video?.Capa.Duration > 0)
        {
            return NoteType.Video;
        }

        // 优先级3：基于type字段的其他值判断
        if (string.Equals(noteCard.Type, "normal", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(noteCard.Type, "image", StringComparison.OrdinalIgnoreCase))
        {
            // 标准图文笔记
            if (noteCard.Cover.InfoList.Count != 0)
            {
                return NoteType.Image;
            }
        }

        // 优先级4：基于封面图片存在与否的后备判断
        if (noteCard.Cover.InfoList.Count != 0)
        {
            // 有封面图片，在无法确定具体类型时，默认为图文笔记
            return NoteType.Image;
        }

        // 无法确定类型时返回未知
        return NoteType.Unknown;
    }

}
#endregion
#region 页面加载等待策略接口和配置
/// <summary>
/// 页面加载等待策略服务接口
/// 提供多级等待策略，解决WaitForLoadStateAsync硬编码超时问题
/// </summary>
public interface IPageLoadWaitService
{
    /// <summary>
    /// 执行多级页面加载等待策略
    /// 按照 DOMContentLoaded → Load → NetworkIdle 的顺序依次尝试
    /// 支持智能降级和重试机制
    /// </summary>
    /// <param name="page">浏览器页面实例</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>等待策略执行结果</returns>
    Task<PageLoadWaitResult> WaitForPageLoadAsync(IPage page, CancellationToken cancellationToken = default);

    /// <summary>
    /// 执行指定的单一等待策略
    /// </summary>
    /// <param name="page">浏览器页面实例</param>
    /// <param name="strategy">等待策略类型</param>
    /// <param name="timeout">自定义超时时间（可选）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>等待策略执行结果</returns>
    Task<PageLoadWaitResult> WaitForPageLoadAsync(IPage page, PageLoadStrategy strategy, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 快速模式页面加载等待
    /// 仅使用DOMContentLoaded策略，适用于轻量级页面或性能要求较高的场景
    /// </summary>
    /// <param name="page">浏览器页面实例</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>等待策略执行结果</returns>
    Task<PageLoadWaitResult> WaitForPageLoadFastAsync(IPage page, CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查页面是否正在加载
    /// </summary>
    /// <param name="page">浏览器页面实例</param>
    /// <returns>页面是否正在加载</returns>
    Task<bool> IsPageLoadingAsync(IPage page);

    /// <summary>
    /// 等待页面加载完成
    /// </summary>
    /// <param name="page">浏览器页面实例</param>
    /// <param name="timeout">超时时间</param>
    /// <returns>是否等待成功</returns>
    Task<bool> WaitForLoadCompleteAsync(IPage page, TimeSpan timeout);
}
/// <summary>
/// 页面加载等待策略枚举
/// 定义不同级别的页面加载完成标准
/// </summary>
public enum PageLoadStrategy
{
    /// <summary>DOM内容加载完成，最快的等待策略</summary>
    DOMContentLoaded,

    /// <summary>页面完全加载（包括图片、样式表等），平衡策略</summary>
    Load,

    /// <summary>网络空闲状态，最严格的等待策略</summary>
    NetworkIdle
}
/// <summary>
/// 页面加载等待配置类
/// 提供所有等待策略的超时时间和重试配置
/// </summary>
public class PageLoadWaitConfig
{
    /// <summary>
    /// DOMContentLoaded 超时时间（毫秒）
    /// </summary>
    public int DOMContentLoadedTimeout { get; set; } = 15000;

    /// <summary>
    /// Load 超时时间（毫秒）
    /// </summary>
    public int LoadTimeout { get; set; } = 30000;

    /// <summary>
    /// NetworkIdle 超时时间（毫秒）
    /// </summary>
    public int NetworkIdleTimeout { get; set; } = 60000;

    /// <summary>
    /// 最大重试次数
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// 重试间隔时间（毫秒）
    /// </summary>
    public int RetryDelayMs { get; set; } = 2000;

    /// <summary>
    /// 是否启用智能降级机制
    /// 当高级策略失败时，自动降级到较低级别的策略
    /// </summary>
    public bool EnableDegradation { get; set; } = true;

    /// <summary>
    /// 快速模式超时时间（毫秒）
    /// 用于WaitForPageLoadFastAsync方法
    /// </summary>
    public int FastModeTimeout { get; set; } = 10000;

    /// <summary>
    /// 自定义验证超时时间（毫秒）
    /// 用于WaitForPageLoadWithValidationAsync方法
    /// </summary>
    public int CustomValidationTimeout { get; set; } = 5000;
}
/// <summary>
/// 页面加载等待结果
/// 包含等待策略执行的详细信息和结果
/// </summary>
public class PageLoadWaitResult
{
    /// <summary>
    /// 等待操作是否成功
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 实际使用的等待策略
    /// </summary>
    public PageLoadStrategy UsedStrategy { get; set; }

    /// <summary>
    /// 总耗时
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// 重试次数
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// 是否发生了策略降级
    /// </summary>
    public bool WasDegraded { get; set; }

    /// <summary>
    /// 错误信息（如果失败）
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 错误类型
    /// </summary>
    public ErrorType ErrorType { get; set; } = ErrorType.Unknown;

    /// <summary>
    /// 详细的执行日志
    /// </summary>
    public List<string> ExecutionLog { get; set; } = [];

    /// <summary>
    /// 自定义验证结果（如果使用了自定义验证）
    /// </summary>
    public bool? CustomValidationResult { get; set; }

    /// <summary>
    /// 创建成功结果
    /// </summary>
    /// <param name="strategy">使用的策略</param>
    /// <param name="duration">耗时</param>
    /// <param name="retryCount">重试次数</param>
    /// <param name="wasDegraded">是否降级</param>
    /// <returns>成功结果实例</returns>
    public static PageLoadWaitResult CreateSuccess(PageLoadStrategy strategy, TimeSpan duration, int retryCount = 0, bool wasDegraded = false)
    {
        return new PageLoadWaitResult
        {
            Success = true,
            UsedStrategy = strategy,
            Duration = duration,
            RetryCount = retryCount,
            WasDegraded = wasDegraded
        };
    }

    /// <summary>
    /// 创建失败结果
    /// </summary>
    /// <param name="errorMessage">错误信息</param>
    /// <param name="errorType">错误类型</param>
    /// <param name="duration">耗时</param>
    /// <param name="retryCount">重试次数</param>
    /// <returns>失败结果实例</returns>
    public static PageLoadWaitResult CreateFailure(string errorMessage, ErrorType errorType = ErrorType.Unknown, TimeSpan duration = default, int retryCount = 0)
    {
        return new PageLoadWaitResult
        {
            Success = false,
            ErrorMessage = errorMessage,
            ErrorType = errorType,
            Duration = duration,
            RetryCount = retryCount
        };
    }
}
#endregion
#region 通用API监听服务接口
/// <summary>
/// API端点类型枚举
/// </summary>
public enum ApiEndpointType
{
    Homefeed,    // 推荐笔记 /api/sns/web/v1/homefeed
    Feed,        // 笔记详情 /api/sns/web/v1/feed
    SearchNotes, // 搜索笔记 /api/sns/web/v1/search/notes
    Comments,    // 评论列表 /api/sns/web/v2/comment/page

    // ===== 互动动作端点（破坏性变更：新增并作为权威信号） =====
    LikeNote,      // 点赞 /api/sns/web/v1/note/like
    DislikeNote,   // 取消点赞 /api/sns/web/v1/note/dislike
    CollectNote,   // 收藏 /api/sns/web/v1/note/collect
    UncollectNote, // 取消收藏 /api/sns/web/v1/note/uncollect
    CommentPost,   // 发表评论 /api/sns/web/v1/comment/post
    CommentDelete  // 删除自己的评论 /api/sns/web/v1/comment/delete
}
/// <summary>
/// 通用API监听服务接口
/// </summary>
public interface IUniversalApiMonitor : IDisposable
{
    /// <summary>
    /// 设置通用API监听器
    /// </summary>
    /// <param name="page">浏览器页面实例</param>
    /// <param name="endpointsToMonitor">要监听的端点类型</param>
    /// <returns>设置是否成功</returns>
    bool SetupMonitor(IPage page, HashSet<ApiEndpointType> endpointsToMonitor);

    /// <summary>
    /// 等待指定端点的API响应
    /// </summary>
    /// <param name="endpointType">端点类型</param>
    /// <param name="expectedCount">期望响应数量</param>
    /// <returns>是否成功等待到响应</returns>
    Task<bool> WaitForResponsesAsync(ApiEndpointType endpointType, int expectedCount = 1);

    /// <summary>
    /// 在指定超时时间内等待指定端点的API响应。
    /// 用于覆盖全局 MCP 超时策略的场景（例如业务侧希望更快失败并重试）。
    /// </summary>
    /// <param name="endpointType">端点类型</param>
    /// <param name="timeout">本次等待的超时时间</param>
    /// <param name="expectedCount">期望响应数量</param>
    /// <returns>是否成功等待到响应</returns>
    Task<bool> WaitForResponsesAsync(ApiEndpointType endpointType, TimeSpan timeout, int expectedCount = 1);

    /// <summary>
    /// 获取指定端点监听到的笔记详情
    /// </summary>
    /// <param name="endpointType">端点类型</param>
    /// <returns>笔记详情列表</returns>
    List<NoteDetail> GetMonitoredNoteDetails(ApiEndpointType endpointType);

    /// <summary>
    /// 获取指定端点监听到的原始响应数据
    /// </summary>
    /// <param name="endpointType">端点类型</param>
    /// <returns>原始响应数据列表</returns>
    List<MonitoredApiResponse> GetRawResponses(ApiEndpointType endpointType);

    /// <summary>
    /// 清理指定端点的监听数据
    /// </summary>
    /// <param name="endpointType">端点类型，null表示清理所有端点</param>
    void ClearMonitoredData(ApiEndpointType? endpointType = null);

    /// <summary>
    /// 停止API监听
    /// </summary>
    Task StopMonitoringAsync();
}
/// <summary>
/// API触发结果
/// </summary>
public class ApiTriggerResult
{
    /// <summary>
    /// 是否成功触发API
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 错误消息（失败时）
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 获取到的数据项数量
    /// </summary>
    public int DataCount { get; set; }

    /// <summary>
    /// 操作耗时
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// 创建成功结果
    /// </summary>
    public static ApiTriggerResult CreateSuccess(int dataCount, TimeSpan duration)
    {
        return new ApiTriggerResult
        {
            Success = true,
            DataCount = dataCount,
            Duration = duration
        };
    }

    /// <summary>
    /// 创建失败结果
    /// </summary>
    public static ApiTriggerResult CreateFailure(string errorMessage, TimeSpan duration)
    {
        return new ApiTriggerResult
        {
            Success = false,
            ErrorMessage = errorMessage,
            DataCount = 0,
            Duration = duration
        };
    }
}
#endregion
