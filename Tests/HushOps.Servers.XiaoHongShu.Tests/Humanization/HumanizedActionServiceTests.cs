using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HushOps.Servers.XiaoHongShu.Configuration;
using HushOps.Servers.XiaoHongShu.Infrastructure.ToolExecution;
using HushOps.Servers.XiaoHongShu.Services.Browser;
using HushOps.Servers.XiaoHongShu.Services.Browser.Fingerprint;
using HushOps.Servers.XiaoHongShu.Services.Browser.Network;
using HushOps.Servers.XiaoHongShu.Services.Humanization;
using HushOps.Servers.XiaoHongShu.Services.Humanization.Behavior;
using HushOps.Servers.XiaoHongShu.Services.Humanization.Interactions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Playwright;

namespace HushOps.Servers.XiaoHongShu.Tests.Humanization;

public sealed class HumanizedActionServiceTests : IAsyncLifetime
{
    private IPlaywright _playwright = null!;
    private IBrowser _browser = null!;
    private IPage _page = null!;
    private FingerprintContext _fingerprint = null!;
    private NetworkSessionContext _network = null!;

    public async Task InitializeAsync()
    {
        await PlaywrightTestSupport.EnsureBrowsersInstalledAsync();
        _playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var context = await _browser.NewContextAsync();
        _page = await context.NewPageAsync();

        await _page.SetContentAsync("""
        <main>
            <label>
                搜索
                <input placeholder='搜索' />
            </label>
        </main>
        """);

        var sample = await _page.EvaluateAsync<NavigatorSample>(
            "() => ({ ua: navigator.userAgent ?? '', lang: navigator.language ?? '', tz: (Intl.DateTimeFormat().resolvedOptions().timeZone ?? ''), width: window.innerWidth || 0, height: window.innerHeight || 0 })");

        _fingerprint = new FingerprintContext(
            Hash: "TEST",
            UserAgent: sample.UserAgent,
            Timezone: string.IsNullOrWhiteSpace(sample.Timezone) ? "Asia/Shanghai" : sample.Timezone,
            Language: string.IsNullOrWhiteSpace(sample.Language) ? "zh-CN" : sample.Language,
            ViewportWidth: sample.Width == 0 ? 1280 : sample.Width,
            ViewportHeight: sample.Height == 0 ? 720 : sample.Height,
            DeviceScaleFactor: 1.0,
            IsMobile: sample.Width <= 768,
            HasTouch: false,
            CanvasNoise: false,
            WebglMask: false,
            ExtraHeaders: new Dictionary<string, string>());

        _network = new NetworkSessionContext(
            ProxyId: "proxy-local",
            ExitIp: null,
            AverageLatencyMs: 120,
            FailureRate: 0.01,
            BandwidthSimulated: false,
            ProxyAddress: "http://127.0.0.1:8080",
            DelayMinMs: 100,
            DelayMaxMs: 200,
            MaxRetryAttempts: 3,
            RetryBaseDelayMs: 150,
            MitigationCount: 0);
    }

