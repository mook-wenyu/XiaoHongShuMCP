using System.Text.RegularExpressions;
using System.Text.Json.Serialization;
using Microsoft.Playwright;
using HushOps.Core.Automation.Abstractions;

// 说明：命名空间迁移至 HushOps.Services。
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
    /// 批量查找笔记详情（纯监听强化版），聚焦统计指标与节奏表现。
    /// </summary>
    /// <param name="keyword">关键词（单字符串）</param>
    /// <param name="maxCount">最大查找数量</param>
    /// <param name="includeComments">是否包含评论数据</param>
    /// <returns>增强的批量笔记结果，聚焦统计分析与监听指标</returns>
    Task<OperationResult<BatchNoteResult>> BatchGetNoteDetailsAsync(
        string keyword,
        int maxCount = 10,
        bool includeComments = false);

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
    /// 统一交互：基于关键词定位并执行点赞/收藏（可组合，破坏式签名：枚举指令）。
    /// likeAction/favoriteAction 取值："do" / "cancel" / "none"
    /// </summary>
    Task<OperationResult<InteractionBundleResult>> InteractNoteAsync(string keyword, string likeAction, string favoriteAction);

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
    /// 执行搜索笔记功能——拟人化操作与API监听结合，输出结构化结果。
    /// </summary>
    /// <param name="keyword">搜索关键词</param>
    /// <param name="maxResults">最大结果数量，默认20</param>
    /// <param name="sortBy">排序方式：comprehensive(综合), latest(最新), most_liked(最多点赞)</param>
    /// <param name="noteType">笔记类型：all(不限), video(视频), image(图文)</param>
    /// <param name="publishTime">发布时间：all(不限), day(一天内), week(一周内), half_year(半年内)</param>
    /// <param name="includeAnalytics">是否包含数据分析，默认true</param>
    /// <returns>包含搜索结果与统计分析的增强搜索结果</returns>
    Task<OperationResult<SearchResult>> SearchNotesAsync(
        string keyword,
        int maxResults = 20,
        string sortBy = "comprehensive",
        string noteType = "all",
        string publishTime = "all",
        bool includeAnalytics = true);

    /// <summary>
    /// 获取推荐笔记（纯监听），维持动态编排的基础能力。
    /// </summary>
    /// <param name="limit">获取数量限制</param>
    /// <param name="timeout">超时时间</param>
    /// <returns>推荐结果</returns>
    Task<OperationResult<RecommendListResult>> GetRecommendedNotesAsync(int limit = 20, TimeSpan? timeout = null);

}

/// <summary>
/// 评论工作流接口：承载评论与草稿暂存的完整业务流程。
/// </summary>
public interface ICommentWorkflow
{
    /// <summary>
    /// 基于关键词执行拟人化评论。
    /// </summary>
    Task<OperationResult<CommentResult>> PostCommentAsync(string keyword, string content, CancellationToken ct = default);
}

public interface INoteEngagementWorkflow
{
    /// <summary>
    /// 基于关键词执行拟人化点赞与收藏组合操作，自动完成页面定位与网络反馈审计。
    /// </summary>
    Task<OperationResult<InteractionBundleResult>> InteractAsync(string keyword, bool like, bool favorite, CancellationToken ct = default);

    /// <summary>
    /// 基于关键词执行一次点赞操作。
    /// </summary>
    Task<OperationResult<InteractionResult>> LikeAsync(string keyword, CancellationToken ct = default);

    /// <summary>
    /// 基于关键词执行一次收藏操作。
    /// </summary>
    Task<OperationResult<InteractionResult>> FavoriteAsync(string keyword, CancellationToken ct = default);

    /// <summary>
    /// 基于关键词执行一次取消点赞操作。
    /// </summary>
    Task<OperationResult<InteractionResult>> UnlikeAsync(string keyword, CancellationToken ct = default);

    /// <summary>
    /// 基于关键词执行一次取消收藏操作。
    /// </summary>
    Task<OperationResult<InteractionResult>> UncollectAsync(string keyword, CancellationToken ct = default);
}

public interface INoteDiscoveryService
{
    /// <summary>
    /// 在推荐/搜索页基于关键词定位首个匹配的笔记元素。
    /// </summary>
    Task<IElementHandle?> FindMatchingNoteElementAsync(string keyword, CancellationToken ct = default);

    /// <summary>
    /// 通过虚拟化滚动在列表中收集指定数量的匹配笔记元素。
    /// </summary>
    Task<OperationResult<List<IElementHandle>>> FindVisibleMatchingNotesAsync(string keyword, int maxCount, CancellationToken ct = default);

    /// <summary>
    /// 判断当前详情页是否与给定关键词相匹配。
    /// </summary>
    Task<bool> DoesDetailMatchKeywordAsync(IPage page, string keyword, CancellationToken ct = default);
}

/// <summary>
/// 智能收集调度器接口：封装批量监听策略与节奏控制。
/// </summary>
public interface ISmartCollectionController
{
    /// <summary>
    /// 基于采集结果与性能指标生成可审计的收集输出。
    /// </summary>
    SmartCollectionResult ComposeResult(IReadOnlyList<NoteInfo> notes, int targetCount, CollectionPerformanceMetrics metrics);

