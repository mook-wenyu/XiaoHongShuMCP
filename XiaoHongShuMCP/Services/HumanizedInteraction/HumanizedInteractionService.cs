using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace XiaoHongShuMCP.Services;

/// <summary>
/// 拟人化交互服务实现（门面）。
/// - 职责：对外提供“像人一样”的点击、悬停、输入、滚动与等待等交互方法；
///         内部协调 <see cref="IDelayManager"/>（延时策略）、<see cref="IElementFinder"/>（元素定位）、
///         <see cref="IDomElementManager"/>（选择器别名）与多种 <see cref="ITextInputStrategy"/>（输入策略）。
/// - 设计：门面 + 策略 + 组合。所有动作统一经过延时管理，尽可能避免“机械化”模式触发风控。
/// - 不变式：
///   1) 不直接硬编码具体 CSS/XPath，统一通过别名向 <see cref="IDomElementManager"/> 取选择器；
///   2) 输入采用语义分割 + 节奏停顿；
///   3) 滚动采用分步 + 自然缓动，可选等待虚拟化列表刷新。
/// - 失败面向：找不到元素、编辑器未就绪、虚拟列表未渲染完毕、按钮存在加载态等；均通过重试/等待做降噪。
/// 线程安全：服务不持有页面或元素的可变状态（按调用传入），可在多个页面并发复用。
/// </summary>
public class HumanizedInteractionService : IHumanizedInteractionService
{
    private readonly IDelayManager _delayManager;
    private readonly IElementFinder _elementFinder;
    private readonly List<ITextInputStrategy> _inputStrategies;
    private readonly IDomElementManager _domElementManager;
    private readonly ILogger<HumanizedInteractionService>? _logger;

    public HumanizedInteractionService(
        IDelayManager delayManager,
        IElementFinder elementFinder,
        IEnumerable<ITextInputStrategy> inputStrategies,
        IDomElementManager domElementManager,
        ILogger<HumanizedInteractionService>? logger = null)
    {
        _delayManager = delayManager;
        _elementFinder = elementFinder;
        _inputStrategies = inputStrategies.ToList();
        _domElementManager = domElementManager;
        _logger = logger;
    }

    /// <summary>
    /// 人性化点击（通过选择器别名）。
    /// 先利用 <see cref="IElementFinder"/> 定位元素，再委托到元素版本的点击方法。
    /// 找不到元素将抛出异常，便于上层统一处理。
    /// </summary>
    public async Task HumanClickAsync(IPage page, string selectorAlias)
    {
        var element = await _elementFinder.FindElementAsync(page, selectorAlias);
        if (element == null)
        {
            throw new Exception($"无法找到元素: {selectorAlias}");
        }

        await HumanClickAsync(page, element);
    }
        
    /// <summary>
    /// 人性化点击（元素句柄）。
    /// - 步骤：短暂“观察”→ Hover 聚焦 → 点击前停顿 → 执行点击。
    /// - 目的：模拟用户将鼠标移入并确认的过程，降低明显的自动化特征。
    /// </summary>
    public async Task HumanClickAsync(IPage page, IElementHandle element)
    {
        // 1. 注意力模拟（hover 前短暂停顿）
        await Task.Delay(_delayManager.GetReviewPauseDelay());
        await element.HoverAsync();

        // 2. 点击前的思考延时（避免秒点）
        await Task.Delay(_delayManager.GetClickDelay());

        // 3. 执行点击
        await element.ClickAsync();
    }
        
    /// <summary>
    /// 人性化悬停（通过选择器别名）。
    /// 如果未找到元素则静默返回，以便调用方决定是否重试或降级。
    /// </summary>
    public async Task HumanHoverAsync(IPage page, string selectorAlias)
    {
        var element = await _elementFinder.FindElementAsync(page, selectorAlias);
        if (element != null)
        {
            await HumanHoverAsync(page, element);
        }
    }
        
