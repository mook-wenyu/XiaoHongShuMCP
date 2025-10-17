using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HushOps.Servers.XiaoHongShu.Configuration;
using HushOps.Servers.XiaoHongShu.Services.Browser;
using HushOps.FingerprintBrowser.Core;
using HushOps.Servers.XiaoHongShu.Services.Browser.Network;
using HushOps.Servers.XiaoHongShu.Services.Humanization.Interactions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Playwright;
using Xunit;
using Xunit.Abstractions;

namespace HushOps.Servers.XiaoHongShu.Tests.Humanization;

public sealed class SessionConsistencyInspectorTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;

    public SessionConsistencyInspectorTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private IPlaywright _playwright = null!;
    private IBrowser _browser = null!;
    private IBrowserContext _context = null!;
    private IPage _page = null!;
    private BrowserOpenResult _profile = null!;
    private FingerprintProfile _fingerprint = null!;
    private NetworkSessionContext _network = null!;

    public async Task InitializeAsync()
    {
        await PlaywrightTestSupport.EnsureBrowsersInstalledAsync();
        _playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        _context = await _browser.NewContextAsync();
        _page = await _context.NewPageAsync();
        await _page.SetContentAsync("<main>ready</main>");

        var navigatorSample = await _page.EvaluateAsync<NavigatorSample>(
            "() => ({ ua: navigator.userAgent ?? '', lang: navigator.language ?? '', tz: (Intl.DateTimeFormat().resolvedOptions().timeZone ?? ''), width: window.innerWidth || 0, height: window.innerHeight || 0 })");

        var fingerprint = new FingerprintProfile(
            ProfileKey: "user",
            ProfileType: ProfileType.User,
            UserAgent: navigatorSample.UserAgent,
            Platform: "Win32",
            ViewportWidth: navigatorSample.Width == 0 ? 1280 : navigatorSample.Width,
            ViewportHeight: navigatorSample.Height == 0 ? 720 : navigatorSample.Height,
            Locale: string.IsNullOrWhiteSpace(navigatorSample.Language) ? "zh-CN" : navigatorSample.Language,
            TimezoneId: string.IsNullOrWhiteSpace(navigatorSample.Timezone) ? "Asia/Shanghai" : navigatorSample.Timezone,
            HardwareConcurrency: 8,
            Vendor: "Google Inc.",
            WebglVendor: "Intel Inc.",
            WebglRenderer: "ANGLE (Intel, Intel(R) UHD Graphics Direct3D11 vs_5_0 ps_5_0, D3D11)",
            CanvasSeed: 0.5,
            WebglSeed: 0.5);

        var network = new NetworkSessionContext(
            ProxyId: "proxy-local",
            ExitIp: "",
            AverageLatencyMs: 80,
            FailureRate: 0.005,
            BandwidthSimulated: false,
            ProxyAddress: "http://127.0.0.1:8080",
            ProxyUsername: null,
            ProxyPassword: null,
            DelayMinMs: 100,
            DelayMaxMs: 200,
            MaxRetryAttempts: 3,
            RetryBaseDelayMs: 120,
            MitigationCount: 0);

        _fingerprint = fingerprint;
        _network = network;
        _profile = new BrowserOpenResult(BrowserProfileKind.User, "user", "/tmp/user", false, false, "", true, true, null, Services.Browser.BrowserConnectionMode.Auto, 9222);
    }

    public async Task DisposeAsync()
    {
        if (_page is not null)
        {
            await _page.CloseAsync();
        }

        if (_context is not null)
        {
            await _context.CloseAsync();
        }

        if (_browser is not null)
        {
            await _browser.CloseAsync();
        }

        _playwright?.Dispose();
    }

    [Fact]
    public async Task InspectAsync_ShouldReturnHealthyReport()
    {
        var inspector = new SessionConsistencyInspector(NullLogger<SessionConsistencyInspector>.Instance);
        var profileOptions = HumanBehaviorProfileOptions.CreateDefault();
        var context = CreateContext(_network);

        var report = await inspector.InspectAsync(context, profileOptions, CancellationToken.None);

        _output.WriteLine($"Report: UA={report.UserAgentMatch}, Lang={report.LanguageMatch}, TZ={report.TimezoneMatch}, Viewport={report.ViewportMatch}, ProxyConfigured={report.ProxyConfigured}, ProxySatisfied={report.ProxyRequirementSatisfied}, GpuAvailable={report.GpuInfoAvailable}, GpuSatisfied={report.GpuRequirementSatisfied}, Automation={report.AutomationIndicatorsDetected}, Warnings={string.Join(';', report.Warnings)}");
        Assert.True(report.LanguageMatch, "Language mismatch");
        Assert.True(report.ViewportMatch, "Viewport mismatch");
        Assert.Equal(!string.IsNullOrWhiteSpace(_network.ProxyAddress), report.ProxyConfigured);
        Assert.True(report.ProxyRequirementSatisfied);
        Assert.True(report.GpuRequirementSatisfied);
        Assert.True(report.AutomationIndicatorsDetected);
        Assert.Contains(report.Warnings, warning => warning.Contains("Timezone mismatch", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.Warnings, warning => warning.Contains("Automation indicator", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task InspectAsync_WhenProxyRequiredButMissing_ShouldWarn()
    {
        var inspector = new SessionConsistencyInspector(NullLogger<SessionConsistencyInspector>.Instance);
        var profileOptions = HumanBehaviorProfileOptions.CreateDefault();
        profileOptions.RequireProxy = true;
        profileOptions.AllowedProxyPrefixes = Array.Empty<string>();

        var networkWithoutProxy = _network with { ProxyAddress = "" };
        var context = CreateContext(networkWithoutProxy);

        var report = await inspector.InspectAsync(context, profileOptions, CancellationToken.None);

        _output.WriteLine($"Report: UA={report.UserAgentMatch}, Lang={report.LanguageMatch}, TZ={report.TimezoneMatch}, Viewport={report.ViewportMatch}, ProxyConfigured={report.ProxyConfigured}, ProxySatisfied={report.ProxyRequirementSatisfied}, GpuAvailable={report.GpuInfoAvailable}, GpuSatisfied={report.GpuRequirementSatisfied}, Automation={report.AutomationIndicatorsDetected}, Warnings={string.Join(';', report.Warnings)}");
        Assert.Contains(report.Warnings, warning => warning.Contains("Proxy requirement", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task InspectAsync_WhenAutomationDetected_ShouldWarn()
    {
        await _page.EvaluateAsync("() => Object.defineProperty(navigator, 'webdriver', { get: () => true, configurable: true })");

        var inspector = new SessionConsistencyInspector(NullLogger<SessionConsistencyInspector>.Instance);
        var profileOptions = HumanBehaviorProfileOptions.CreateDefault();
        profileOptions.AllowAutomationIndicators = false;
        var context = CreateContext(_network);

        var report = await inspector.InspectAsync(context, profileOptions, CancellationToken.None);

        _output.WriteLine($"Report: UA={report.UserAgentMatch}, Lang={report.LanguageMatch}, TZ={report.TimezoneMatch}, Viewport={report.ViewportMatch}, ProxyConfigured={report.ProxyConfigured}, ProxySatisfied={report.ProxyRequirementSatisfied}, GpuAvailable={report.GpuInfoAvailable}, GpuSatisfied={report.GpuRequirementSatisfied}, Automation={report.AutomationIndicatorsDetected}, Warnings={string.Join(';', report.Warnings)}");
        Assert.Contains(report.Warnings, warning => warning.Contains("Automation indicator", StringComparison.OrdinalIgnoreCase));
    }

    private BrowserPageContext CreateContext(NetworkSessionContext network)
        => new BrowserPageContext(_profile, _fingerprint, network, _page);

    private sealed record NavigatorSample(string UserAgent, string Language, string Timezone, int Width, int Height)
    {
        public NavigatorSample() : this(string.Empty, string.Empty, string.Empty, 0, 0)
        {
        }
    }
}











