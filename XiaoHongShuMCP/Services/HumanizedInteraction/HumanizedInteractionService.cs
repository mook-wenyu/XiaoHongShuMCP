using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace XiaoHongShuMCP.Services;


/// <summary>
/// 拟人化交互服务实现（门面）。
/// - 职责：对外提供"像人一样"的点击、悬停、输入、滚动与等待等交互方法；
///         内部协调 <see cref="IDelayManager"/>（延时策略）、<see cref="IElementFinder"/>（元素定位）、
///         <see cref="IDomElementManager"/>（选择器别名）与多种 <see cref="ITextInputStrategy"/>（输入策略）。
/// - 设计：门面 + 策略 + 组合。所有动作统一经过延时管理，尽可能避免"机械化"模式触发风控。
/// - 不变式：
///   1) 不直接硬编码具体 CSS/XPath，统一通过别名向 <see cref="IDomElementManager"/> 取选择器；
///   2) 输入采用语义分割 + 节奏停顿；
///   3) 滚动采用分步 + 自然缓动，可选等待虚拟化列表刷新。
/// - 失败面向：找不到元素、编辑器未就绪、虚拟列表未渲染完毕、按钮存在加载态等；均通过重试/等待做降噪。
/// 线程安全：服务不持有页面或元素的可变状态（按调用传入），可在多个页面并发复用。
/// </summary>
public class HumanizedInteractionService : IHumanizedInteractionService
{
    private readonly IBrowserManager _browserManager;
    private readonly IDelayManager _delayManager;
    private readonly IElementFinder _elementFinder;
    private readonly List<ITextInputStrategy> _inputStrategies;
    private readonly IDomElementManager _domElementManager;
    private readonly ILogger<HumanizedInteractionService>? _logger;
    private readonly TimeSpan _cacheTtl;

    public HumanizedInteractionService(
        IBrowserManager browserManager,
        IDelayManager delayManager,
        IElementFinder elementFinder,
        IEnumerable<ITextInputStrategy> inputStrategies,
        IDomElementManager domElementManager,
        Microsoft.Extensions.Options.IOptions<InteractionCacheConfig>? cacheOptions = null,
        ILogger<HumanizedInteractionService>? logger = null)
    {
        _browserManager = browserManager;
        _delayManager = delayManager;
        _elementFinder = elementFinder;
        _inputStrategies = inputStrategies.ToList();
        _domElementManager = domElementManager;
        _logger = logger;
        var ttlMin = cacheOptions?.Value?.TtlMinutes;
        if (ttlMin is null or <= 0) ttlMin = 3;
        // 上限做个保守限制，避免误配置：最大 1 天
        if (ttlMin > 1440) ttlMin = 1440;
        _cacheTtl = TimeSpan.FromMinutes(ttlMin.Value);
    }

    // 兼容构造已移除：请注入 IOptions<InteractionCacheConfig>（可使用默认配置）

    /// <summary>
    /// 人性化点击（通过选择器别名）。
    /// 先利用 <see cref="IElementFinder"/> 定位元素，再委托到元素版本的点击方法。
    /// 找不到元素将抛出异常，便于上层统一处理。
    /// </summary>
    public async Task HumanClickAsync(string selectorAlias)
    {
        var page = await _browserManager.GetPageAsync();

        var element = await _elementFinder.FindElementAsync(page, selectorAlias);
        if (element == null)
        {
            throw new Exception($"无法找到元素: {selectorAlias}");
        }

        await HumanClickAsync(element);
    }

    /// <summary>
    /// 人性化点击（元素句柄）。
    /// - 步骤：短暂“观察”→ Hover 聚焦 → 点击前停顿 → 执行点击。
    /// - 目的：模拟用户将鼠标移入并确认的过程，降低明显的自动化特征。
    /// </summary>
    public async Task HumanClickAsync(IElementHandle element)
    {
        // 1) 注意力模拟（hover 前短暂停顿）
        await _delayManager.WaitAsync(HumanWaitType.ReviewPause);
        try
        {
            await element.HoverAsync();
        }
        catch (PlaywrightException ex)
        {
            // hover 失败仅记录，不进行任何回退
            _logger?.LogDebug(ex, "Hover 失败");
        }

        // 2) 点击前的思考延时（避免秒点）
        await _delayManager.WaitAsync(HumanWaitType.ClickPreparation);

        // 3) 仅执行常规点击；失败由上层处理
        await element.ClickAsync();
    }

