using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
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
/// 扩展白名单键校验测试：验证 Cookies/Storage/HardwareConcurrency/DPR 等新键的判定逻辑。
/// </summary>
public class AntiDetectionToolsExtendedWhitelistTests
{
    [Test]
    public async Task Snapshot_Should_Respect_Extended_Whitelist_Keys()
    {
        var services = new ServiceCollection();
        var tempDir = Path.Combine(Path.GetTempPath(), "xhs-antidetect-xtests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        services.AddSingleton<IOptions<XhsSettings>>(_ => Options.Create(new XhsSettings
        {
            AntiDetection = new XhsSettings.AntiDetectionSection
            {
                Enabled = true,
                AuditEnabled = false,
                AuditDirectory = tempDir
            }
        }));

        // Mock IPage → 返回包含扩展字段的快照 JSON
        var pageMock = new Mock<IPage>(MockBehavior.Strict);
        var sample = JsonSerializer.Serialize(new AntiDetectionSnapshot
        {
            Ua = "Mozilla/5.0",
            Webdriver = false,
            Languages = new[] { "zh-CN", "zh" },
            Language = "zh-CN",
            TimeZone = "Asia/Shanghai",
            DevicePixelRatio = 2,
            HardwareConcurrency = 8,
            Platform = "Win32",
            WebglVendor = "Google Inc.",
            WebglRenderer = "ANGLE (Google, OpenGL)",
            CookiesEnabled = true,
            LocalStorageKeys = 1,
            SessionStorageKeys = 0
        });
        pageMock.Setup(p => p.EvaluateAsync<string>(It.IsAny<string>(), null)).ReturnsAsync(sample);

        var browserMgr = new Mock<IBrowserManager>(MockBehavior.Strict);
        browserMgr.Setup(b => b.GetPageAsync()).ReturnsAsync(pageMock.Object);
        services.AddSingleton(browserMgr.Object);

        services.AddSingleton<IPlaywrightAntiDetectionPipeline>(_ => new DefaultPlaywrightAntiDetectionPipeline());
        var sp = services.BuildServiceProvider();

        // 构造扩展白名单（要求 Cookies=true、LS>=1、SS>=0、HC>=4、DPR>=2）
        var wl = new
        {
            AllowedWebdrivers = new[] { false },
            CookiesEnabled = true,
            MinLocalStorageKeys = 1,
            MinSessionStorageKeys = 0,
            MinHardwareConcurrency = 4,
            MinDevicePixelRatio = 2
        };
        var wlPath = Path.Combine(tempDir, "whitelist-extended.json");
        await File.WriteAllTextAsync(wlPath, JsonSerializer.Serialize(wl));

        var res = await AntiDetectionSnapshotService.GetAntiDetectionSnapshot(false, tempDir, wlPath, sp);
        Assert.That(res.Success, Is.True);
        Assert.That(res.Violations == null || res.Violations!.Count == 0, Is.True);

        // 修改条件触发违反：要求 Cookies=false
        var wlBad = new
        {
            AllowedWebdrivers = new[] { false },
            CookiesEnabled = false
        };
        var wlBadPath = Path.Combine(tempDir, "whitelist-extended-bad.json");
        await File.WriteAllTextAsync(wlBadPath, JsonSerializer.Serialize(wlBad));
        var resBad = await AntiDetectionSnapshotService.GetAntiDetectionSnapshot(false, tempDir, wlBadPath, sp);
        Assert.That(resBad.Success, Is.False);
        Assert.That(resBad.Violations!.Any(v => v == "COOKIES_ENABLED_MISMATCH"), Is.True);
    }
}
