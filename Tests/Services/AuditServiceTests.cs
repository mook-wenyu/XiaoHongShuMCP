using HushOps.Core.Persistence;
using Microsoft.Extensions.Options;
using XiaoHongShuMCP.Services;

namespace Tests.Services;

public class AuditServiceTests
{
    [Test]
    public async Task WriteAsync_ShouldCreateFile_WhenEnabled()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "xhs_audit_test_" + Guid.NewGuid().ToString("N"));
        var store = new JsonLocalStore(new JsonLocalStoreOptions(tempRoot));

        var settings = new XhsSettings
        {
            Audit = new XhsSettings.AuditSection
            {
                Enabled = true,
                Directory = ".audit"
            }
        };
        var svc = new AuditService(Options.Create(settings), store);
        var evt = new InteractionAuditEvent
        {
            Action = "单元测试",
            Keyword = "测试关键词",
            DomVerified = true,
            ApiConfirmed = false,
            DurationMs = 123,
            Extra = new string('A', 1024)
        };

        await svc.WriteAsync(evt);

        var entries = await store.ListAsync(".audit");
        Assert.That(entries.Count, Is.GreaterThanOrEqualTo(1));
    }
}