    public async Task DisposeAsync()
    {
        if (_page is not null)
        {
            await _page.Context.CloseAsync();
        }
        if (_browser is not null)
        {
            await _browser.CloseAsync();
        }
        _playwright?.Dispose();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldInvokeExecutorWithBuiltScript()
    {
        var behaviorOptions = new HumanBehaviorOptions();
        var service = CreateService(behaviorOptions);

        var outcome = await service.ExecuteAsync(
            new HumanizedActionRequest("露营", null, null, true, "user", "req-1", "default"),
            HumanizedActionKind.KeywordBrowse,
            CancellationToken.None);

        Assert.True(outcome.Success);
        Assert.Equal("ok", outcome.Status);
        Assert.Equal("露营", await _page.EvaluateAsync<string>("() => document.querySelector('input').value"));
        Assert.Equal("露营", outcome.Metadata["resolvedKeyword"]);
        Assert.Contains("InputText", outcome.Metadata["script.actions"]);
        Assert.Equal("True", outcome.Metadata["consistency.uaMatch"]);
        Assert.Equal("default", outcome.Metadata["behaviorProfile"]);
    }

    [Fact]
    public async Task PrepareAsync_ShouldProducePlanWithScriptMetadata()
    {
        var behaviorOptions = new HumanBehaviorOptions();
        var service = CreateService(behaviorOptions);

        var plan = await service.PrepareAsync(
            new HumanizedActionRequest("露营", null, null, true, "user", "plan-1", "default"),
            HumanizedActionKind.KeywordBrowse,
            CancellationToken.None);

        Assert.Equal("default", plan.BehaviorProfile);
        Assert.Equal("露营", plan.ResolvedKeyword);
        Assert.NotEmpty(plan.Script.Actions);
        Assert.Contains("script.actionCount", plan.Metadata.Keys);
    }

    private HumanizedActionService CreateService(HumanBehaviorOptions behaviorOptions)
    {
        var keywordResolver = new StubKeywordResolver();
        var delayProvider = new StubDelayProvider();
        var browserAutomation = new StubBrowserAutomationService(_page, _fingerprint, _network);
        var builder = new DefaultHumanizedActionScriptBuilder();
        var executor = new HumanizedInteractionExecutor(
            new InteractionLocatorBuilder(NullLogger<InteractionLocatorBuilder>.Instance),
            Microsoft.Extensions.Options.Options.Create(behaviorOptions),
            NullLogger<HumanizedInteractionExecutor>.Instance,
            new Random(42));
        var behaviorController = new StubBehaviorController();
        var inspector = new SessionConsistencyInspector(NullLogger<SessionConsistencyInspector>.Instance);

        return new HumanizedActionService(
            keywordResolver,
            delayProvider,
            browserAutomation,
            builder,
            executor,
            behaviorController,
            inspector,
            Microsoft.Extensions.Options.Options.Create(behaviorOptions),
            NullLogger<HumanizedActionService>.Instance);
    }

    private sealed class StubKeywordResolver : IKeywordResolver
    {
        public Task<string> ResolveAsync(string? keyword, string? portraitId, IDictionary<string, string> metadata, CancellationToken cancellationToken)
            => Task.FromResult(string.IsNullOrWhiteSpace(keyword) ? "默认" : keyword.Trim());
    }

    private sealed class StubDelayProvider : IHumanDelayProvider
    {
        public Task DelayBetweenActionsAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class StubBrowserAutomationService : IBrowserAutomationService
    {
        private readonly BrowserOpenResult _result;
        private readonly BrowserPageContext _context;

        public StubBrowserAutomationService(IPage page, FingerprintContext fingerprint, NetworkSessionContext network)
        {
            _result = new BrowserOpenResult(BrowserProfileKind.User, "user", "/tmp/user", false, false, null, true, true, null);
            _context = new BrowserPageContext(_result, fingerprint, network, page);
        }

        public Task<BrowserOpenResult> EnsureProfileAsync(string profileKey, string? profilePath, CancellationToken cancellationToken) => Task.FromResult(_result);

        public Task<BrowserOpenResult> OpenAsync(BrowserOpenRequest request, CancellationToken cancellationToken) => Task.FromResult(_result);

        public bool TryGetOpenProfile(string profileKey, out BrowserOpenResult? result)
        {
            result = _result;
            return true;
        }

        public IReadOnlyDictionary<string, BrowserOpenResult> OpenProfiles => new Dictionary<string, BrowserOpenResult> { ["user"] = _result };

        public Task NavigateRandomAsync(string browserKey, string keyword, bool waitForLoad, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task NavigateKeywordAsync(string browserKey, string keyword, bool waitForLoad, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<BrowserPageContext> EnsurePageContextAsync(string browserKey, CancellationToken cancellationToken) => Task.FromResult(_context);
    }

    private sealed class StubBehaviorController : IBehaviorController
    {
        public Task<BehaviorTrace> ExecuteAfterActionAsync(BehaviorActionContext context, BehaviorResult result)
            => Task.FromResult(new BehaviorTrace(context.ActionType, 0, Array.Empty<double>(), 0, 0, new Dictionary<string, string>()));

        public Task<BehaviorTrace> ExecuteBeforeActionAsync(BehaviorActionContext context)
            => Task.FromResult(new BehaviorTrace(context.ActionType, 0, Array.Empty<double>(), 0, 0, new Dictionary<string, string>()));
    }

    private sealed record NavigatorSample(string UserAgent, string Language, string Timezone, int Width, int Height)
    {
        public NavigatorSample() : this(string.Empty, string.Empty, string.Empty, 0, 0)
        {
        }
    }
}
