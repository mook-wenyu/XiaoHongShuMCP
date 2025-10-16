using System.Threading;
using System.Threading.Tasks;
using HushOps.FingerprintBrowser.Core;
using HushOps.Servers.XiaoHongShu.Services.Browser.Network;
using HushOps.FingerprintBrowser.Playwright;
using HushOps.FingerprintBrowser.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Playwright;
using Xunit;

namespace HushOps.Servers.XiaoHongShu.Tests;

public class BrowserHostMappingTests
{
    [Fact(Skip = "受运行环境限制（无 dotnet/无 Edge 通道），仅用于本地验证时启用")]
    public async Task TemporaryContext_ShouldApplyAcceptLanguageAndProxy()
    {
        using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        var host = new BrowserHost(playwright, NullLogger<BrowserHost>.Instance);

        var profile = new FingerprintProfile(
            ProfileKey: "test",
            ProfileType: ProfileType.Synthetic,
            UserAgent: "Mozilla/5.0",
            Platform: "Win32",
            ViewportWidth: 1280,
            ViewportHeight: 720,
            Locale: "zh-CN",
            TimezoneId: "Asia/Shanghai",
            HardwareConcurrency: 8,
            Vendor: "Google Inc.",
            WebglVendor: "Intel Inc.",
            WebglRenderer: "ANGLE",
            CanvasSeed: 0.1,
            WebglSeed: 0.2
        );

        var network = new NetworkSessionOptions
        {
            ProxyAddress = null,
            WebRtcPolicy = "default-route-only"
        };

        var context = await host.NewTemporaryContextAsync(profile, network, CancellationToken.None);
        Assert.NotNull(context);
        await context!.CloseAsync();
    }
}
