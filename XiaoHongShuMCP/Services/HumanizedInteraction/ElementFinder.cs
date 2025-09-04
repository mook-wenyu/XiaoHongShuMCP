using Microsoft.Playwright;

namespace XiaoHongShuMCP.Services;

/// <summary>
/// 元素查找器实现类
/// 统一处理元素查找和重试逻辑
/// </summary>
public class ElementFinder : IElementFinder
{
    private readonly ISelectorManager _selectorManager;
    private readonly IDelayManager _delayManager;
    
    public ElementFinder(ISelectorManager selectorManager, IDelayManager delayManager)
    {
        _selectorManager = selectorManager;
        _delayManager = delayManager;
    }
    
    /// <inheritdoc />
    public async Task<IElementHandle?> FindElementAsync(IPage page, string selectorAlias, int retries = 3, int timeout = 3000)
    {
        var selectors = _selectorManager.GetSelectors(selectorAlias);
        
        for (int attempt = 1; attempt <= retries; attempt++)
        {
            foreach (var selector in selectors)
            {
                try
                {
                    // 等待元素出现
                    await page.WaitForSelectorAsync(selector, new() { Timeout = timeout });
                    var element = await page.QuerySelectorAsync(selector);
                    
                    if (element != null)
                    {
                        return element;
                    }
                }
                catch (TimeoutException)
                {
                    // 忽略超时，继续尝试下一个选择器
                    continue;
                }
                catch (Exception ex)
                {
                    // 记录其他异常但继续尝试
                    Console.WriteLine($"元素查找异常: {ex.Message}");
                    continue;
                }
            }
            
            // 如果所有选择器都失败了，等待一段时间再重试
            if (attempt < retries)
            {
                var retryDelay = _delayManager.GetRetryDelay(attempt);
                await Task.Delay(retryDelay);
            }
        }
        
        return null;
    }
    
    /// <inheritdoc />
    public async Task<List<IElementHandle>> FindElementsAsync(IPage page, string selectorAlias, int timeout = 3000)
    {
        var selectors = _selectorManager.GetSelectors(selectorAlias);
        var allElements = new List<IElementHandle>();
        
        foreach (var selector in selectors)
        {
            try
            {
                await page.WaitForSelectorAsync(selector, new() { Timeout = timeout });
                var elements = await page.QuerySelectorAllAsync(selector);
                allElements.AddRange(elements);
            }
            catch (TimeoutException)
            {
                // 忽略超时，继续尝试下一个选择器
                continue;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"批量元素查找异常: {ex.Message}");
                continue;
            }
        }
        
        return allElements.Distinct().ToList();
    }
    
    /// <inheritdoc />
    public async Task<bool> WaitForElementVisibleAsync(IPage page, string selectorAlias, int timeout = 3000)
    {
        var selectors = _selectorManager.GetSelectors(selectorAlias);
        
        foreach (var selector in selectors)
        {
            try
            {
                await page.WaitForSelectorAsync(selector, new() 
                { 
                    Timeout = timeout, 
                    State = WaitForSelectorState.Visible 
                });
                return true;
            }
            catch (TimeoutException)
            {
                // 继续尝试下一个选择器
                continue;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"等待元素可见异常: {ex.Message}");
                continue;
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// 等待元素消失
    /// </summary>
    public async Task<bool> WaitForElementHiddenAsync(IPage page, string selectorAlias, int timeout = 3000)
    {
        var selectors = _selectorManager.GetSelectors(selectorAlias);
        
        foreach (var selector in selectors)
        {
            try
            {
                await page.WaitForSelectorAsync(selector, new() 
                { 
                    Timeout = timeout, 
                    State = WaitForSelectorState.Hidden 
                });
                return true;
            }
            catch (TimeoutException)
            {
                // 继续尝试下一个选择器
                continue;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"等待元素隐藏异常: {ex.Message}");
                continue;
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// 检查元素是否可点击
    /// </summary>
    public async Task<bool> IsElementClickableAsync(IElementHandle element)
    {
        try
        {
            // 检查元素是否可见且启用
            var isVisible = await element.IsVisibleAsync();
            var isEnabled = await element.IsEnabledAsync();
            
            return isVisible && isEnabled;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"检查元素可点击状态异常: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// 智能等待元素稳定（停止移动/变化）
    /// </summary>
    public async Task<bool> WaitForElementStableAsync(IElementHandle element, int stableTimeMs = 500)
    {
        try
        {
            var previousBounds = await element.BoundingBoxAsync();
            await Task.Delay(stableTimeMs);
            var currentBounds = await element.BoundingBoxAsync();
            
            if (previousBounds == null || currentBounds == null)
            {
                return false;
            }
            
            // 检查位置和大小是否稳定
            return Math.Abs(previousBounds.X - currentBounds.X) < 1 &&
                   Math.Abs(previousBounds.Y - currentBounds.Y) < 1 &&
                   Math.Abs(previousBounds.Width - currentBounds.Width) < 1 &&
                   Math.Abs(previousBounds.Height - currentBounds.Height) < 1;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"等待元素稳定异常: {ex.Message}");
            return false;
        }
    }
}