    /// <summary>
    /// 人性化悬停（元素句柄）。
    /// 移动到元素并进行观察性停顿，常用于触发 tooltip/菜单等悬停态。
    /// </summary>
    public async Task HumanHoverAsync(IPage page, IElementHandle element)
    {
        // 模拟移动到元素需要的时间
        await Task.Delay(_delayManager.GetHoverDelay());
        await element.HoverAsync();
            
        // 悬停后的观察时间（等待UI变化稳定）
        await Task.Delay(_delayManager.GetReviewPauseDelay());
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
    public async Task HumanScrollAsync(IPage page)
    {
        await HumanScrollAsync(page, targetDistance: 0, waitForLoad: false);
    }

    /// <summary>
    /// 参数化的人性化滚动操作。
    /// - 用途：支持虚拟化列表“滚动-加载-匹配”的搜索流程。
    /// - 策略：将总距离拆分为多步滚动，每步之间插入“短/中/长”不同节奏的停顿，模拟阅读与浏览。
    /// - 边界：若已到达页面底部，直接返回。
    /// </summary>
    /// <param name="page">页面对象</param>
    /// <param name="targetDistance">目标滚动距离（像素）；传 0 则在 [800,2000] 内随机</param>
    /// <param name="waitForLoad">是否等待新内容加载（含虚拟列表渲染）</param>
    public async Task HumanScrollAsync(IPage page, int targetDistance, bool waitForLoad = true)
    {
        // 获取当前页面滚动位置和总高度信息
        var scrollInfo = await GetScrollInfoAsync(page);
        
        // 如果未指定目标距离，使用拟人化的随机滚动
        if (targetDistance <= 0)
        {
            targetDistance = Random.Shared.Next(800, 2000); // 适合虚拟化列表的较大滚动距离
        }

        // 检测页面底部边界（避免滚动越界造成无效等待）
        var maxScrollDistance = Math.Max(0, scrollInfo.ScrollHeight - scrollInfo.ViewportHeight - scrollInfo.CurrentScrollTop);
        targetDistance = Math.Min(targetDistance, maxScrollDistance);

        if (targetDistance <= 0)
        {
            // 已经到达页面底部
            return;
        }

        // 分步滚动策略 - 使用更稳定的evaluate滚动方式
        var steps = CalculateScrollSteps(targetDistance);
        var remainingDistance = targetDistance;
        var scrolledDistance = 0;

        for (int i = 0; i < steps.Count; i++)
        {
            var stepDistance = Math.Min(steps[i], remainingDistance);
            
            // 使用evaluate进行分步滚动，比mouse wheel更稳定
            await page.EvaluateAsync($"window.scrollBy(0, {stepDistance})");
            
            scrolledDistance += stepDistance;
            remainingDistance -= stepDistance;

            // 拟人化延时：每步滚动后的自然停顿
            var delay = i == 0 ? _delayManager.GetReviewPauseDelay() : // 第一步较短停顿
                       i == steps.Count - 1 ? _delayManager.GetThinkingPauseDelay() : // 最后一步较长停顿
                       _delayManager.GetScrollDelay(); // 中间步骤正常停顿
            
            await Task.Delay(delay);

            // 模拟真人阅读行为 - 偶尔有较长的停顿观察内容
            if (Random.Shared.NextDouble() < 0.3) // 30%概率较长停顿
            {
                await Task.Delay(Random.Shared.Next(800, 1500));
            }

            if (remainingDistance <= 0) break;
        }

        // 等待新内容加载（懒加载/瀑布流/虚拟列表刷新）
        if (waitForLoad && scrolledDistance > 0)
        {
            await WaitForContentLoadAsync(page);
        }
    }

    /// <summary>
    /// 获取当前页面滚动信息（位置/总高度/视口高度）。
    /// </summary>
    private async Task<ScrollInfo> GetScrollInfoAsync(IPage page)
    {
        var scrollInfo = await page.EvaluateAsync<ScrollInfo>(@"
            () => ({
                currentScrollTop: window.pageYOffset || document.documentElement.scrollTop,
                scrollHeight: Math.max(document.body.scrollHeight, document.documentElement.scrollHeight),
                viewportHeight: window.innerHeight
            })
        ");
        
        return scrollInfo;
    }

    /// <summary>
    /// 计算拟人化的滚动步骤。
    /// 小距离更少步，大距离更多步；每一步的步长带一定随机扰动，避免整齐划一。
    /// </summary>
    private List<int> CalculateScrollSteps(int totalDistance)
    {
        var steps = new List<int>();
        var remainingDistance = totalDistance;
        
        // 基于总距离确定步骤数量 - 更大的距离分更多步骤
        var stepCount = totalDistance switch
        {
            <= 500 => Random.Shared.Next(2, 4),
            <= 1000 => Random.Shared.Next(3, 6),
            <= 2000 => Random.Shared.Next(4, 8),
            _ => Random.Shared.Next(6, 12)
        };

        for (int i = 0; i < stepCount && remainingDistance > 0; i++)
        {
            int stepSize;
            
            if (i == stepCount - 1) // 最后一步，滚动剩余距离
            {
                stepSize = remainingDistance;
            }
            else
            {
                // 拟人化的不规则步长 - 避免机械化的等长步骤
                var baseStepSize = totalDistance / stepCount;
                var variance = (int)(baseStepSize * 0.4); // 40%的变化幅度
                stepSize = Math.Max(50, baseStepSize + Random.Shared.Next(-variance, variance + 1));
                stepSize = Math.Min(stepSize, remainingDistance);
            }
            
            steps.Add(stepSize);
            remainingDistance -= stepSize;
        }

        return steps;
    }

    /// <summary>
    /// 等待新内容加载完成：
    /// - 动作间基本延时；
    /// - 等待加载指示器消失（若存在）；
    /// - 最终小等待，保障虚拟化渲染。
    /// </summary>
    private async Task WaitForContentLoadAsync(IPage page)
    {
        // 等待网络活动减少
        await Task.Delay(_delayManager.GetBetweenActionsDelay());
        
        try
        {
            // 等待可能的懒加载内容
            await page.WaitForFunctionAsync(@"
                () => {
                    // 检查是否有加载指示器
                    const loadingElements = document.querySelectorAll('[class*=""loading""], [class*=""spinner""]');
                    return loadingElements.length === 0 || 
                           Array.from(loadingElements).every(el => el.style.display === 'none');
                }
            ", new PageWaitForFunctionOptions { Timeout = 3000 });
        }
        catch
        {
            // 忽略超时错误，继续执行
        }
        
        // 最终等待，确保虚拟化列表渲染完成
        await Task.Delay(Random.Shared.Next(300, 800));
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
    public async Task<IElementHandle?> FindElementAsync(IPage page, string selectorAlias, PageState pageState, int retries = 3, int timeout = 3000)
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
                await Task.Delay(timeout / retries);
            }
        }
        
        return null;
    }

    /// <summary>
    /// 统一的人性化等待控制（委托给DelayManager）
    /// </summary>
    public async Task HumanWaitAsync(HumanWaitType waitType)
    {
        await _delayManager.WaitAsync(waitType);
    }

    /// <summary>
    /// 重试延时控制（委托给DelayManager）
    /// </summary>
    public async Task HumanRetryDelayAsync(int attemptNumber)
    {
        var delay = _delayManager.GetRetryDelay(attemptNumber);
        await Task.Delay(delay);
    }

    /// <summary>
    /// 动作间延时（委托给DelayManager）
    /// </summary>
    public async Task HumanBetweenActionsDelayAsync()
    {
        await HumanWaitAsync(HumanWaitType.BetweenActions);
    }

    /// <summary>
    /// 拟人化点赞操作
    /// 检测当前点赞状态，执行点赞操作，并验证结果
    /// </summary>
    public async Task<InteractionResult> HumanLikeAsync(IPage page)
    {
        _logger?.LogInformation("开始执行拟人化点赞操作");

        try
        {
            // 先进行思考停顿
            await HumanWaitAsync(HumanWaitType.ThinkingPause);

            // 检测当前点赞状态
            string currentState = "未点赞";
            bool isCurrentlyLiked = false;
            IElementHandle? activeButton = null;

            // 首先检查是否已经点赞
            var likedButtonSelectors = _domElementManager.GetSelectors("likeButtonActive");
            foreach (var selector in likedButtonSelectors)
            {
                try
                {
                    activeButton = await page.QuerySelectorAsync(selector);
                    if (activeButton != null)
                    {
                        isCurrentlyLiked = true;
                        currentState = "已点赞";
                        break;
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug("检测已点赞状态选择器 {Selector} 失败: {Error}", selector, ex.Message);
                }
            }

            // 如果未找到已点赞状态，查找未点赞状态的按钮
            IElementHandle? likeButton = activeButton;
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
                // 已经点赞，记录状态但不执行操作
                _logger?.LogInformation("笔记已经点赞，无需重复操作");
                return new InteractionResult(
                    Success: true,
                    Action: "点赞",
                    PreviousState: previousState,
                    CurrentState: "已点赞",
                    Message: "笔记已经点赞"
                );
            }

            // 执行点赞操作
            _logger?.LogDebug("执行点赞点击操作");
            await HumanClickAsync(page, likeButton);
            
            // 等待操作完成
            await HumanWaitAsync(HumanWaitType.BetweenActions);

            // 验证操作结果
            bool operationSuccess = false;
            string newState = previousState;

            // 检查是否出现加载状态
            var loadingSelectors = _domElementManager.GetSelectors("likeButtonLoading");
            bool isLoading = false;
            foreach (var selector in loadingSelectors)
            {
                try
                {
                    var loadingElement = await page.QuerySelectorAsync(selector);
                    if (loadingElement != null)
                    {
                        isLoading = true;
                        break;
                    }
                }
                catch { }
            }

            if (isLoading)
            {
                // 等待加载完成
                await Task.Delay(Random.Shared.Next(1000, 2000));
            }

            // 再次检查点赞状态
            foreach (var selector in likedButtonSelectors)
            {
                try
                {
                    var likedButton = await page.QuerySelectorAsync(selector);
                    if (likedButton != null)
                    {
                        operationSuccess = true;
                        newState = "已点赞";
                        break;
                    }
                }
                catch { }
            }

            if (operationSuccess)
            {
                _logger?.LogInformation("点赞操作成功: {PreviousState} -> {NewState}", previousState, newState);
                return new InteractionResult(
                    Success: true,
                    Action: "点赞",
                    PreviousState: previousState,
                    CurrentState: newState,
                    Message: "点赞成功"
                );
            }
            _logger?.LogWarning("点赞操作可能失败，无法验证状态改变");
            return new InteractionResult(
                Success: false,
                Action: "点赞",
                PreviousState: previousState,
                CurrentState: previousState,
                Message: "点赞操作失败或无法验证状态",
                ErrorCode: "LIKE_VERIFICATION_FAILED"
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

    /// <summary>
    /// 拟人化收藏操作
    /// 检测当前收藏状态，执行收藏操作，并验证结果
    /// </summary>
    public async Task<InteractionResult> HumanFavoriteAsync(IPage page)
    {
        _logger?.LogInformation("开始执行拟人化收藏操作");

        try
        {
            // 先进行思考停顿
            await HumanWaitAsync(HumanWaitType.ThinkingPause);

            // 检测当前收藏状态
            string currentState = "未收藏";
            bool isCurrentlyFavorited = false;
            IElementHandle? activeButton = null;

            // 首先检查是否已经收藏
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

            // 如果未找到已收藏状态，查找未收藏状态的按钮
            IElementHandle? favoriteButton = activeButton;
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
                // 已经收藏，记录状态但不执行操作
                _logger?.LogInformation("笔记已经收藏，无需重复操作");
                return new InteractionResult(
                    Success: true,
                    Action: "收藏",
                    PreviousState: previousState,
                    CurrentState: "已收藏",
                    Message: "笔记已经收藏"
                );
            }

            // 执行收藏操作
            _logger?.LogDebug("执行收藏点击操作");
            await HumanClickAsync(page, favoriteButton);
            
            // 等待操作完成
            await HumanWaitAsync(HumanWaitType.BetweenActions);

            // 验证操作结果
            bool operationSuccess = false;
            string newState = previousState;

            // 检查是否出现加载状态
            var loadingSelectors = _domElementManager.GetSelectors("favoriteButtonLoading");
            bool isLoading = false;
            foreach (var selector in loadingSelectors)
            {
                try
                {
                    var loadingElement = await page.QuerySelectorAsync(selector);
                    if (loadingElement != null)
                    {
                        isLoading = true;
                        break;
                    }
                }
                catch { }
            }

            if (isLoading)
            {
                // 等待加载完成
                await Task.Delay(Random.Shared.Next(1000, 2000));
            }

            // 再次检查收藏状态
            foreach (var selector in favoritedButtonSelectors)
            {
                try
                {
                    var favoritedButton = await page.QuerySelectorAsync(selector);
                    if (favoritedButton != null)
                    {
                        operationSuccess = true;
                        newState = "已收藏";
                        break;
                    }
                }
                catch { }
            }

            if (operationSuccess)
            {
                _logger?.LogInformation("收藏操作成功: {PreviousState} -> {NewState}", previousState, newState);
                return new InteractionResult(
                    Success: true,
                    Action: "收藏",
                    PreviousState: previousState,
                    CurrentState: newState,
                    Message: "收藏成功"
                );
            }
            _logger?.LogWarning("收藏操作可能失败，无法验证状态改变");
            return new InteractionResult(
                Success: false,
                Action: "收藏",
                PreviousState: previousState,
                CurrentState: previousState,
                Message: "收藏操作失败或无法验证状态",
                ErrorCode: "FAVORITE_VERIFICATION_FAILED"
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
    /// 使用三次缓动（ease-out 风格）以获得更贴近手动滚轮的视觉效果。
    /// </summary>
    /// <param name="page">页面对象</param>
    /// <param name="distance">滚动距离</param>
    /// <param name="duration">滚动时长</param>
    public async Task PerformNaturalScrollAsync(IPage page, int distance, TimeSpan duration)
    {
        try
        {
            await page.EvaluateAsync(@"
                (args) => {
                    const { distance, duration } = args;
                    return new Promise((resolve) => {
                        const startTime = performance.now();
                        const startScroll = window.pageYOffset;
                        const endScroll = startScroll + distance;
                        
                        function smoothScroll() {
                            const currentTime = performance.now();
                            const elapsed = currentTime - startTime;
                            const progress = Math.min(elapsed / duration, 1);
                            
                            // 使用easing函数使滚动更自然
                            const easeProgress = 1 - Math.pow(1 - progress, 3);
                            const currentScroll = startScroll + (distance * easeProgress);
                            
                            window.scrollTo(0, currentScroll);
                            
                            if (progress < 1) {
                                requestAnimationFrame(smoothScroll);
                            } else {
                                resolve();
                            }
                        }
                        
                        smoothScroll();
                    });
                }
            ", new { distance, duration = (int)duration.TotalMilliseconds });
            
            _logger?.LogDebug("执行自然滚动操作: 距离={Distance}px, 时长={Duration}ms", distance, duration.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "执行自然滚动操作失败");
            throw;
        }
    }
    
    #endregion
}
