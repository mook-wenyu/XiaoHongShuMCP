using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using XiaoHongShuMCP.Internal;

namespace Tests.Tools;

public class AntiDetectionReportToolsTests
{
    private string _tempAudit = null!;
    private string _tempDocs = null!;
    private string _wlFile = null!;
    private ServiceProvider _sp = null!;

    [SetUp]
    public void Setup()
    {
        _tempAudit = Path.Combine(Path.GetTempPath(), "xhs-antidetect-audit", Guid.NewGuid().ToString("N"));
        _tempDocs = Path.Combine(Path.GetTempPath(), "xhs-antidetect-docs", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempAudit);
        Directory.CreateDirectory(Path.Combine(_tempDocs, "anti-detect"));
        Directory.CreateDirectory(Path.Combine(_tempDocs, "adr"));

        // 写入 2 份快照：一份 OK，一份违反规则
        var today = DateTime.UtcNow.ToString("yyyyMMdd");
        var okSnap = new { Ua = "Mozilla/5.0", Webdriver = false, Platform = "Win32", TimeZone = "Asia/Shanghai", Languages = new[] {"zh-CN"} };
        var badSnap = new { Ua = "HeadlessChrome", Webdriver = true, Platform = "Other", TimeZone = "UTC" };
        File.WriteAllText(Path.Combine(_tempAudit, $"antidetect-snapshot-{today}-ok.json"), JsonSerializer.Serialize(okSnap));
        File.WriteAllText(Path.Combine(_tempAudit, $"antidetect-snapshot-{today}-bad.json"), JsonSerializer.Serialize(badSnap));

        // 白名单：允许 Win32/Asia/Shanghai，要求 webdriver=false，不允许 UA 包含 HeadlessChrome
        var wl = new
        {
            AllowedWebdrivers = new[] { false },
            AllowedPlatforms = new[] { "Win32" },
            AllowedTimeZones = new[] { "Asia/Shanghai" },
            UserAgentMustNotContain = new[] { "HeadlessChrome" }
        };
        _wlFile = Path.Combine(_tempDocs, "anti-detect", "whitelist.json");
        File.WriteAllText(_wlFile, JsonSerializer.Serialize(wl));

        var settings = new HushOps.Core.Config.XhsSettings
        {
            BrowserSettings = new HushOps.Core.Config.XhsSettings.BrowserOptions(),
            AntiDetection = new HushOps.Core.Config.XhsSettings.AntiDetectionOptions { AuditDirectory = _tempAudit },
            Metrics = new HushOps.Core.Config.XhsSettings.MetricsOptions(),
            InteractionPolicy = new HushOps.Core.Config.XhsSettings.InteractionPolicyOptions(),
            Mcp = new HushOps.Core.Config.XhsSettings.McpOptions()
        };
        _sp = new ServiceCollection()
            .AddSingleton<IOptions<HushOps.Core.Config.XhsSettings>>(_ => Options.Create(settings))
            .BuildServiceProvider();
    }

    [TearDown]
    public void Cleanup()
    {
        try { Directory.Delete(_tempAudit, true); } catch {}
        try { Directory.Delete(_tempDocs, true); } catch {}
        try { _sp.Dispose(); } catch {}
    }

    [Test]
    public async Task DailyReport_Should_Summarize_And_Write_File()
    {
        var res = await AntiDetectionReportService.GenerateDailyReport(null, _wlFile, _sp);
        Assert.That(res.Samples, Is.EqualTo(2));
        Assert.That(res.Violated, Is.GreaterThanOrEqualTo(1));
        Assert.That(File.Exists(res.OutputPath), Is.True);
    }
}
