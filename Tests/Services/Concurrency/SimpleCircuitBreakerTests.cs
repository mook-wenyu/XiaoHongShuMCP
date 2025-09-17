using XiaoHongShuMCP.Services;

namespace Tests.Services.Concurrency;

public class PollyCircuitBreakerAdapterTests
{
    [Test]
    public void Breaker_Should_Open_After_Threshold()
    {
        var settings = new XhsSettings
        {
            Concurrency = new XhsSettings.ConcurrencySection
            {
                Breaker = new XhsSettings.ConcurrencySection.BreakerSection
                {
                    FailureThreshold = 3,
                    WindowSeconds = 60,
                    OpenSeconds = 120
                }
            }
        };
        var br = new PollyCircuitBreakerAdapter(Microsoft.Extensions.Options.Options.Create(settings));
        var key = "acc:write";

        Assert.That(br.IsOpen(key), Is.False);
        br.RecordFailure(key, "E1");
        br.RecordFailure(key, "E2");
        Assert.That(br.IsOpen(key), Is.False);
        br.RecordFailure(key, "E3");
        Assert.That(br.IsOpen(key), Is.True);
        Assert.That(br.RemainingOpen(key)?.TotalSeconds, Is.GreaterThan(0));
    }
}
