using Polly;
using Polly.Retry;
using System.Threading.RateLimiting;

namespace HushOps.Core.Resilience;

public static class ResiliencePipelines
{
    public static ResiliencePipeline CreateDefault()
    {
        var retry = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(200),
            })
            .Build();
        return retry;
    }

    public static RateLimiter CreateDefaultLimiter()
        => new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
        {
            AutoReplenishment = true,
            PermitLimit = 20,
            SegmentsPerWindow = 2,
            QueueLimit = 0,
            Window = TimeSpan.FromSeconds(1)
        });
}
