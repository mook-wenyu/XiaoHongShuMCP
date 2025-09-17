using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using Moq;
using NUnit.Framework;
using HushOps.Core.Runtime.Playwright.AntiDetection;
using HushOps.Core.AntiDetection;
using XiaoHongShuMCP.Services;
using XiaoHongShuMCP.Internal;

namespace Tests.Tools;

/// <summary>
/// AntiDetectionTools.GetAntiDetectionSnapshot 工具测试：
/// - 验证可采集快照并写入审计目录；
/// - 验证白名单通过/违反两种情形；
/// - 采用 Mock 页面与管线，避免真实浏览器依赖。
/// </summary>
public class AntiDetectionToolsTests
{
    private ServiceProvider _sp = null!;
    private string _tempDir = null!;

    [SetUp]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "xhs-antidetect-mcp-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        var services = new ServiceCollection();

        // XHS 配置
        services.AddSingleton<IOptions<XhsSettings>>(_ => Options.Create(new XhsSettings
        {
            AntiDetection = new XhsSettings.AntiDetectionSection
            {
                Enabled = true,
                AuditEnabled = true,
                AuditDirectory = _tempDir
            }
        }));

        // Mock: IBrowserManager -> IPage
        var pageMock = new Mock<IPage>(MockBehavior.Strict);
        var sample = JsonSerializer.Serialize(new AntiDetectionSnapshot
        {
            Ua = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/126.0.0.0 Safari/537.36",
            Webdriver = false,
            Languages = new[] { "zh-CN", "zh" },
            Language = "zh-CN",
            TimeZone = "Asia/Shanghai",
            DevicePixelRatio = 2,
            HardwareConcurrency = 8,
            Platform = "Win32",
            WebglVendor = "Google Inc.",
            WebglRenderer = "ANGLE (Google, OpenGL)"
        });
        pageMock.Setup(p => p.EvaluateAsync<string>(It.IsAny<string>(), null)).ReturnsAsync(sample);

        var browserMgr = new Mock<IBrowserManager>(MockBehavior.Strict);
        browserMgr.Setup(b => b.GetPageAsync()).ReturnsAsync(pageMock.Object);
        services.AddSingleton(browserMgr.Object);

        // 使用真实 DefaultPlaywrightAntiDetectionPipeline，仅用其 CollectSnapshotAsync（不会连接真实浏览器）
        services.AddSingleton<IPlaywrightAntiDetectionPipeline>(_ => new DefaultPlaywrightAntiDetectionPipeline());

        services.AddLogging();
        _sp = services.BuildServiceProvider();
    }

    [TearDown]
    public void Cleanup()
    {
        try { _sp.Dispose(); } catch { }
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    [Test]
    public async Task GetAntiDetectionSnapshot_Should_Write_Audit_File_When_Enabled()
    {
        var res = await AntiDetectionSnapshotService.GetAntiDetectionSnapshot(writeAudit: true, auditDirectory: _tempDir, whitelistPath: null, _sp);
        Assert.That(res.Success, Is.True);
        Assert.That(res.Snapshot.Platform, Is.EqualTo("Win32"));
        Assert.That(res.AuditPath, Is.Not.Null);
        Assert.That(File.Exists(res.AuditPath!), Is.True);
        var json = await File.ReadAllTextAsync(res.AuditPath!);
        Assert.That(json.Contains("\"WebglVendor\""), Is.True);
    }

    [Test]
    public async Task GetAntiDetectionSnapshot_Should_Pass_Whitelist()
    {
        // 构造白名单文件（允许 Win32/Asia/Shanghai/Webdriver=false/UA含 Mozilla）
        var wl = new
        {
            AllowedPlatforms = new[] { "Win32", "MacIntel" },
            AllowedTimeZones = new[] { "Asia/Shanghai", "America/Los_Angeles" },
            AllowedWebdrivers = new[] { false },
            UserAgentMustContain = new[] { "Mozilla/5.0" },
            UserAgentMustNotContain = new[] { "HeadlessChrome" },
            WebglVendors = new[] { "Google Inc." },
            WebglRendererRegex = new[] { "ANGLE\\s*\\(.*\\)" },
            LanguagesPrefixAny = new[] { "zh-CN", "en-US" }
        };
        var wlPath = Path.Combine(_tempDir, "whitelist-ok.json");
        await File.WriteAllTextAsync(wlPath, JsonSerializer.Serialize(wl));

        var res = await AntiDetectionSnapshotService.GetAntiDetectionSnapshot(true, _tempDir, wlPath, _sp);
        Assert.That(res.Success, Is.True);
        Assert.That(res.Violations == null || res.Violations!.Count == 0, Is.True);
    }

    [Test]
    public async Task GetAntiDetectionSnapshot_Should_Fail_When_UA_Denied()
    {
        // 使用禁止 UA 片段的白名单
        var wl = new
        {
            AllowedWebdrivers = new[] { false },
            UserAgentMustNotContain = new[] { "Chrome/126" } // 故意拦截
        };
        var wlPath = Path.Combine(_tempDir, "whitelist-bad.json");
        await File.WriteAllTextAsync(wlPath, JsonSerializer.Serialize(wl));

        var res = await AntiDetectionSnapshotService.GetAntiDetectionSnapshot(false, _tempDir, wlPath, _sp);
        Assert.That(res.Success, Is.False);
        Assert.That(res.Violations, Is.Not.Null);
        Assert.That(res.Violations!.Any(v => v.StartsWith("UA_MUST_NOT_CONTAIN")), Is.True);
    }
}
