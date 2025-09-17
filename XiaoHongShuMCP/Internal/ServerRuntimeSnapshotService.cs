using Microsoft.Extensions.Options;
using XiaoHongShuMCP.Services;

namespace XiaoHongShuMCP.Internal;

/// <summary>
/// 服务器端（服务层）并发/限流/熔断当前配置快照导出。
/// - 仅导出配置层面速率与阈值（非实时令牌数），用于与 MCP 入口级策略对照。
/// </summary>
internal static class ServerRuntimeSnapshotService
{
    public static object? GetServerLimits(IServiceProvider sp)
    {
        var obj = sp.GetService(typeof(IOptions<XiaoHongShuMCP.Services.XhsSettings>));
        var s = (obj as IOptions<XiaoHongShuMCP.Services.XhsSettings>)?.Value;
        if (s == null) return null;
        var r = s.Concurrency.Rate;
        var b = s.Concurrency.Breaker;
        // 尝试提取运行时状态（若已在容器中注册）
        object? limiterState = null;
        object? breakerState = null;
        try
        {
            var limiter = sp.GetService(typeof(XiaoHongShuMCP.Services.IRateLimiter)) as XiaoHongShuMCP.Services.IRateLimiter;
            if (limiter is XiaoHongShuMCP.Services.IRateLimiterDiagnostics diag)
                limiterState = diag.GetSnapshot();
        }
        catch { }
        try
        {
            var breaker = sp.GetService(typeof(XiaoHongShuMCP.Services.ICircuitBreaker)) as XiaoHongShuMCP.Services.ICircuitBreaker;
            if (breaker is XiaoHongShuMCP.Services.ICircuitBreakerDiagnostics diag)
                breakerState = diag.GetSnapshot();
        }
        catch { }

        return new
        {
            rate = new
            {
                like = new { capacity = r.LikeCapacity, refillPerSecond = r.LikeRefillPerSecond },
                collect = new { capacity = r.CollectCapacity, refillPerSecond = r.CollectRefillPerSecond },
                comment = new { capacity = r.CommentCapacity, refillPerSecond = r.CommentRefillPerSecond },
                search = new { capacity = r.SearchCapacity, refillPerSecond = r.SearchRefillPerSecond },
                feed = new { capacity = r.FeedCapacity, refillPerSecond = r.FeedRefillPerSecond }
            },
            breaker = new
            {
                failureThreshold = b.FailureThreshold,
                windowSeconds = b.WindowSeconds,
                openSeconds = b.OpenSeconds
            },
            state = new { limiter = limiterState, breaker = breakerState }
        };
    }
}
