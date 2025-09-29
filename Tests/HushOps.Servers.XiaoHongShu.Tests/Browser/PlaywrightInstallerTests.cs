using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using HushOps.Servers.XiaoHongShu.Configuration;
using HushOps.Servers.XiaoHongShu.Services.Browser.Playwright;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HushOps.Servers.XiaoHongShu.Tests.Browser;

public sealed class PlaywrightInstallerTests
{
    [Fact]
    public async Task EnsureInstalledAsync_ShouldSkipWhenBrowsersPresent()
    {
        ResetInstallerState();
        var tempRoot = Path.Combine(Path.GetTempPath(), "playwright-cache-" + Guid.NewGuid().ToString("N"));
        var msPlaywright = Path.Combine(tempRoot, "ms-playwright");
        Directory.CreateDirectory(Path.Combine(msPlaywright, "chromium-test"));
        Directory.CreateDirectory(Path.Combine(msPlaywright, "ffmpeg-test"));

        var options = new PlaywrightInstallationOptions
        {
            BrowsersPath = tempRoot,
            Browsers = new List<string> { "chromium", "ffmpeg" },
            SkipIfBrowsersPresent = true
        };

        await PlaywrightInstaller.EnsureInstalledAsync(options, NullLogger.Instance, CancellationToken.None);

        Assert.True(GetInstallationCompleted());
    }

    private static void ResetInstallerState()
    {
        var field = typeof(PlaywrightInstaller).GetField("_installationCompleted", BindingFlags.Static | BindingFlags.NonPublic);
        field?.SetValue(null, false);
    }

    private static bool GetInstallationCompleted()
    {
        var field = typeof(PlaywrightInstaller).GetField("_installationCompleted", BindingFlags.Static | BindingFlags.NonPublic);
        return field is not null && (bool)field.GetValue(null)!;
    }
}