    /// <summary>
    /// 根据节奏反馈调整后续批量收集策略（可选）。
    /// </summary>
    void RecordFeedback(SmartCollectionResult result);
}

/// <summary>
/// 页面守护接口：负责页面状态检测、等待与核心 DOM 校验。
/// </summary>
public interface IPageGuardian
{
    /// <summary>
    /// 检测当前页面状态并返回结构化信息。
    /// </summary>
    Task<PageStatusInfo> InspectAsync(IPage page, PageType expectedType, CancellationToken ct = default);

    /// <summary>
    /// 激活评论区域并确保输入控件可用。
    /// </summary>
    Task<bool> EnsureCommentAreaReadyAsync(IPage page, CancellationToken ct = default);

    /// <summary>
    /// 基于别名等待定位器出现，用于后续交互。
    /// </summary>
    Task<bool> WaitForLocatorAsync(IAutoPage autoPage, string alias, TimeSpan timeout, CancellationToken ct = default);
}

/// <summary>
/// 拟人化交互执行接口：封装输入、点击等细节。
/// </summary>
public interface IInteractionExecutor
{
    /// <summary>
    /// 在指定页面输入评论草稿内容，并处理表情/标签等增强要素。
    /// </summary>
    Task InputCommentAsync(IAutoPage page, CommentDraft draft, CancellationToken ct = default);

    /// <summary>
    /// 点击评论发布按钮并根据策略等待提交流程。
    /// </summary>
    Task<InteractionSubmitResult> SubmitCommentAsync(IAutoPage page, CancellationToken ct = default);
}

/// <summary>
/// 反馈协调接口：负责 API 监听、审计与结果汇总。
/// </summary>
public interface IFeedbackCoordinator
{
    /// <summary>
    /// 针对页面初始化监控上下文，并清理历史数据。
    /// </summary>
    void Initialize(IPage page, IReadOnlyCollection<ApiEndpointType> endpoints);

    /// <summary>
    /// 清理指定 Endpoint 的历史响应。
    /// </summary>
    void Reset(ApiEndpointType endpoint);

    /// <summary>
    /// 启动监测并等待指定 Endpoint 的反馈。
    /// </summary>
    Task<ApiFeedback> ObserveAsync(ApiEndpointType endpoint, CancellationToken ct = default);

    /// <summary>
    /// 记录一次交互审计事件。
    /// </summary>
    void Audit(string operation, string keyword, FeedbackContext context);
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
    /// 等待直到检测到已登录或超时。
    /// 建议在已调用 ConnectToBrowserAsync（未登录时会自动显示浏览器）之后调用。
    /// </summary>
    /// <param name="maxWait">最大等待时长</param>
    /// <param name="pollInterval">轮询间隔</param>
    /// <param name="ct">取消令牌</param>
    Task<bool> WaitUntilLoggedInAsync(TimeSpan maxWait, TimeSpan pollInterval, CancellationToken ct = default);

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
    /// 获取抽象页面（IAutoPage）。
    /// 说明：用于逐步迁移到自动化抽象层，平台层不应直接依赖 Playwright 类型。
    /// </summary>
    Task<HushOps.Core.Automation.Abstractions.IAutoPage> GetAutoPageAsync();

    /// <summary>
    /// 获取 Playwright 页面实例（兼容旧流程）。
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

    /// <summary>
    /// 页面探测：打开指定 URL 并按选择器别名验证元素可达性，返回 HTML 采样与别名明细。
    /// 仅读取页面与查询 DOM，不进行写操作；用于真实环境下修正选择器与结构变更。
    /// </summary>
    /// <param name="url">要打开的 URL；为空则使用探索页。</param>
    /// <param name="aliases">要探测的别名集合；为空则使用内置关键别名集（探索/搜索/互动）。</param>
    /// <param name="maxHtmlKb">HTML 采样最大大小（KB）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<PageProbeResult> ProbePageAsync(string? url = null, List<string>? aliases = null, int maxHtmlKb = 64, CancellationToken cancellationToken = default);

    /// <summary>
    /// 确保以可视化（Headful）方式显示浏览器以便登录。
    /// - 若当前为无头模式（Headless=true），将自动以相同用户数据目录重启为可视化模式；
    /// - 若已为可视化模式，则仅确保打开目标 URL。
    /// </summary>
    /// <param name="url">可选：打开的初始页面，默认探索页。</param>
    /// <returns>是否成功显示或重启为可视化模式。</returns>
    Task<bool> EnsureHeadfulForLoginAsync(string? url = null);

    /// <summary>
    /// 维持浏览器会话健康，执行 Cookie 续期或上下文轮换等维护操作。
    /// 默认实现为空；具体行为由实现类决定。
    /// </summary>
    Task EnsureSessionFreshAsync(CancellationToken ct = default) => Task.CompletedTask;
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
    /// 基于别名创建 Playwright Locator（若实现未提供则返回 null）。
    /// </summary>
    ILocator? CreateLocator(IPage page, string alias, PageState pageState = PageState.Auto);

    /// <summary>
    /// 获取所有选择器配置
    /// </summary>
    Dictionary<string, List<string>> GetAllSelectors();

