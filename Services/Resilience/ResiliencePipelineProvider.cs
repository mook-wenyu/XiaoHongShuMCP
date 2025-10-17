using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace HushOps.Servers.XiaoHongShu.Services.Resilience;

/// <summary>
/// 中文：弹性管道提供者实现，提供预配置的弹性策略管道。
/// English: Resilience pipeline provider implementation that provides pre-configured resilience pipelines.
/// </summary>
public sealed class ResiliencePipelineProvider : IResiliencePipelineProvider
{
    private readonly ILogger<ResiliencePipelineProvider> _logger;
    private readonly ResiliencePipeline _networkPipeline;
    private readonly ResiliencePipeline _browserPipeline;
    private readonly ResiliencePipeline _dataAccessPipeline;

    /// <summary>
    /// 中文：初始化弹性管道提供者实例。
    /// English: Initialize resilience pipeline provider instance.
    /// </summary>
    /// <param name="logger">日志记录器 / Logger</param>
    public ResiliencePipelineProvider(ILogger<ResiliencePipelineProvider> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // 中文：配置网络调用的弹性策略（Retry + Circuit Breaker）
        // English: Configure resilience strategies for network calls (Retry + Circuit Breaker)
        _networkPipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true, // 中文：添加抖动避免重试风暴 / English: Add jitter to avoid retry storms
                OnRetry = args =>
                {
                    _logger.LogWarning(
                        "网络调用重试 | Network retry: Attempt {Attempt}/{MaxAttempts}, Delay {Delay}ms, Exception: {Exception}",
                        args.AttemptNumber,
                        3,
                        args.RetryDelay.TotalMilliseconds,
                        args.Outcome.Exception?.Message ?? "N/A");
                    return ValueTask.CompletedTask;
                }
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5, // 中文：失败率阈值 50% / English: Failure ratio threshold 50%
                SamplingDuration = TimeSpan.FromSeconds(10), // 中文：采样窗口 10 秒 / English: Sampling duration 10 seconds
                MinimumThroughput = 5, // 中文：最小请求数阈值 / English: Minimum throughput threshold
                BreakDuration = TimeSpan.FromSeconds(30), // 中文：熔断持续时间 30 秒 / English: Break duration 30 seconds
                OnOpened = args =>
                {
                    _logger.LogError(
                        "熔断器打开 | Circuit breaker opened: BreakDuration={BreakDuration}s, 后续请求将快速失败",
                        args.BreakDuration.TotalSeconds);
                    return ValueTask.CompletedTask;
                },
                OnClosed = args =>
                {
                    _logger.LogInformation(
                        "熔断器关闭 | Circuit breaker closed: 服务已恢复，允许所有请求通过");
                    return ValueTask.CompletedTask;
                },
                OnHalfOpened = args =>
                {
                    _logger.LogInformation(
                        "熔断器半开 | Circuit breaker half-opened: 允许探测请求验证服务是否恢复");
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
        
        // 中文：后续任务会配置浏览器和数据访问的策略
        // English: Subsequent tasks will configure browser and data access strategies
        _browserPipeline = ResiliencePipeline.Empty;
        _dataAccessPipeline = ResiliencePipeline.Empty;
        
        _logger.LogInformation(
            "ResiliencePipelineProvider 已初始化 | ResiliencePipelineProvider initialized: " +
            "NetworkPipeline=Retry(Max=3,Delay=1s,Exponential)+CircuitBreaker(FailureRatio=0.5,SamplingDuration=10s,MinThroughput=5,BreakDuration=30s), " +
            "BrowserPipeline=Empty, DataAccessPipeline=Empty");
    }

    /// <inheritdoc />
    public ResiliencePipeline GetNetworkPipeline() => _networkPipeline;

    /// <inheritdoc />
    public ResiliencePipeline GetBrowserPipeline() => _browserPipeline;

    /// <inheritdoc />
    public ResiliencePipeline GetDataAccessPipeline() => _dataAccessPipeline;
}
