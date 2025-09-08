using Microsoft.Playwright;

namespace XiaoHongShuMCP.Services;

/// <summary>
/// 元素查找器实现类。
/// - 职责：基于“选择器别名”从 <see cref="IDomElementManager"/> 取得候选选择器并按顺序尝试，
///         封装等待可见/批量查找/重试与退避逻辑。
/// - 约定：不直接硬编码具体 Selector；所有 Selector 均来自别名映射，便于跨版本维护。
/// - 失败：当所有候选均失败时返回 null（或 false），由上层决定是否重试/降级。
/// </summary>
public class ElementFinder : IElementFinder
{
    private readonly IDomElementManager _domElementManager;
    private readonly IDelayManager _delayManager;
    
    public ElementFinder(IDomElementManager domElementManager, IDelayManager delayManager)
    {
        _domElementManager = domElementManager;
        _delayManager = delayManager;
    }
    
    /// <inheritdoc />
    public async Task<IElementHandle?> FindElementAsync(IPage page, string selectorAlias, int retries = 3, int timeout = 3000)
    {
        var selectors = _domElementManager.GetSelectors(selectorAlias);
        
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
                }
                catch (Exception ex)
                {
                    // 记录其他异常但继续尝试
                    Console.WriteLine($"元素查找异常: {ex.Message}");
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
        var selectors = _domElementManager.GetSelectors(selectorAlias);
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
            }
            catch (Exception ex)
            {
                Console.WriteLine($"批量元素查找异常: {ex.Message}");
            }
        }
        
        return allElements.Distinct().ToList();
    }
    
    /// <inheritdoc />
    public async Task<bool> WaitForElementVisibleAsync(IPage page, string selectorAlias, int timeout = 3000)
    {
        var selectors = _domElementManager.GetSelectors(selectorAlias);
        
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
            }
            catch (Exception ex)
            {
                Console.WriteLine($"等待元素可见异常: {ex.Message}");
            }
        }
        
        return false;
    }

}
