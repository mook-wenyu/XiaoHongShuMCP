using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace XiaoHongShuMCP.Services;

/// <summary>
/// 页面加载等待策略服务实现
/// 提供多级等待策略，解决WaitForLoadStateAsync硬编码超时问题
/// 支持智能降级、重试机制和详细的错误处理
/// </summary>
public class PageLoadWaitService : IPageLoadWaitService
{
    private readonly PageLoadWaitConfig _config;
    private readonly ILogger<PageLoadWaitService> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="config">页面加载等待配置</param>
    /// <param name="logger">日志记录器</param>
    public PageLoadWaitService(IOptions<PageLoadWaitConfig> config, ILogger<PageLoadWaitService> logger)
    {
        _config = config.Value;
        _logger = logger;
    }

    /// <summary>
    /// 执行多级页面加载等待策略
    /// 按照 DOMContentLoaded → Load → NetworkIdle 的顺序依次尝试
    /// 支持智能降级和重试机制
    /// </summary>
    /// <param name="page">浏览器页面实例</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>等待策略执行结果</returns>
    public async Task<PageLoadWaitResult> WaitForPageLoadAsync(IPage page, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        var retryCount = 0;

        _logger.LogDebug("开始执行多级页面加载等待策略");

        // 策略优先级：NetworkIdle → Load → DOMContentLoaded
        var strategies = new[]
        {
            PageLoadStrategy.NetworkIdle,
            PageLoadStrategy.Load,
            PageLoadStrategy.DOMContentLoaded
        };

        Exception? lastException = null;

        foreach (var strategy in strategies)
        {
            try
            {
                _logger.LogDebug("尝试使用策略: {Strategy}", strategy);

                var result = await ExecuteSingleStrategyAsync(page, strategy, null, cancellationToken);
                
                if (result.Success)
                {
                    var duration = DateTime.UtcNow - startTime;
                    var wasDegraded = strategy != PageLoadStrategy.NetworkIdle;
                    
                    _logger.LogInformation("多级等待策略成功，使用策略: {Strategy}, 耗时: {Duration}ms, 是否降级: {WasDegraded}", 
                        strategy, duration.TotalMilliseconds, wasDegraded);

                    return new PageLoadWaitResult
                    {
                        Success = true,
                        UsedStrategy = strategy,
                        Duration = duration,
                        RetryCount = retryCount,
                        WasDegraded = wasDegraded,
                        ExecutionLog = result.ExecutionLog
                    };
                }

                lastException = new Exception(result.ErrorMessage);
                
                if (_config.EnableDegradation)
                {
                    _logger.LogWarning("策略 {Strategy} 失败，尝试降级到下一个策略", strategy);
                }
                else
                {
                    break;
                }
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger.LogWarning(ex, "执行策略 {Strategy} 时发生异常", strategy);
                
                if (!_config.EnableDegradation) break;
            }
        }

        var totalDuration = DateTime.UtcNow - startTime;
        var errorMessage = lastException?.Message ?? "所有页面加载策略都失败";
        
        _logger.LogError("多级页面加载等待策略失败: {ErrorMessage}, 总耗时: {Duration}ms", 
            errorMessage, totalDuration.TotalMilliseconds);

        return PageLoadWaitResult.CreateFailure(errorMessage, ErrorType.NavigationError, totalDuration, retryCount);
    }

    /// <summary>
    /// 执行指定的单一等待策略
    /// </summary>
    /// <param name="page">浏览器页面实例</param>
    /// <param name="strategy">等待策略类型</param>
    /// <param name="timeout">自定义超时时间（可选）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>等待策略执行结果</returns>
    public async Task<PageLoadWaitResult> WaitForPageLoadAsync(IPage page, PageLoadStrategy strategy, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        return await ExecuteSingleStrategyAsync(page, strategy, timeout, cancellationToken);
    }

    /// <summary>
    /// 快速模式页面加载等待
    /// 仅使用DOMContentLoaded策略，适用于轻量级页面或性能要求较高的场景
    /// </summary>
    /// <param name="page">浏览器页面实例</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>等待策略执行结果</returns>
    public async Task<PageLoadWaitResult> WaitForPageLoadFastAsync(IPage page, CancellationToken cancellationToken = default)
    {
        var timeout = TimeSpan.FromMilliseconds(_config.FastModeTimeout);
        
        _logger.LogDebug("开始执行快速模式页面加载等待，超时时间: {Timeout}ms", _config.FastModeTimeout);

        return await ExecuteSingleStrategyAsync(page, PageLoadStrategy.DOMContentLoaded, timeout, cancellationToken);
    }

    #region 私有方法