    /// <summary>
    /// 检测当前页面状态
    /// </summary>
    Task<PageState> DetectPageStateAsync(IAutoPage page);

    /// <summary>
    /// 基于“模板别名”和占位符字典构建动态选择器列表。
    /// 约定：模板占位符使用大括号，如 {type}、{value}，并支持 {nameLower} 自动转小写。
    /// </summary>
    /// <param name="templateAlias">模板别名（由实现维护）。</param>
    /// <param name="tokens">占位符字典，如 {"type": "综合"} 或 {"value": "一周内"}。</param>
    /// <returns>展开后的选择器列表（按优先级排序）。</returns>
    List<string> BuildSelectors(string templateAlias, IDictionary<string, string> tokens);

    /// <summary>
    /// 尝试按给定顺序重排某别名的选择器列表（仅当完全覆盖原集合且不含未知条目时生效）。
    /// </summary>
    bool TryReorderSelectors(string alias, IEnumerable<string> newOrder);
}
/// <summary>
/// 延时管理器接口
/// 统一管理所有类型的拟人化延时
/// </summary>
// 破坏性迁移：IDelayManager 已下沉至 HushOps.Core.Humanization.IDelayManager。
/// <summary>
/// 元素查找器接口
/// 统一处理元素查找和重试逻辑
/// </summary>
public interface IElementFinder
{
    /// <summary>
    /// 查找元素（抽象页面/元素），支持重试与超时。
    /// </summary>
    Task<IAutoElement?> FindElementAsync(IAutoPage page, string selectorAlias, int retries = 3, int timeout = 3000);

    /// <summary>
    /// 使用指定候选选择器集合查找元素（支持遥测别名）。
    /// </summary>
    Task<IAutoElement?> FindElementAsync(IAutoPage page, IEnumerable<string> selectors, string telemetryAlias, int retries = 3, int timeout = 3000);

    /// <summary>
    /// 批量查找元素。
    /// </summary>
    Task<List<IAutoElement>> FindElementsAsync(IAutoPage page, string selectorAlias, int timeout = 3000);

    /// <summary>
    /// 等待元素可见。
    /// </summary>
    Task<bool> WaitForElementVisibleAsync(IAutoPage page, string selectorAlias, int timeout = 3000);
}

/// <summary>
/// 选择器遥测接口：记录别名-候选选择器在运行期的命中率/耗时/命中顺序，并支持基于历史统计的优先级重排。
/// </summary>
// 破坏性迁移：ISelectorTelemetry 已下沉至 HushOps.Core.Selectors.ISelectorTelemetry。

/// <summary>
/// 单个选择器统计信息（只读快照）。
/// </summary>
// 破坏性迁移：SelectorStat 已下沉至 HushOps.Core.Selectors.SelectorStat。
/// <summary>
/// 文本输入策略接口
/// 定义不同类型元素的输入策略
/// </summary>
public interface ITextInputStrategy
{
    /// <summary>
    /// 检查策略是否适用于指定元素（抽象元素）。
    /// </summary>
    Task<bool> IsApplicableAsync(IAutoElement element);

    /// <summary>
    /// 执行文本输入（抽象页面 + 抽象元素）。
    /// </summary>
    Task InputTextAsync(IAutoPage page, IAutoElement element, string text);
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
    Task HumanClickAsync(IAutoElement element);

    

    /// <summary>
    /// 模拟真人输入操作（抽象页面）。
    /// </summary>
    Task HumanTypeAsync(HushOps.Core.Automation.Abstractions.IAutoPage page, string selectorAlias, string text);

    /// <summary>
    /// 抽象页面与元素组合输入（强类型人性化输入入口）。
    /// </summary>
    Task InputTextAsync(IAutoPage page, IAutoElement element, string text);

    /// <summary>
    /// 模拟真人滚动操作（抽象页面）。
    /// </summary>
    Task HumanScrollAsync(HushOps.Core.Automation.Abstractions.IAutoPage page, CancellationToken cancellationToken = default);