    /// <summary>
    /// 人性化悬停（通过选择器别名）。
    /// 如果未找到元素则静默返回，以便调用方决定是否重试或降级。
    /// </summary>
    public async Task HumanHoverAsync(string selectorAlias)
    {
        var page = await _browserManager.GetPageAsync();
        var element = await _elementFinder.FindElementAsync(page, selectorAlias);
        if (element != null)
        {
            await HumanHoverAsync(element);
        }
    }

    /// <summary>
    /// 人性化悬停（元素句柄）。
    /// 移动到元素并进行观察性停顿，常用于触发 tooltip/菜单等悬停态。
    /// </summary>
    public async Task HumanHoverAsync(IElementHandle element)
    {
        // 模拟移动到元素需要的时间
        await _delayManager.WaitAsync(HumanWaitType.HoverPause);
        await element.HoverAsync();

        // 悬停后的观察时间（等待UI变化稳定）
        await _delayManager.WaitAsync(HumanWaitType.ReviewPause);
    }

    /// <summary>
    /// 人性化文本输入（通过选择器别名）。
    /// 使用“策略模式”根据元素类型选择具体输入策略（标准输入/富文本等）。
    /// 未命中策略或未找到元素会抛出异常，以便上层统一处理。
    /// </summary>
    public async Task HumanTypeAsync(IPage page, string selectorAlias, string text)
    {
        var element = await _elementFinder.FindElementAsync(page, selectorAlias);
        if (element == null)
        {
            throw new Exception($"无法找到元素: {selectorAlias}");
        }

        // 使用策略模式选择合适的输入策略
        var applicableStrategy = await GetApplicableInputStrategyAsync(element);
        if (applicableStrategy == null)
        {
            throw new Exception($"未找到适用的文本输入策略: {selectorAlias}");
        }

        await applicableStrategy.InputTextAsync(page, element, text);
    }




    /// <summary>
    /// 人性化滚动（默认随机距离，不等待新内容）。
    /// 等价于 <c>HumanScrollAsync(page, 0, false)</c>。
    /// </summary>
    public async Task HumanScrollAsync(IPage page, CancellationToken cancellationToken = default)
    {
        await HumanScrollAsync(page, targetDistance: 0, waitForLoad: false, cancellationToken);
    }

    /// <summary>
    /// 简化的人性化滚动操作 - 基础滚动 + 刷新策略
    /// - 用途：支持虚拟化列表的内容获取
    /// - 策略：基础滚轮滚动，检测阻塞后刷新页面获取新内容
    /// - 设计：简单可靠，避免与平台检测机制对抗
    /// </summary>
    /// <param name="page">页面对象</param>
    /// <param name="targetDistance">目标滚动距离（像素）；传 0 则在 [300,800] 内随机</param>
    /// <param name="waitForLoad">是否等待新内容加载</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task HumanScrollAsync(IPage page, int targetDistance, bool waitForLoad = true, CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("开始累积滚动模式");

        // 如果未指定目标距离，使用较小的随机滚动
        if (targetDistance <= 0)
        {
            targetDistance = Random.Shared.Next(300, 800);
        }

        await ExecuteSingleScrollAsync(page, targetDistance, cancellationToken);
        
        await Task.Delay(Random.Shared.Next(2000, 3500), cancellationToken); // 2-3.5秒随机间隔
        