    /// <summary>
    /// 执行单一策略的核心实现
    /// </summary>
    /// <param name="page">浏览器页面实例</param>
    /// <param name="strategy">等待策略</param>
    /// <param name="customTimeout">自定义超时时间</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>等待策略执行结果</returns>
    private async Task<PageLoadWaitResult> ExecuteSingleStrategyAsync(IPage page, PageLoadStrategy strategy, TimeSpan? customTimeout, CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;
        var retryCount = 0;
        
        var timeout = customTimeout ?? GetTimeoutForStrategy(strategy);
        var loadState = MapStrategyToLoadState(strategy);

        _logger.LogDebug("执行单一等待策略: {Strategy}, 超时时间: {Timeout}ms", strategy, timeout.TotalMilliseconds);

        for (int attempt = 0; attempt <= _config.MaxRetries; attempt++)
        {
            if (attempt > 0)
            {
                retryCount = attempt;
                var delayMs = _config.RetryDelayMs * attempt; // 指数退避
                
                _logger.LogDebug("第 {Attempt} 次重试，延时 {Delay}ms", attempt, delayMs);
                
                await Task.Delay(delayMs, cancellationToken);
            }

            try
            {
                var attemptStartTime = DateTime.UtcNow;
                
                _logger.LogDebug("尝试等待页面状态: {LoadState}", loadState);

                // 创建超时取消令牌
                using var timeoutCts = new CancellationTokenSource(timeout);
                using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                await page.WaitForLoadStateAsync(loadState, new PageWaitForLoadStateOptions
                {
                    Timeout = (float)timeout.TotalMilliseconds
                });

                var attemptDuration = DateTime.UtcNow - attemptStartTime;
                var totalDuration = DateTime.UtcNow - startTime;

                _logger.LogInformation("页面加载等待成功: {Strategy}, 本次尝试耗时: {AttemptDuration}ms, 总耗时: {TotalDuration}ms, 重试次数: {RetryCount}", 
                    strategy, attemptDuration.TotalMilliseconds, totalDuration.TotalMilliseconds, retryCount);
                
                return PageLoadWaitResult.CreateSuccess(strategy, totalDuration, retryCount);
            }
            catch (TimeoutException ex)
            {
                var attemptDuration = DateTime.UtcNow - startTime;
                _logger.LogWarning("页面加载等待超时: {Strategy}, 尝试 {Attempt}/{MaxRetries}, 耗时: {Duration}ms - {Message}", 
                    strategy, attempt + 1, _config.MaxRetries + 1, attemptDuration.TotalMilliseconds, ex.Message);
                
                if (attempt >= _config.MaxRetries)
                {
                    var totalDuration = DateTime.UtcNow - startTime;
                    return PageLoadWaitResult.CreateFailure(
                        $"页面加载等待超时，策略: {strategy}，已重试 {retryCount} 次",
                        ErrorType.NavigationError,
                        totalDuration,
                        retryCount);
                }
            }
            catch (Exception ex)
            {
                var totalDuration = DateTime.UtcNow - startTime;
                _logger.LogError(ex, "页面加载等待发生异常: {Strategy}, 尝试 {Attempt}/{MaxRetries}", 
                    strategy, attempt + 1, _config.MaxRetries + 1);
                
                if (attempt >= _config.MaxRetries)
                {
                    return PageLoadWaitResult.CreateFailure(
                        $"页面加载等待异常，策略: {strategy}，异常: {ex.Message}",
                        ErrorType.BrowserError,
                        totalDuration,
                        retryCount);
                }
            }
        }

        // 理论上不会到达这里，但为了完整性
        var finalDuration = DateTime.UtcNow - startTime;
        return PageLoadWaitResult.CreateFailure(
            $"页面加载等待失败，策略: {strategy}，已达到最大重试次数",
            ErrorType.NavigationError,
            finalDuration,
            retryCount);
    }

    /// <summary>
    /// 获取指定策略的超时时间
    /// </summary>
    /// <param name="strategy">等待策略</param>
    /// <returns>超时时间</returns>
    private TimeSpan GetTimeoutForStrategy(PageLoadStrategy strategy)
    {
        return strategy switch
        {
            PageLoadStrategy.DOMContentLoaded => TimeSpan.FromMilliseconds(_config.DOMContentLoadedTimeout),
            PageLoadStrategy.Load => TimeSpan.FromMilliseconds(_config.LoadTimeout),
            PageLoadStrategy.NetworkIdle => TimeSpan.FromMilliseconds(_config.NetworkIdleTimeout),
            _ => TimeSpan.FromMilliseconds(_config.LoadTimeout)
        };
    }

    /// <summary>
    /// 将策略枚举映射到Playwright的LoadState枚举
    /// </summary>
    /// <param name="strategy">等待策略</param>
    /// <returns>Playwright LoadState</returns>
    private static LoadState MapStrategyToLoadState(PageLoadStrategy strategy)
    {
        return strategy switch
        {
            PageLoadStrategy.DOMContentLoaded => LoadState.DOMContentLoaded,
            PageLoadStrategy.Load => LoadState.Load,
            PageLoadStrategy.NetworkIdle => LoadState.NetworkIdle,
            _ => LoadState.Load
        };
    }

    /// <summary>
    /// 检查页面是否正在加载
    /// </summary>
    /// <param name="page">浏览器页面实例</param>
    /// <returns>页面是否正在加载</returns>
    public async Task<bool> IsPageLoadingAsync(IPage page)
    {
        try
        {
            var isLoading = await page.EvaluateAsync<bool>(@"
                () => {
                    return document.readyState === 'loading' ||
                           window.performance.getEntriesByType('navigation')[0]?.loadEventEnd === 0;
                }
            ");
            
            _logger.LogDebug("页面加载状态检查: {IsLoading}", isLoading ? "正在加载" : "已完成");
            return isLoading;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "检查页面加载状态失败");
            return false;
        }
    }

    /// <summary>
    /// 等待页面加载完成
    /// </summary>
    /// <param name="page">浏览器页面实例</param>
    /// <param name="timeout">超时时间</param>
    /// <returns>是否等待成功</returns>
    public async Task<bool> WaitForLoadCompleteAsync(IPage page, TimeSpan timeout)
    {
        try
        {
            await page.WaitForLoadStateAsync(LoadState.Load, new PageWaitForLoadStateOptions
            {
                Timeout = (float)timeout.TotalMilliseconds
            });
            
            _logger.LogDebug("页面加载完成等待成功，超时时间: {Timeout}ms", timeout.TotalMilliseconds);
            return true;
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("等待页面加载完成超时: {Timeout}ms", timeout.TotalMilliseconds);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "等待页面加载完成失败");
            return false;
        }
    }

    #endregion
}