    /// <summary>
    /// 参数化的人性化滚动操作（抽象页面）。
    /// </summary>
    Task HumanScrollAsync(HushOps.Core.Automation.Abstractions.IAutoPage page, int targetDistance, bool waitForLoad = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// 模拟真人悬停操作
    /// </summary>
    Task HumanHoverAsync(string selectorAlias);

    /// <summary>
    /// 模拟真人悬停操作（直接传入元素）
    /// </summary>
    Task HumanHoverAsync(IAutoElement element);

    /// <summary>
    /// 查找元素，支持重试、多选择器和自定义超时
    /// </summary>
    Task<IAutoElement?> FindElementAsync(IAutoPage page, string selectorAlias, int retries = 3, int timeout = 3000);

    /// <summary>
    /// 查找元素，支持页面状态感知
    /// </summary>
    Task<IAutoElement?> FindElementAsync(IAutoPage page, string selectorAlias, PageState pageState, int retries = 3, int timeout = 3000, CancellationToken cancellationToken = default);

    /// <summary>
    /// 统一的拟人化等待控制方法
    /// </summary>
    Task HumanWaitAsync(HushOps.Core.Humanization.HumanWaitType waitType, CancellationToken cancellationToken = default);

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
    Task<InteractionResult> HumanUnlikeAsync(IAutoPage page);

    /// <summary>
    /// 拟人化收藏操作
    /// 检测当前收藏状态，执行收藏操作，并验证结果
    /// </summary>
    Task<InteractionResult> HumanFavoriteAsync(IAutoPage page);

    /// <summary>
    /// 拟人化“取消收藏”操作（新）
    /// 检测当前收藏状态，若已收藏则点击取消；若本就未收藏则返回成功（幂等）。
    /// </summary>
    Task<InteractionResult> HumanUnfavoriteAsync(IAutoPage page);

    // 破坏性变更：移除 Direct* 直击交互（已废弃）。仅保留拟人化交互接口。

    // 破坏性变更：已彻底移除 IPage 直连滚动相关接口，统一使用 IAutoPage 版本。
}

/// <summary>
/// 可点性检测器接口
/// - 输入：元素句柄
/// - 输出：可点性报告（可见性、尺寸、遮挡、pointer-events、视口内等）
/// 说明：仅做快速启发式检测，避免频繁点击无效元素；不做重型等待。
/// </summary>
// 破坏性迁移：IClickabilityDetector 已下沉至 HushOps.Core.Humanization.IClickabilityDetector。

/// <summary>
/// 拟人化点击策略接口
/// - 统一“点击前/后”的准备、回退与节奏；
/// - 兜底（受门控）：DOM dispatchEvent('click')（默认禁用）与坐标点击；
/// - 不负责业务级校验（如点赞后计数变化），仅保障“人能点到”。
/// </summary>
public interface IHumanizedClickPolicy
{
    Task<ClickDecision> ClickAsync(IAutoPage page, IAutoElement element, CancellationToken ct = default);
}

/// <summary>
/// DOM 预检器接口（源代码/ARIA/加载态等语义级就绪性检查）。
/// - 目标：在点击前进行“语义就绪”验证，避免对处于禁用/加载/占位的元素进行无效点击；
/// - 与 <see cref="IClickabilityDetector"/> 的关系：前者关注“语义与状态”（aria/disabled/loading/role），
///   后者关注“物理可点性”（可见/遮挡/尺寸/视口内/pointer-events）。二者互补。
/// </summary>
// 破坏性迁移：IDomPreflightInspector 已下沉至 HushOps.Core.Humanization.IDomPreflightInspector。

/// <summary>
/// DOM 预检报告
/// </summary>
// 破坏性迁移：DomPreflightReport 已下沉至 HushOps.Core.Humanization.DomPreflightReport。

/// <summary>
/// 可点性检测报告
/// </summary>
// 破坏性迁移：ClickabilityReport 已下沉至 HushOps.Core.Humanization.ClickabilityReport。

/// <summary>
/// 点击决策结果（用于审计与调优）。
/// </summary>
public sealed class ClickDecision
{
    /// <summary>是否点击成功（某一条路径奏效）</summary>
    public bool Success { get; set; }
    /// <summary>采用的路径：regular/dispatch/coordinate</summary>
    public string Path { get; set; } = string.Empty;
    /// <summary>尝试次数</summary>
    public int Attempts { get; set; }
    /// <summary>尝试的路径序列</summary>
    public List<string> StepsTried { get; set; } = new();
    /// <summary>DOM 预检是否就绪（用于审计）。</summary>
    public bool? PreflightReady { get; set; }
    /// <summary>DOM 预检原因（用于审计）。</summary>
    public string? PreflightReason { get; set; }
}

// =============== 并发与速率治理（新） ===============

/// <summary>
/// 操作种类：只读/写入（写入更敏感，需要强约束）。
/// </summary>
public enum OperationKind
{
    Read,
    Write
}

/// <summary>
/// 端点类别：用于速率限制与熔断分组。
/// </summary>
public enum EndpointCategory
{
    Like,
    Collect,
    Comment,
    Search,
    Feed
}

/// <summary>
/// 操作租约接口：通过 <see cref="IConcurrencyGovernor"/> 申请，确保在作用域内独占或受限并发。
/// </summary>
public interface IOperationLease : IAsyncDisposable
{
    string AccountId { get; }
    OperationKind Kind { get; }
    string ResourceKey { get; }
}

/// <summary>
/// 并发治理器：为每个账号与操作种类施加并发预算（写=1，读=2，默认值可配），并发出“操作租约”。
/// </summary>
public interface IConcurrencyGovernor
{
    Task<IOperationLease> AcquireAsync(OperationKind kind, string resourceKey, CancellationToken ct = default);
}

/// <summary>
/// 令牌桶速率限制器：按端点类别和账号粒度进行速率整形。
/// </summary>
public interface IRateLimiter
{
    Task AcquireAsync(EndpointCategory category, string accountId, CancellationToken ct = default);
}

/// <summary>
/// 令牌桶限流诊断接口：提供运行时快照（按 accountId:category 分区）。
/// </summary>
public interface IRateLimiterDiagnostics
{
    IReadOnlyDictionary<string, object> GetSnapshot();
}

/// <summary>
/// 熔断器：在错误密度高（如429/403/CAPTCHA）时打开断路，进入冷却期。
/// </summary>
public interface ICircuitBreaker
{
    bool IsOpen(string key);
    TimeSpan? RemainingOpen(string key);
    void RecordSuccess(string key);
    void RecordFailure(string key, string reasonCode);
}

/// <summary>
/// 熔断器诊断接口：提供运行时状态快照。
/// </summary>
public interface ICircuitBreakerDiagnostics
{
    IReadOnlyDictionary<string, object> GetSnapshot();
}

// =============== 上下文池（新） ===============

/// <summary>
/// 浏览器上下文租约：归还时不关闭底层浏览器，仅释放占用名额。
/// </summary>
public interface IContextLease : IAsyncDisposable
{
    IBrowserContext Context { get; }
    IAutoPage Page { get; }
}

/// <summary>
/// 浏览器上下文池：对上提供“获取 Page/Context 的作用域租约”，以便未来扩展为多上下文/多用户数据目录的真实池化。
/// </summary>
public interface IBrowserContextPool
{
    /// <summary>
    /// 按账户ID获取上下文/页面租约（多用户数据目录隔离）。
    /// - accountId：账户标识（必填，若为空将使用 "anonymous"）；
    /// - purpose：用途标签（仅审计/日志）；
    /// - 返回：独占页面的作用域租约（释放后页面归还到池）。
    /// </summary>
    Task<IContextLease> AcquireAsync(string accountId, string? purpose = null, CancellationToken ct = default);
}

// =============== 弱选择器治理（新） ===============

/// <summary>
/// 弱选择器治理计划与应用接口。
/// </summary>
public interface IWeakSelectorGovernor
{
    HushOps.Core.Selectors.WeakSelectorPlan BuildPlan(double successRateThreshold, long minAttempts);
    bool ApplyPlan(HushOps.Core.Selectors.WeakSelectorPlan plan);
}

/// <summary>
/// 弱选择器治理计划：按别名给出“应用前后顺序”和被降权的选择器集合。
/// </summary>
// 破坏性迁移：WeakSelectorPlan/Item 已下沉至 HushOps.Core.Selectors.

/// <summary>
/// 审计服务接口：将关键交互与证据以结构化方式落盘（.audit/*.json）。
/// </summary>
public interface IAuditService
{
    Task WriteAsync(InteractionAuditEvent evt, CancellationToken ct = default);
}

/// <summary>
/// 交互审计事件模型（简化版，包含最关键字段）。
/// </summary>
public sealed class InteractionAuditEvent
{
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public string Action { get; set; } = string.Empty; // 点赞/收藏/取消点赞/取消收藏/发表评论 等
    public string Keyword { get; set; } = string.Empty; // 触发关键字（若有）
    public bool DomVerified { get; set; }
    public bool ApiConfirmed { get; set; }
    public long DurationMs { get; set; }
    public string? Extra { get; set; }
    public string? ClickPath { get; set; } // regular/dispatch/coordinate，未知则为空
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
// 破坏性迁移：HumanWaitType 已下沉至 HushOps.Core.Humanization.HumanWaitType。
// 配置类迁移说明：DetailMatchConfig 已并入统一配置 XhsSettings.DetailMatchConfig（破坏性变更）。
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
    /// <summary>
    /// 封面详情（若可用，来自 note_card.image_list 首图）。
    /// </summary>
    public RecommendedCoverInfo? CoverInfo { get; set; }
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
    /// 推荐流交互信息快照（若可用，来自 note_card.interact_info）。
    /// </summary>
    public RecommendedInteractInfo? InteractInfo { get; set; }

