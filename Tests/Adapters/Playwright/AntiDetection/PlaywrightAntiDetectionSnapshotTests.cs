using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Moq;
using NUnit.Framework;
using HushOps.Core.Runtime.Playwright.AntiDetection;
using HushOps.Core.AntiDetection;

namespace Tests.Adapters.Playwright.AntiDetection;

public class PlaywrightAntiDetectionSnapshotTests
{
    [Test]
    public async Task CollectSnapshotAsync_Should_Map_Fields_From_Json()
    {
        System.Environment.SetEnvironmentVariable("XHS__InteractionPolicy__EnableJsReadEval", "true");
        System.Environment.SetEnvironmentVariable("XHS__InteractionPolicy__EvalAllowedPaths", "antidetect.snapshot,page.eval.read,element.tagName,element.html.sample,element.computedStyle,element.textProbe,element.clickability,element.probeVisibility");
        var page = new Mock<IPage>(MockBehavior.Strict);
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
            WebglRenderer = "ANGLE"
        });
        page.Setup(p => p.EvaluateAsync<string>(It.IsAny<string>(), null)).ReturnsAsync(sample);

        var pipeline = new DefaultPlaywrightAntiDetectionPipeline();
        var snap = await pipeline.CollectSnapshotAsync(page.Object);

        Assert.That(snap.Ua, Is.EqualTo("Mozilla/5.0"));
        Assert.That(snap.Webdriver, Is.False);
        Assert.That(snap.Languages!.Length, Is.EqualTo(2));
        Assert.That(snap.WebglVendor, Is.EqualTo("Google Inc."));
    }
}
