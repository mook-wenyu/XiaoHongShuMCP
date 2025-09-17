using FluentAssertions;
using HushOps.Core.AntiDetection;
using HushOps.Core.Audit;
using HushOps.Core.Browser;
using HushOps.Core.Config;

namespace HushOps.Core.Tests;

public class AntiDetectionPipelineTests
{
    private sealed class FakeDriver : IBrowserDriver
    {
        public Task InitializeAsync() => Task.CompletedTask;
        public Task<Microsoft.Playwright.IBrowserContext> CreateContextAsync() => throw new NotSupportedException();
        public Task<Microsoft.Playwright.IPage> NewPageAsync(Microsoft.Playwright.IBrowserContext? context = null) => throw new NotSupportedException();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    [Fact]
    public async Task Snapshot_Does_Not_ReadEval_By_Default()
    {
        var settings = new XhsSettings
        {
            BrowserSettings = new(),
            AntiDetection = new() { EnableJsReadEval = false },
            Metrics = new(),
            InteractionPolicy = new()
        };
        using var audit = new AuditRecorder(Path.Combine(Path.GetTempPath(), "audit_" + Guid.NewGuid()));
        var pipe = new DefaultPlaywrightAntiDetectionPipeline(settings, audit);
        var snap = await pipe.CaptureSnapshotAsync(new FakeDriver(), CancellationToken.None);
        snap.Should().NotBeNull();
        snap.WebdriverFlag.Should().Be("n/a");
    }
}