    /// <summary>
    /// 页面令牌（若可用）
    /// </summary>
    public string? PageToken { get; set; }

    /// <summary>
    /// 搜索ID（若可用）
    /// </summary>
    public string? SearchId { get; set; }
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
    public RecommendedUserInfo? UserInfo { get; set; }
    public RecommendedVideoInfo? VideoInfo { get; set; }

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
/// 推荐流用户信息（精简版）。
/// </summary>
public class RecommendedUserInfo : BaseUserInfo
{
    /// <summary>是否为认证用户。</summary>
    public bool IsVerified { get; set; }

    /// <summary>用户简介。</summary>
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// 推荐流交互信息（提供字符串原始计数与数值化访问）。
/// </summary>
public class RecommendedInteractInfo : BaseInteractInfo
{
    /// <summary>点赞计数字符串（原始值）。</summary>
    public string LikedCountRaw { get; set; } = "0";

    /// <summary>评论计数字符串（原始值）。</summary>
    public string CommentCountRaw { get; set; } = "0";

    /// <summary>收藏计数字符串（原始值）。</summary>
    public string CollectedCountRaw { get; set; } = "0";

    /// <summary>分享计数字符串（原始值）。</summary>
    public string ShareCountRaw { get; set; } = "0";

    /// <summary>解析后的点赞数量。</summary>
    public override int LikeCount
    {
        get => ParseCount(LikedCountRaw, base.LikeCount);
        set
        {
            base.LikeCount = value;
            LikedCountRaw = value.ToString();
        }
    }

    /// <summary>解析后的评论数量。</summary>
    public override int CommentCount
    {
        get => ParseCount(CommentCountRaw, base.CommentCount);
        set
        {
            base.CommentCount = value;
            CommentCountRaw = value.ToString();
        }
    }

