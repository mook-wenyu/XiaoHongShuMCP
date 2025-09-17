using Microsoft.Playwright;

namespace XiaoHongShuMCP.Services;

/// <summary>
/// 元素查找器实现类。
/// - 职责：基于“选择器别名”从 <see cref="IDomElementManager"/> 取得候选选择器并按顺序尝试，
///         封装等待可见/批量查找/重试与退避逻辑。
/// - 约定：不直接硬编码具体 Selector；所有 Selector 均来自别名映射，便于跨版本维护。
/// - 失败：当所有候选均失败时返回 null（或 false），由上层决定是否重试/降级。
/// </summary>
using HushOps.Core.Automation.Abstractions;

public class ElementFinder : IElementFinder
{
    private readonly IDomElementManager _domElementManager;
    private readonly HushOps.Core.Humanization.IDelayManager _delayManager;
    private readonly HushOps.Core.Selectors.ISelectorTelemetry _telemetry;
    
    public ElementFinder(IDomElementManager domElementManager, HushOps.Core.Humanization.IDelayManager delayManager, HushOps.Core.Selectors.ISelectorTelemetry telemetry)
    {
        _domElementManager = domElementManager;
        _delayManager = delayManager;
        _telemetry = telemetry;
    }
    
    /// <inheritdoc />
    /// <summary>
    /// 使用抽象页面在候选选择器集中查找元素（中文注释）。
    /// </summary>
    public async Task<IAutoElement?> FindElementAsync(IAutoPage page, string selectorAlias, int retries = 3, int timeout = 3000)
    {
        var selectors = _domElementManager.GetSelectors(selectorAlias);
        return await FindElementAsync(page, selectors, selectorAlias, retries, timeout);
    }

    /// <inheritdoc />
    public async Task<IAutoElement?> FindElementAsync(IAutoPage page, IEnumerable<string> selectors, string telemetryAlias, int retries = 3, int timeout = 3000)
    {
        var list = selectors?.ToList() ?? new List<string>();
        if (list.Count == 0) return null;

        for (int attempt = 1; attempt <= retries; attempt++)
        {
            // 基于历史命中统计对候选优先级进行一次重排（成功率优先，耗时次之）
            foreach (var selector in _telemetry.OrderByTelemetry(telemetryAlias, list))
            {
                try
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    var element = await page.QueryAsync(selector, timeout);
                    var elapsed = sw.ElapsedMilliseconds;

                    var ok = element != null;
                    _telemetry.RecordAttempt(telemetryAlias, selector, ok, elapsed, attempt);

                    if (ok) return element;
                }
                catch (TimeoutException)
                {
                    _telemetry.RecordAttempt(telemetryAlias, selector, false, timeout, attempt);
                    // 忽略超时，继续尝试下一个选择器
                }
                catch (Exception ex)
                {
                    // 记录其他异常但继续尝试
                    Console.WriteLine($"元素查找异常: {ex.Message}");
                    _telemetry.RecordAttempt(telemetryAlias, selector, false, 0, attempt);
                }
            }
            
            // 如果所有选择器都失败了，等待一段时间再重试
            if (attempt < retries)
            {
                // 统一改为 WaitAsync（加速版退避）
                await _delayManager.WaitAsync(HushOps.Core.Humanization.HumanWaitType.RetryBackoff, attempt);
            }
        }
        
        return null;
    }
    
    /// <inheritdoc />
    public async Task<List<IAutoElement>> FindElementsAsync(IAutoPage page, string selectorAlias, int timeout = 3000)
    {
        var selectors = _domElementManager.GetSelectors(selectorAlias);
        var allElements = new List<IAutoElement>();
        
        foreach (var selector in _telemetry.OrderByTelemetry(selectorAlias, selectors))
        {
            try
            {
                var elements = await page.QueryAllAsync(selector, timeout);
                if (elements != null && elements.Count > 0)
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
    public async Task<bool> WaitForElementVisibleAsync(IAutoPage page, string selectorAlias, int timeout = 3000)
    {
        var selectors = _domElementManager.GetSelectors(selectorAlias);
        
        foreach (var selector in _telemetry.OrderByTelemetry(selectorAlias, selectors))
        {
            try
            {
                var el = await page.QueryAsync(selector, timeout);
                if (el != null)
                {
                    var visible = await el.IsVisibleAsync();
                    if (visible) return true;
                }
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