        // 如果需要等待新内容加载
        if (waitForLoad)
        {
            await _delayManager.WaitAsync(HumanWaitType.ContentLoading, cancellationToken: cancellationToken);
        }
    }


    /// <summary>
    /// 执行单次滚动操作（不刷新页面）
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    private async Task ExecuteSingleScrollAsync(IPage page, int targetDistance, CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("执行单次滚动 - 距离: {Distance}px", targetDistance);
        
        // 将鼠标移到视口中心
        var viewportSize = page.ViewportSize;
        if (viewportSize != null)
        {
            var centerX = viewportSize.Width / 2;
            var centerY = viewportSize.Height / 2;
            await page.Mouse.MoveAsync(centerX, centerY);
            await _delayManager.WaitAsync(HumanWaitType.ScrollPreparation, cancellationToken: cancellationToken);
        }

        // 执行基础滚轮滚动
        await page.Mouse.WheelAsync(0, targetDistance);
        
        _logger?.LogDebug("单次滚动完成");
    }

    /// <summary>
    /// 基础滚动（测试探针用的简化私有方法）。
    /// 语义包装 <see cref="ExecuteSingleScrollAsync"/>，用于验证“虚拟滚动简化”是否落地。
    /// </summary>
    private async Task ExecuteBasicScrollAsync(IPage page, int targetDistance, CancellationToken cancellationToken = default)
    {
        await ExecuteSingleScrollAsync(page, targetDistance, cancellationToken);
    }

    /// <summary>
    /// 等待新内容加载（测试探针）。
    /// 简单委托到延时管理器的 <see cref="HumanWaitType.ContentLoading"/>。
    /// </summary>
    private async Task WaitForContentLoadAsync(CancellationToken cancellationToken = default)
    {
        await _delayManager.WaitAsync(HumanWaitType.ContentLoading, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// 刷新页面以便获取新内容（测试探针）。
    /// </summary>
    private async Task RefreshPageForNewContentAsync(IPage page, CancellationToken cancellationToken = default)
    {
        try
        {
            await page.ReloadAsync();
        }
        catch
        {
            // 忽略刷新异常（例如无网络或页面不支持 reload），交由后续等待与重试吸收
        }
        await _delayManager.WaitAsync(HumanWaitType.ContentLoading, cancellationToken: cancellationToken);
    }


    /// <summary>
    /// 查找元素（委托给 <see cref="IElementFinder"/>）。
    /// </summary>
    public async Task<IElementHandle?> FindElementAsync(IPage page, string selectorAlias, int retries = 3, int timeout = 3000)
    {
        return await _elementFinder.FindElementAsync(page, selectorAlias, retries, timeout);
    }

    /// <summary>
    /// 查找元素（支持页面状态感知）。
    /// 根据 <paramref name="pageState"/> 从 <see cref="IDomElementManager"/> 获取更精确的候选选择器并逐一尝试。
    /// </summary>
    public async Task<IElementHandle?> FindElementAsync(IPage page, string selectorAlias, PageState pageState, int retries = 3, int timeout = 3000, CancellationToken cancellationToken = default)
    {
        // 获取页面状态感知的选择器
        var selectors = _domElementManager.GetSelectors(selectorAlias, pageState);

        for (int attempt = 0; attempt < retries; attempt++)
        {
            foreach (var selector in selectors)
            {
                try
                {
                    var element = await page.QuerySelectorAsync(selector);
                    if (element != null)
                    {
                        return element;
                    }
                }
                catch
                {
                    // 继续尝试下一个选择器
                }
            }

            if (attempt < retries - 1)
            {
                await Task.Delay(timeout / retries, cancellationToken);
            }
        }

        return null;
    }

    /// <summary>
    /// 统一的人性化等待控制（委托给DelayManager）
    /// </summary>
    public async Task HumanWaitAsync(HumanWaitType waitType, CancellationToken cancellationToken = default)
    {
        await _delayManager.WaitAsync(waitType, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// 重试延时控制（委托给DelayManager）
    /// </summary>
    public async Task HumanRetryDelayAsync(int attemptNumber, CancellationToken cancellationToken = default)
    {
        await _delayManager.WaitAsync(HumanWaitType.RetryBackoff, attemptNumber, cancellationToken);
    }

    /// <summary>
    /// 动作间延时（委托给DelayManager）
    /// </summary>
    public async Task HumanBetweenActionsDelayAsync(CancellationToken cancellationToken = default)
    {
        await HumanWaitAsync(HumanWaitType.BetweenActions, cancellationToken);
    }

    /// <summary>
    /// 拟人化点赞操作
    /// 检测当前点赞状态，执行点赞操作，并验证结果
    /// </summary>
    public async Task<InteractionResult> HumanLikeAsync()
    {
        _logger?.LogInformation("开始执行拟人化点赞操作");

        try
        {
            var page = await _browserManager.GetPageAsync();

            // 保持拟人化不滚动
            await HumanWaitAsync(HumanWaitType.ThinkingPause);

            string currentState = "未点赞";
            bool isCurrentlyLiked = false;
            IElementHandle? activeButton = null;

            // 优先读取详情页API的临时缓存（不检测DOM）
            if (InteractionStateCache.TryGetMostRecent(out var likeSnap, _cacheTtl))
            {
                isCurrentlyLiked = likeSnap!.IsLiked;
                currentState = isCurrentlyLiked ? "已点赞" : "未点赞";
            }

            var likeButton = activeButton;
            if (!isCurrentlyLiked)
            {
                var likeButtonSelectors = _domElementManager.GetSelectors("likeButton");
                foreach (var selector in likeButtonSelectors)
                {
                    try
                    {
                        likeButton = await page.QuerySelectorAsync(selector);
                        if (likeButton != null)
                        {
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogDebug("查找点赞按钮选择器 {Selector} 失败: {Error}", selector, ex.Message);
                    }
                }
            }

            if (likeButton == null)
            {
                return new InteractionResult(
                    Success: false,
                    Action: "点赞",
                    PreviousState: currentState,
                    CurrentState: currentState,
                    Message: "未找到点赞按钮",
                    ErrorCode: "LIKE_BUTTON_NOT_FOUND"
                );
            }

            string previousState = currentState;
            if (isCurrentlyLiked)
            {
                _logger?.LogInformation("笔记已经点赞，无需重复操作");
                // 标记为幂等成功，附加 ErrorCode 供上层短路判定
                return new InteractionResult(
                    Success: true,
                    Action: "点赞",
                    PreviousState: previousState,
                    CurrentState: "已点赞",
                    Message: "笔记已经点赞",
                    ErrorCode: "ALREADY_LIKED"
                );
            }

            var likeWrapper = await GetClosestWrapperOrSelfAsync(likeButton, ".like-wrapper");
            _logger?.LogDebug("执行点赞点击操作（目标wrapper）");
            await HumanClickAsync(likeWrapper);
            await HumanWaitAsync(HumanWaitType.BetweenActions);

            return new InteractionResult(
                Success: true,
                Action: "点赞",
                PreviousState: previousState,
                CurrentState: "未知",
                Message: "已发起点赞（等待API确认）"
            );
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "拟人化点赞操作异常");
            return new InteractionResult(
                Success: false,
                Action: "点赞",
                PreviousState: "未知",
                CurrentState: "未知",
                Message: $"点赞操作异常: {ex.Message}",
                ErrorCode: "LIKE_OPERATION_EXCEPTION"
            );
        }
    }

    public async Task<InteractionResult> HumanUnlikeAsync(IPage page)
    {
        _logger?.LogInformation("开始执行拟人化取消点赞操作");
        try
        {
            await HumanWaitAsync(HumanWaitType.ThinkingPause);

            string currentState = "未点赞";
            IElementHandle? activeButton = null;

            // 优先读取缓存判断状态
            bool likedByCache = false;
            if (InteractionStateCache.TryGetMostRecent(out var likeSnap, _cacheTtl))
            {
                likedByCache = likeSnap!.IsLiked;
                currentState = likedByCache ? "已点赞" : "未点赞";
            }
            else
            {
                var likedButtonSelectors = _domElementManager.GetSelectors("likeButtonActive");
                foreach (var selector in likedButtonSelectors)
                {
                    try
                    {
                        activeButton = await page.QuerySelectorAsync(selector);
                        if (activeButton != null)
                        {
                            currentState = "已点赞";
                            break;
                        }
                    }
                    catch { }
                }
            }

            // 未点赞 → 幂等成功（不依赖DOM）
            if (!likedByCache && activeButton == null)
            {
                return new InteractionResult(true, "取消点赞", "未点赞", "未点赞", "已处于未点赞状态");
            }

            // 升阶点击目标（若缓存判断已点赞但未找到元素，则尝试按活动选择器再找一次）
            if (activeButton == null)
            {
                foreach (var sel in _domElementManager.GetSelectors("likeButtonActive"))
                {
                    try
                    {
                        activeButton = await page.QuerySelectorAsync(sel);
                        if (activeButton != null) break;
                    }
                    catch { }
                }
            }
            var likeWrapper = await GetClosestWrapperOrSelfAsync(activeButton!, ".like-wrapper");

            await HumanClickAsync(likeWrapper);
            await HumanWaitAsync(HumanWaitType.BetweenActions);

            // 不再依赖DOM校验（以API回执为准）
            return new InteractionResult(true, "取消点赞", "已点赞", "未知", "已发起取消点赞（等待API确认）");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "拟人化取消点赞操作异常");
            return new InteractionResult(false, "取消点赞", "未知", "未知", $"取消点赞操作异常: {ex.Message}", "UNLIKE_OPERATION_EXCEPTION");
        }
    }

    /// <summary>
    /// 拟人化收藏操作
    /// 检测当前收藏状态，执行收藏操作，并验证结果
    /// </summary>
    public async Task<InteractionResult> HumanFavoriteAsync(IPage page)
    {
        _logger?.LogInformation("开始执行拟人化收藏操作");

        try
        {
            // 不做遮罩指针事件处理，保持拟人化不滚动
            await HumanWaitAsync(HumanWaitType.ThinkingPause);

            string currentState = "未收藏";
            bool isCurrentlyFavorited = false;
            IElementHandle? activeButton = null;

            // 优先读取缓存判断状态
            if (InteractionStateCache.TryGetMostRecent(out var favSnap, _cacheTtl))
            {
                isCurrentlyFavorited = favSnap!.IsCollected;
                currentState = isCurrentlyFavorited ? "已收藏" : "未收藏";
            }
            else
            {
                var favoritedButtonSelectors = _domElementManager.GetSelectors("favoriteButtonActive");
                foreach (var selector in favoritedButtonSelectors)
                {
                    try
                    {
                        activeButton = await page.QuerySelectorAsync(selector);
                        if (activeButton != null)
                        {
                            isCurrentlyFavorited = true;
                            currentState = "已收藏";
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogDebug("检测已收藏状态选择器 {Selector} 失败: {Error}", selector, ex.Message);
                    }
                }
            }

            var favoriteButton = activeButton;
            if (!isCurrentlyFavorited)
            {
                var favoriteButtonSelectors = _domElementManager.GetSelectors("favoriteButton");
                foreach (var selector in favoriteButtonSelectors)
                {
                    try
                    {
                        favoriteButton = await page.QuerySelectorAsync(selector);
                        if (favoriteButton != null)
                        {
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogDebug("查找收藏按钮选择器 {Selector} 失败: {Error}", selector, ex.Message);
                    }
                }
            }

            if (favoriteButton == null)
            {
                return new InteractionResult(
                    Success: false,
                    Action: "收藏",
                    PreviousState: currentState,
                    CurrentState: currentState,
                    Message: "未找到收藏按钮",
                    ErrorCode: "FAVORITE_BUTTON_NOT_FOUND"
                );
            }

            string previousState = currentState;
            if (isCurrentlyFavorited)
            {
                _logger?.LogInformation("笔记已经收藏，无需重复操作");
                // 标记为幂等成功，附加 ErrorCode 供上层短路判定
                return new InteractionResult(
                    Success: true,
                    Action: "收藏",
                    PreviousState: previousState,
                    CurrentState: "已收藏",
                    Message: "笔记已经收藏",
                    ErrorCode: "ALREADY_FAVORITED"
                );
            }

            var favWrapper = await GetClosestWrapperOrSelfAsync(favoriteButton, ".collect-wrapper");
            _logger?.LogDebug("执行收藏点击操作（目标wrapper）");
            await HumanClickAsync(favWrapper);
            await HumanWaitAsync(HumanWaitType.BetweenActions);

            return new InteractionResult(
                Success: true,
                Action: "收藏",
                PreviousState: previousState,
                CurrentState: "未知",
                Message: "已发起收藏（等待API确认）"
            );
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "拟人化收藏操作异常");
            return new InteractionResult(
                Success: false,
                Action: "收藏",
                PreviousState: "未知",
                CurrentState: "未知",
                Message: $"收藏操作异常: {ex.Message}",
                ErrorCode: "FAVORITE_OPERATION_EXCEPTION"
            );
        }
    }

    public async Task<InteractionResult> HumanUnfavoriteAsync(IPage page)
    {
        _logger?.LogInformation("开始执行拟人化取消收藏操作");
        try
        {
            // 不做遮罩指针事件处理，保持拟人化不滚动
            await HumanWaitAsync(HumanWaitType.ThinkingPause);

            string currentState = "未收藏";
            IElementHandle? activeButton = null;

            // 优先读取缓存判断状态
            bool collectedByCache = false;
            if (InteractionStateCache.TryGetMostRecent(out var favSnap, _cacheTtl))
            {
                collectedByCache = favSnap!.IsCollected;
                currentState = collectedByCache ? "已收藏" : "未收藏";
            }
            else
            {
                var favoritedButtonSelectors = _domElementManager.GetSelectors("favoriteButtonActive");
                foreach (var selector in favoritedButtonSelectors)
                {
                    try
                    {
                        activeButton = await page.QuerySelectorAsync(selector);
                        if (activeButton != null)
                        {
                            currentState = "已收藏";
                            break;
                        }
                    }
                    catch { }
                }
            }

            if (!collectedByCache && activeButton == null)
            {
                return new InteractionResult(true, "取消收藏", "未收藏", "未收藏", "已处于未收藏状态");
            }

            if (activeButton == null)
            {
                foreach (var sel in _domElementManager.GetSelectors("favoriteButtonActive"))
                {
                    try
                    {
                        activeButton = await page.QuerySelectorAsync(sel);
                        if (activeButton != null) break;
                    }
                    catch { }
                }
            }

            var favWrapper = await GetClosestWrapperOrSelfAsync(activeButton!, ".collect-wrapper");

            await HumanClickAsync(favWrapper);
            await HumanWaitAsync(HumanWaitType.BetweenActions);

            // 不再依赖DOM校验
            return new InteractionResult(true, "取消收藏", "已收藏", "未知", "已发起取消收藏（等待API确认）");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "拟人化取消收藏操作异常");
            return new InteractionResult(false, "取消收藏", "未知", "未知", $"取消收藏操作异常: {ex.Message}", "UNFAVORITE_OPERATION_EXCEPTION");
        }
    }

    #region 私有辅助方法
    /// <summary>
    /// 选择一个适用的文本输入策略（先到先得）。
    /// </summary>
    private async Task<ITextInputStrategy?> GetApplicableInputStrategyAsync(IElementHandle element)
    {
        foreach (var strategy in _inputStrategies)
        {
            if (await strategy.IsApplicableAsync(element))
            {
                return strategy;
            }
        }

        return null;
    }

    /// <summary>
    /// 执行自然滚动操作（单段平滑滚动）。
    /// 使用鼠标滚轮分帧滚动模拟平滑效果。
    /// </summary>
    /// <param name="page">页面对象</param>
    /// <param name="distance">滚动距离</param>
    /// <param name="duration">滚动时长</param>
    public async Task PerformNaturalScrollAsync(IPage page, int distance, TimeSpan duration, CancellationToken cancellationToken = default)
    {
        // 将鼠标移至视口中心
        var viewportSize = page.ViewportSize;
        if (viewportSize != null)
        {
            await page.Mouse.MoveAsync(viewportSize.Width / 2, viewportSize.Height / 2);
        }

        // 使用滚轮分帧滚动模拟平滑效果
        var frames = Math.Max(6, (int)(duration.TotalMilliseconds / 16));
        var step = (double)distance / frames;
        for (int i = 0; i < frames; i++)
        {
            await page.Mouse.WheelAsync(0, (float)step);
            await Task.Delay(10, cancellationToken);
        }
        _logger?.LogDebug("执行自然滚动操作(滚轮): 距离={Distance}px, 时长={Duration}ms", distance, duration.TotalMilliseconds);
    }

    /// <summary>
    /// 升阶到最接近的可点击包装容器（如 .like-wrapper / .collect-wrapper）。
    /// 若未命中包装容器，则返回原元素自身。
    /// </summary>
    private async Task<IElementHandle> GetClosestWrapperOrSelfAsync(IElementHandle element, string wrapperSelector)
    {
        try
        {
            var handle = await element.EvaluateHandleAsync("(el, sel) => el.closest(sel)", wrapperSelector);
            var wrapper = handle?.AsElement();
            return wrapper ?? element;
        }
        catch
        {
            return element;
        }
    }

    /// <summary>
    /// 读取包装容器中的计数与图标 href 快照，用于点击前后对比。
    /// </summary>
    private async Task<(int? Count, string IconHref)> ReadWrapperStateAsync(IElementHandle wrapper, string countSelector)
    {
        int? count = null;
        string iconHref = string.Empty;

        try
        {
            var countEl = await wrapper.QuerySelectorAsync(countSelector);
            if (countEl != null)
            {
                var txt = (await countEl.InnerTextAsync())?.Trim() ?? string.Empty;
                var digits = Regex.Replace(txt, "[^0-9]", "");
                if (int.TryParse(digits, out var val)) count = val;
            }
        }
        catch { }

        try
        {
            var useEl = await wrapper.QuerySelectorAsync("svg use");
            if (useEl != null)
            {
                iconHref = (await useEl.GetAttributeAsync("xlink:href"))
                           ?? (await useEl.GetAttributeAsync("href"))
                           ?? string.Empty;
            }
        }
        catch { }

        return (count, iconHref);
    }

    /// <summary>
    /// 验证点击后包装容器状态是否发生变化（计数或图标）。
    /// - 优先以同一 wrapper 对比；若节点被替换，则尝试通过 activeSelectors 重新获取。
    /// </summary>
    private async Task<bool> VerifyWrapperStateChangedAsync(
        IPage page,
        IElementHandle originalWrapper,
        (int? Count, string IconHref) prev,
        List<string> activeSelectors,
        int retryMs,
        int stepMs,
        CancellationToken cancellationToken = default)
    {
        var start = DateTime.UtcNow;
        var wrapper = originalWrapper;

        while ((DateTime.UtcNow - start).TotalMilliseconds < retryMs)
        {
            try
            {
                // 若原 wrapper 已失效，尝试通过 active 选择器重新获取
                if (wrapper == null)
                {
                    foreach (var sel in activeSelectors)
                    {
                        var el = await page.QuerySelectorAsync(sel);
                        if (el != null)
                        {
                            wrapper = await GetClosestWrapperOrSelfAsync(el, ".like-wrapper, .collect-wrapper");
                            break;
                        }
                    }
                }

                if (wrapper != null)
                {
                    var now = await ReadWrapperStateAsync(wrapper, countSelector: ".count");
                    if ((prev.Count.HasValue && now.Count.HasValue && now.Count.Value != prev.Count.Value)
                        || (!string.IsNullOrEmpty(now.IconHref) && now.IconHref != prev.IconHref))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                // 忽略瞬时DOM错误，继续重试
            }

            await Task.Delay(stepMs, cancellationToken);
        }

        return false;
    }

    /// <summary>
    /// 从当前页面URL提取笔记ID（支持 /explore/{id} 与 /explore/item/{id}）。
    /// </summary>
    // 已移除 URL 提取逻辑：改为依赖 API 监听写入的 InteractionStateCache.TryGetMostRecent()

    /// <summary>
    /// 依序尝试一组选择器，返回首个命中的元素。
    /// </summary>
    private static async Task<IElementHandle?> FindFirstByAliasesAsync(IPage page, IEnumerable<string> selectors)
    {
        foreach (var sel in selectors)
        {
            try
            {
                var el = await page.QuerySelectorAsync(sel);
                if (el != null) return el;
            }
            catch { }
        }
        return null;
    }
    #endregion
}
