using FluentAssertions;
using HushOps.Core.Audit;

namespace HushOps.Core.Tests;

public class AuditRecorderTests
{
    [Fact]
    public void Write_Creates_File()
    {
        var dir = Path.Combine(Path.GetTempPath(), "audit_" + Guid.NewGuid());
        using var audit = new AuditRecorder(dir);
        audit.Write("test", new { ok = true });
        Directory.GetFiles(dir, "*.jsonl").Length.Should().Be(1);
    }
}

