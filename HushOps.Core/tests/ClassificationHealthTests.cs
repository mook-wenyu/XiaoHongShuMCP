using FluentAssertions;
using HushOps.Core.Audit;
using HushOps.Core.Network;

namespace HushOps.Core.Tests;

public class ClassificationHealthTests
{
    [Fact]
    public void UnknownRatio_Exceed_Threshold_Writes_Audit()
    {
        var dir = Path.Combine(Path.GetTempPath(), "audit_" + Guid.NewGuid());
        using var audit = new AuditRecorder(dir);
        var health = new ClassificationHealth(0.5, audit, breachConsecutive: 1);

        // 2/3 为 unknown，超过 50% 阈值，触发一次审计
        health.OnEvent(new EndpointKey("unknown", "http", "2xx"));
        health.OnEvent(new EndpointKey("unknown", "http", "2xx"));
        health.OnEvent(new EndpointKey("items.list", "http", "2xx"));

        Directory.GetFiles(dir, "*_net-unknown-threshold.jsonl").Length
            .Should().Be(1);
    }
}