    /// <summary>解析后的收藏数量。</summary>
    public override int FavoriteCount
    {
        get => ParseCount(CollectedCountRaw, base.FavoriteCount);
        set
        {
            base.FavoriteCount = value;
            CollectedCountRaw = value.ToString();
        }
    }

    /// <summary>解析后的分享数量。</summary>
    public override int ShareCount
    {
        get => ParseCount(ShareCountRaw, base.ShareCount);
        set
        {
            base.ShareCount = value;
            ShareCountRaw = value.ToString();
        }
    }

    private static int ParseCount(string raw, int fallback)
    {
        if (string.IsNullOrWhiteSpace(raw)) return fallback;
        return int.TryParse(raw, out var parsed) ? parsed : fallback;
    }

    /// <summary>与旧模型字段保持兼容的点赞数量封装。</summary>
    public int LikedCount
    {
        get => LikeCount;
        set => LikeCount = value;
    }

    /// <summary>与旧模型字段保持兼容的收藏数量封装。</summary>
    public int CollectedCount
    {
        get => FavoriteCount;
        set => FavoriteCount = value;
    }
}

/// <summary>
/// 推荐流封面信息。
/// </summary>
public class RecommendedCoverInfo
{
    public string DefaultUrl { get; set; } = string.Empty;
    public string PreviewUrl { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public string FileId { get; set; } = string.Empty;
    public List<ImageSceneInfo> Scenes { get; set; } = [];
}

/// <summary>
/// 图片场景信息。
/// </summary>
public class ImageSceneInfo
{
    public string SceneType { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}

/// <summary>
/// 推荐流视频信息。
/// </summary>
public class RecommendedVideoInfo
{
    public int Duration { get; set; }
    public string Cover { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
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
    /// <param name="page">抽象页面</param>
    /// <returns>是否保证当前不在笔记详情页</returns>
    Task<bool> EnsureExitNoteDetailIfPresentAsync(IAutoPage page);

    /// <summary>
    /// 确保当前处于“发现/搜索”入口上下文：
    /// 1) 若在详情页，先尝试退出；
    /// 2) 若已在 发现(Recommend)/搜索(Search) 则直接通过；
    /// 3) 否则尝试点击侧边栏发现链接；失败则回退为直接URL导航；
    /// 4) 导航后再次检测，判定成功与否。
    /// </summary>
    /// <param name="page">抽象页面</param>
    /// <returns>是否处于发现/搜索入口上下文</returns>
    Task<bool> EnsureOnDiscoverOrSearchAsync(IAutoPage page);
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
    RateLimited,
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
/// </summary>

/// <summary>
/// 拟人化处理模式分类（破坏性改版，去除导出逻辑后保留节奏调度枚举）。
/// </summary>
public enum ProcessingMode
{
    /// <summary>快速处理：优先效率，适用于低风险路径。</summary>
    Fast,
    /// <summary>标准处理：均衡稳定与速度的常规模式。</summary>
    Standard,
    /// <summary>谨慎处理：强调安全与观测的慢速路径。</summary>
    Careful
}

/// <summary>
/// 批量抓取的统计数据快照，用于审计采集质量与节奏表现。
/// </summary>
public sealed record BatchProcessingStatistics(
    int CompleteDataCount,
    int PartialDataCount,
    int MinimalDataCount,
    double AverageProcessingTime,
    double AverageLikes,
    double AverageComments,
    IReadOnlyDictionary<NoteType, int> TypeDistribution,
    IReadOnlyDictionary<ProcessingMode, int> ProcessingModeStats,
    DateTime CalculatedAt)
{
    /// <summary>总样本量。</summary>
    public int TotalSamples => CompleteDataCount + PartialDataCount + MinimalDataCount;
}

/// <summary>
/// 批量笔记处理结果，聚焦纯监听采集的成功/失败与统计信息。
/// </summary>
public sealed record BatchNoteResult(
    IReadOnlyList<NoteDetail> SuccessfulNotes,
    IReadOnlyList<(string Keyword, string Reason)> FailedNotes,
    int ProcessedCount,
    TimeSpan ProcessingTime,
    DataQuality OverallQuality,
    BatchProcessingStatistics Statistics)
{
    /// <summary>是否存在失败项。</summary>
    public bool HasFailures => FailedNotes.Count > 0;
}

/// <summary>
/// 搜索统计信息，涵盖质量、类型与互动等多维指标。
/// </summary>
public sealed record SearchStatistics(
    int CompleteDataCount,
    int PartialDataCount,
    int MinimalDataCount,
    double AverageLikes,
    double AverageComments,
    DateTime CalculatedAt,
    int VideoNotesCount,
    int ImageNotesCount,
    double AverageCollects,
    IReadOnlyDictionary<string, int> AuthorDistribution,
    IReadOnlyDictionary<NoteType, int> TypeDistribution,
    IReadOnlyDictionary<DataQuality, int> DataQualityDistribution)
{
    /// <summary>总数据条数。</summary>
    public int TotalCount => CompleteDataCount + PartialDataCount + MinimalDataCount;
}

/// <summary>
/// 搜索请求参数快照，便于审计与重放。
/// </summary>
public sealed record SearchParametersInfo(
    string Keyword,
    string SortBy,
    string NoteType,
    string PublishTime,
    int MaxResults,
    DateTime RequestedAt);

/// <summary>
/// 搜索结果模型，聚焦笔记列表与统计摘要。
/// </summary>
public sealed record SearchResult(
    IReadOnlyList<NoteInfo> Notes,
    int TotalCount,
    string SearchKeyword,
    TimeSpan Duration,
    SearchStatistics Statistics,
    int ApiRequests,
    int InterceptedResponses,
    SearchParametersInfo SearchParameters,
    DateTime GeneratedAt);

/// <summary>
/// 智能收集性能指标，用于分析监听链路健康度。
/// </summary>
public sealed record CollectionPerformanceMetrics(
    int SuccessfulRequests,
    int FailedRequests,
    int ScrollCount,
    TimeSpan Duration)
{
    /// <summary>总请求数。</summary>
    public int RequestCount => SuccessfulRequests + FailedRequests;

    /// <summary>成功率（0-1）。</summary>
    public double SuccessRate => RequestCount == 0 ? 0 : (double)SuccessfulRequests / RequestCount;
}

/// <summary>
/// 智能收集结果，集中记录采集笔记及性能指标。
/// </summary>
public sealed record SmartCollectionResult(
    IReadOnlyList<NoteInfo> CollectedNotes,
    int TargetCount,
    CollectionPerformanceMetrics PerformanceMetrics,
    TimeSpan Duration)
{
    /// <summary>成功采集条数。</summary>
    public int CollectedCount => CollectedNotes.Count;

    /// <summary>请求总数，包含成功与失败。</summary>
    public int RequestCount => PerformanceMetrics.RequestCount;

    /// <summary>创建成功结果的便捷工厂。</summary>
    public static SmartCollectionResult CreateSuccess(
        IReadOnlyList<NoteInfo> notes,
        int targetCount,
        CollectionPerformanceMetrics metrics)
    {
        return new SmartCollectionResult(
            CollectedNotes: notes,
            TargetCount: targetCount,
            PerformanceMetrics: metrics,
            Duration: metrics.Duration);
    }
}

/// <summary>
/// 推荐统计信息，覆盖互动、分类与作者分布。
/// </summary>
public sealed record RecommendStatistics(
    int VideoNotesCount,
    int ImageNotesCount,
    double AverageLikes,
    double AverageComments,
    double AverageCollects,
    IReadOnlyDictionary<string, int> TopCategories,
    IReadOnlyDictionary<string, int> AuthorDistribution,
    DateTime CalculatedAt);

/// <summary>
/// 推荐收集的过程详情，强调监听链路观测性。
/// </summary>
public sealed record RecommendCollectionDetails(
    int InterceptedRequests,
    int SuccessfulRequests,
    int FailedRequests,
    int ScrollOperations,
    int AverageScrollDelay,
    DataQuality DataQuality,
    RecommendCollectionMode CollectionMode);

/// <summary>
/// 推荐模块的采集模式枚举。
/// </summary>
public enum RecommendCollectionMode
{
    /// <summary>标准模式：均衡效率与稳定性。</summary>
    Standard,
    /// <summary>谨慎模式：偏向稳定，速度较慢。</summary>
    Conservative,
    /// <summary>激进模式：追求速度，容忍更高失败率。</summary>
    Aggressive
}

/// <summary>
/// 推荐列表结果，承载笔记明细与统计分析。
/// </summary>
public sealed record RecommendListResult(
    IReadOnlyList<NoteInfo> Notes,
    int TotalCollected,
    int RequestCount,
    TimeSpan Duration,
    RecommendStatistics Statistics,
    RecommendCollectionDetails CollectionDetails);

/// <summary>
/// 推荐笔记的统一模型，兼容多端口信号。
/// </summary>
public class RecommendedNote
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string AuthorId { get; set; } = string.Empty;
    public string AuthorAvatar { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string CoverImage { get; set; } = string.Empty;
    public NoteType Type { get; set; } = NoteType.Unknown;
    public int? LikeCount { get; set; }
    public int? CommentCount { get; set; }
    public int? FavoriteCount { get; set; }
    public int? ShareCount { get; set; }
    public DateTime? ExtractedAt { get; set; }
    public DataQuality Quality { get; set; } = DataQuality.Minimal;
    public List<string> MissingFields { get; set; } = [];
    public string Description { get; set; } = string.Empty;
    public string? VideoUrl { get; set; }
    public int? VideoDuration { get; set; }
    public bool IsLiked { get; set; }
    public bool IsCollected { get; set; }
    public string? TrackId { get; set; }
    public string? XsecToken { get; set; }
    public string? PageToken { get; set; }
    public string? SearchId { get; set; }
    public RecommendedCoverInfo? CoverInfo { get; set; }
    public RecommendedInteractInfo? InteractInfo { get; set; }
    public RecommendedUserInfo? UserInfo { get; set; }
    public List<RecommendedImageInfo> Images { get; set; } = [];
}

/// <summary>
/// 推荐图片元数据。
/// </summary>
public class RecommendedImageInfo
{
    public string Url { get; set; } = string.Empty;
    public int? Width { get; set; }
    public int? Height { get; set; }
    public string? Description { get; set; }
}

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
/// <summary>
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
#region MCP工具强类型返回值定义
/// <summary>
/// 浏览器连接结果
/// </summary>
public record BrowserConnectionResult(
    bool IsConnected,
    bool IsLoggedIn,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("errorCode")] string? ErrorCode = null,
    [property: JsonPropertyName("retriable")] bool? Retriable = null,
    [property: JsonPropertyName("requestId")] string? RequestId = null
);
/// <summary>
/// 笔记详情结果
/// </summary>
public record NoteDetailResult(
    NoteDetail? Detail,
    bool Success,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("errorCode")] string? ErrorCode = null,
    [property: JsonPropertyName("retriable")] bool? Retriable = null,
    [property: JsonPropertyName("requestId")] string? RequestId = null
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
    string? ErrorCode = null,
    string? ClickPath = null
);

/// <summary>
/// 评论草稿数据载体。
/// </summary>
public sealed record CommentDraft(
    string Content,
    bool UseEmoji,
    IReadOnlyList<string>? Emojis,
    IReadOnlyList<string>? Hashtags
);

/// <summary>
/// 拟人化提交操作的即时结果。
/// </summary>
public sealed record InteractionSubmitResult(bool Success, string Message);

/// <summary>
/// API 反馈聚合结果。
/// </summary>
public sealed record ApiFeedback(
    bool Success,
    string Message,
    IReadOnlyList<string> ResponseIds,
    IReadOnlyDictionary<string, object?>? Payload
);

/// <summary>
/// 审计上下文：记录一次交互的关键指标。
/// </summary>
public sealed record FeedbackContext(
    bool DomVerified,
    bool ApiConfirmed,
    TimeSpan Duration,
    string? Extra = null
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

/// <summary>
/// 打开探索页第一个笔记并执行交互（点赞/收藏）的简易结果（MCP 工具返回）。
/// </summary>
public record FirstNoteInteractResultMcp(
    bool Success,
    string? Title,
    bool? Liked,
    bool? Favorited,
    string Message,
    string? ErrorCode = null
);
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
/// <summary>
/// 基础交互信息（点赞/收藏/评论等统计）。
/// </summary>
public class BaseInteractInfo
{
    /// <summary>点赞状态</summary>
    public virtual bool Liked { get; set; }
    /// <summary>收藏状态</summary>
    public virtual bool Collected { get; set; }
    /// <summary>点赞数量</summary>
    public virtual int LikeCount { get; set; }
    /// <summary>评论数量</summary>
    public virtual int CommentCount { get; set; }
    /// <summary>收藏数量</summary>
    public virtual int FavoriteCount { get; set; }
    /// <summary>分享数量</summary>
    public virtual int ShareCount { get; set; }
}

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
    /// 覆盖基类的 LikeCount，提供字符串解析。
    /// </summary>
    public override int LikeCount
    {
        get => ParseCount(LikedCountRaw, base.LikeCount);
        set
        {
            base.LikeCount = value;
            LikedCountRaw = value.ToString();
        }
    }

    private static int ParseCount(string raw, int fallback)
    {
        return int.TryParse(raw, out var parsed) ? parsed : fallback;
    }
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
    /// <param name="page">抽象页面实例</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>等待策略执行结果</returns>
    Task<PageLoadWaitResult> WaitForPageLoadAsync(IAutoPage page, CancellationToken cancellationToken = default);

    /// <summary>
    /// 执行指定的单一等待策略
    /// </summary>
    /// <param name="page">抽象页面实例</param>
    /// <param name="strategy">等待策略类型</param>
    /// <param name="timeout">自定义超时时间（可选）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>等待策略执行结果</returns>
    Task<PageLoadWaitResult> WaitForPageLoadAsync(IAutoPage page, PageLoadStrategy strategy, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 快速模式页面加载等待
    /// 仅使用DOMContentLoaded策略，适用于轻量级页面或性能要求较高的场景
    /// </summary>
    /// <param name="page">抽象页面实例</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>等待策略执行结果</returns>
    Task<PageLoadWaitResult> WaitForPageLoadFastAsync(IAutoPage page, CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查页面是否正在加载
    /// </summary>
    /// <param name="page">抽象页面实例</param>
    /// <returns>页面是否正在加载</returns>
    Task<bool> IsPageLoadingAsync(IAutoPage page);

    /// <summary>
    /// 等待页面加载完成
    /// </summary>
    /// <param name="page">抽象页面实例</param>
    /// <param name="timeout">超时时间</param>
    /// <returns>是否等待成功</returns>
    Task<bool> WaitForLoadCompleteAsync(IAutoPage page, TimeSpan timeout);
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
// 配置类迁移说明：PageLoadWaitConfig 已并入统一配置 XhsSettings.PageLoadWaitConfig（破坏性变更）。
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
    /// <param name="page">抽象页面（驱动无关）</param>
    /// <param name="endpointsToMonitor">要监听的端点类型</param>
    /// <returns>设置是否成功</returns>
    bool SetupMonitor(HushOps.Core.Automation.Abstractions.IAutoPage page, HashSet<ApiEndpointType> endpointsToMonitor);

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
    /// 获取最近窗口的网络统计（用于反检测调优）。
    /// </summary>
    NetworkStats GetNetworkStats();

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
























