using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;
using HushOps.Core.Observability;

namespace XiaoHongShuMCP.Services;

/// <summary>
/// 基于 System.Threading.RateLimiting 的令牌桶限流适配器（替换自研 TokenBucketRateLimiter）。
/// - 维度：账号 × 端点类别；
/// - 映射：将 XhsSettings.Concurrency.Rate*(Capacity/RefillPerSecond) 映射为官方 TokenBucketRateLimiter 配置；
/// - 策略：不使用自定义排队实现，采用“立即反馈 + 按 RetryAfter 轮询等待”的简单等待回路，便于可控与可观测；
/// - 线程安全：每个 key（accountId:category）对应一个限流器实例，放入并发字典。
/// </summary>
public sealed class RateLimitingRateLimiter : IRateLimiter, IRateLimiterDiagnostics
{
    private readonly IAccountManager _accountManager;
    private readonly XhsSettings.ConcurrencySection _cfg;
    private readonly ConcurrentDictionary<string, TokenBucketRateLimiter> _limiters = new(StringComparer.Ordinal);
    private readonly IMetrics? _metrics;
    private readonly ICounter? _acquired;
    private readonly IHistogram? _waitMs;
    private readonly HushOps.Core.Humanization.IPacingAdvisor? _pacing;
    private readonly ICircuitBreaker? _breaker;

    public RateLimitingRateLimiter(IAccountManager accountManager, IOptions<XhsSettings> options, IMetrics? metrics = null, HushOps.Core.Humanization.IPacingAdvisor? pacing = null, ICircuitBreaker? breaker = null)
    {
        _accountManager = accountManager;
        _cfg = options.Value?.Concurrency ?? new XhsSettings.ConcurrencySection();
        _metrics = metrics;
        _pacing = pacing;
        _breaker = breaker;
        if (_metrics != null)
        {
            _acquired = _metrics.CreateCounter("rate_limit_acquired_total", "限流获取次数");
            _waitMs = _metrics.CreateHistogram("rate_limit_wait_ms", "限流等待耗时（毫秒）");
        }
    }

    /// <summary>
    /// 获取指定端点类别的限流规则（容量与每秒补充速率）。
    /// </summary>
    private (int Capacity, int TokensPerPeriod, TimeSpan Period) GetRule(EndpointCategory c)
    {
        var r = _cfg.Rate;
        // 将 double 的“每秒补充速率”映射为（每周期补充令牌数，周期时长）整数模型
        static (int tokens, TimeSpan period) ToPeriod(double refillPerSecond)
        {
            if (refillPerSecond >= 1)
            {
                // ≥1 token/s：简化为“每秒 N 个令牌”
                var tokens = Math.Max(1, (int)Math.Round(refillPerSecond));
                return (tokens, TimeSpan.FromSeconds(1));
            }
            // <1 token/s：按倒数近似为每 N 秒 1 个令牌
            var seconds = Math.Max(1, (int)Math.Round(1.0 / Math.Max(0.000_001, refillPerSecond)));
            return (1, TimeSpan.FromSeconds(seconds));
        }

        return c switch
        {
            EndpointCategory.Like => ((int)Math.Max(1, Math.Round(r.LikeCapacity)), ToPeriod(r.LikeRefillPerSecond).tokens, ToPeriod(r.LikeRefillPerSecond).period),
            EndpointCategory.Collect => ((int)Math.Max(1, Math.Round(r.CollectCapacity)), ToPeriod(r.CollectRefillPerSecond).tokens, ToPeriod(r.CollectRefillPerSecond).period),
            EndpointCategory.Comment => ((int)Math.Max(1, Math.Round(r.CommentCapacity)), ToPeriod(r.CommentRefillPerSecond).tokens, ToPeriod(r.CommentRefillPerSecond).period),
            EndpointCategory.Search => ((int)Math.Max(1, Math.Round(r.SearchCapacity)), ToPeriod(r.SearchRefillPerSecond).tokens, ToPeriod(r.SearchRefillPerSecond).period),
            _ => ((int)Math.Max(1, Math.Round(r.FeedCapacity)), ToPeriod(r.FeedRefillPerSecond).tokens, ToPeriod(r.FeedRefillPerSecond).period)
        };
    }

    private TokenBucketRateLimiter GetOrCreateLimiter(string key, EndpointCategory category)
    {
        var (cap, tokensPer, period) = GetRule(category);
        return _limiters.GetOrAdd(key, _ => new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = cap,
            TokensPerPeriod = tokensPer,
            ReplenishmentPeriod = period,
            AutoReplenishment = true,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = int.MaxValue // 允许等待；具体等待时长通过 RetryAfter 推导
        }));
    }

    public async Task AcquireAsync(EndpointCategory category, string accountId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(accountId))
            accountId = _accountManager.CurrentUser?.UserId ?? "anonymous";
        var key = $"{accountId}:{category}";
        var limiter = GetOrCreateLimiter(key, category);
        var mult = Math.Max(1.0, _pacing?.CurrentMultiplier ?? 1.0);
        var isWrite = category is EndpointCategory.Like or EndpointCategory.Collect or EndpointCategory.Comment;
        var permits = isWrite
            ? Math.Max(1, (int)Math.Ceiling(mult))
            : Math.Max(1, (int)Math.Round(mult / 2.0));

        // Breaker 联动：当熔断打开时，写端点直接快速退避并抛出受控异常（或可选返回）
        if (isWrite && _accountManager.CurrentUser is { } u)
        {
            var breakerKey = $"{u.UserId}:write";
            if (_cfg.Breaker is { FailureThreshold: > 0 } && (_breaker?.IsOpen(breakerKey) ?? false))
            {
                var openLeft = _breaker?.RemainingOpen(breakerKey) ?? TimeSpan.FromSeconds(10);
                // 记录一次等待指标为退避（0ms），并抛出操作受限异常
                try { _waitMs?.Record(0, LabelSet.From(("endpoint", category.ToString()), ("type", "breaker_open"))); } catch { }
                throw new InvalidOperationException($"Breaker 打开中，拒绝写端点请求：{category}，剩余 {openLeft.TotalSeconds:F0}s");
            }
        }

        var sw = Stopwatch.StartNew();
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            using var lease = await limiter.AcquireAsync(permits, ct).ConfigureAwait(false);
            if (lease.IsAcquired)
            {
                try
                {
                    _acquired?.Add(1, LabelSet.From(("endpoint", category.ToString()), ("type", "limiter"), ("permits", permits), ("multiplier", mult)));
                    _waitMs?.Record(sw.Elapsed.TotalMilliseconds, LabelSet.From(("endpoint", category.ToString()), ("type", "limiter"), ("permits", permits), ("multiplier", mult)));
                }
                catch { }
                return; // 成功获得令牌
            }

            if (lease.TryGetMetadata(MetadataName.RetryAfter, out var retryObj) && retryObj is TimeSpan retry && retry > TimeSpan.Zero)
            {
                await Task.Delay(retry, ct).ConfigureAwait(false);
            }
            else
            {
                // 未提供 RetryAfter 元数据时，采用一个保守的短暂退避
                await Task.Delay(TimeSpan.FromMilliseconds(50), ct).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// 诊断快照：导出各分区可用令牌数（CurrentAvailablePermits）。
    /// </summary>
    public IReadOnlyDictionary<string, object> GetSnapshot()
    {
        var dict = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var kv in _limiters)
        {
            long? available = null;
            try { available = kv.Value.GetStatistics()?.CurrentAvailablePermits; } catch { }
            dict[kv.Key] = new { availablePermits = available };
        }
        return dict;
    }
}
