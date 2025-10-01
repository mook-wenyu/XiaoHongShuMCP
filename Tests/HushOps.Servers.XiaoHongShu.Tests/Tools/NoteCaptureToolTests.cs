using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HushOps.Servers.XiaoHongShu.Configuration;
using HushOps.Servers.XiaoHongShu.Infrastructure.ToolExecution;
using HushOps.Servers.XiaoHongShu.Services.Browser;
using HushOps.Servers.XiaoHongShu.Services.Browser.Fingerprint;
using HushOps.Servers.XiaoHongShu.Services.Browser.Network;
using HushOps.Servers.XiaoHongShu.Services.Humanization;
using HushOps.Servers.XiaoHongShu.Services.Humanization.Interactions;
using HushOps.Servers.XiaoHongShu.Services.Notes;
using HushOps.Servers.XiaoHongShu.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Playwright;
using HushOps.Servers.XiaoHongShu.Tests.Humanization;
using Xunit;

namespace HushOps.Servers.XiaoHongShu.Tests.Tools;

public sealed class NoteCaptureToolTests : IAsyncLifetime
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

        _fingerprint = new FingerprintContext(
            Hash: "hash",
            UserAgent: await _page.EvaluateAsync<string>("() => navigator.userAgent"),
            Timezone: "Asia/Shanghai",
            Language: "zh-CN",
            ViewportWidth: 1280,
            ViewportHeight: 720,
            DeviceScaleFactor: 1.0,
            IsMobile: false,
            HasTouch: false,
            CanvasNoise: false,
            WebglMask: false,
            ExtraHeaders: new Dictionary<string, string>(),
            HardwareConcurrency: 8,
            Vendor: "Google Inc.",
            WebglVendor: "Intel Inc.",
            WebglRenderer: "ANGLE (Intel, Intel(R) UHD Graphics Direct3D11 vs_5_0 ps_5_0, D3D11)",
            CanvasSeed: 0.5,
            WebglSeed: 0.5);

        _network = new NetworkSessionContext("proxy", null, 0, 0, false, null, 0, 0, 0, 0, 0);
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
    public async Task CaptureAsync_WithNavigation_ShouldReturnNavigationMetadata()
    {
        var tool = CreateTool(out var humanizedService, navigationShouldFail: false);

        var request = new NoteCaptureToolRequest(new[] { "露营" }, null, 5, "user", "default");

        var result = await tool.CaptureAsync(request, CancellationToken.None);

        Assert.True(result.Success);
        // Metadata now only contains requestId after simplification
        Assert.Single(result.Metadata);
        Assert.Contains("requestId", result.Metadata.Keys);
        Assert.True(humanizedService.PrepareCalled);
        Assert.True(humanizedService.ExecutePlanCalled);
    }

    [Fact]
    public async Task CaptureAsync_WhenNavigationFails_ShouldReturnError()
    {
        var tool = CreateTool(out var humanizedService, navigationShouldFail: true);

        var request = new NoteCaptureToolRequest(new[] { "露营" }, null, 5, "user", "default");

        var result = await tool.CaptureAsync(request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("ERR_NAVIGATION", result.Status);
        // Metadata now only contains requestId after simplification
        Assert.Single(result.Metadata);
        Assert.Contains("requestId", result.Metadata.Keys);
        Assert.True(humanizedService.PrepareCalled);
        Assert.True(humanizedService.ExecutePlanCalled);
    }

    private NoteCaptureTool CreateTool(out StubHumanizedActionService humanizedService, bool navigationShouldFail = false)
    {
        var captureService = new StubNoteCaptureService();
        var portraitStore = new StubPortraitStore();
        var keywordProvider = new StubKeywordProvider();
        var browserService = new StubBrowserAutomationService(_page, _fingerprint, _network);
        humanizedService = new StubHumanizedActionService { FailOnExecute = navigationShouldFail };

        return new NoteCaptureTool(
            captureService,
            portraitStore,
            keywordProvider,
            browserService,
            humanizedService,
            NullLogger<NoteCaptureTool>.Instance);
    }

    private sealed class StubNoteCaptureService : INoteCaptureService
    {
        public Task<NoteCaptureResult> CaptureAsync(NoteCaptureContext context, CancellationToken cancellationToken)
        {
            var notes = new List<NoteRecord>
            {
                new NoteRecord("id", "title", "author", "url", new Dictionary<string, string>(), new Dictionary<string, string>(), new Dictionary<string, string>())
            };

            return Task.FromResult(new NoteCaptureResult(notes, "./note.csv", null, TimeSpan.FromMilliseconds(50), new Dictionary<string, string>()));
        }
    }

    private sealed class StubPortraitStore : IAccountPortraitStore
    {
        public Task<AccountPortrait?> GetAsync(string portraitId, CancellationToken cancellationToken)
            => Task.FromResult<AccountPortrait?>(null);
    }

    private sealed class StubKeywordProvider : IDefaultKeywordProvider
    {
        public Task<string?> GetDefaultAsync(CancellationToken cancellationToken)
            => Task.FromResult<string?>("露营");
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
        public bool TryGetOpenProfile(string profileKey, out BrowserOpenResult? result) { result = _result; return true; }
        public IReadOnlyDictionary<string, BrowserOpenResult> OpenProfiles => new Dictionary<string, BrowserOpenResult> { ["user"] = _result };
        public Task NavigateRandomAsync(string browserKey, string keyword, bool waitForLoad, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task NavigateKeywordAsync(string browserKey, string keyword, bool waitForLoad, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<BrowserPageContext> EnsurePageContextAsync(string browserKey, CancellationToken cancellationToken) => Task.FromResult(_context);
    }

    private sealed class StubHumanizedActionService : IHumanizedActionService
    {
        public bool FailOnExecute { get; set; }
        public bool PrepareCalled { get; private set; }
        public bool ExecutePlanCalled { get; private set; }

        public Task<HumanizedActionPlan> PrepareAsync(HumanizedActionRequest request, HumanizedActionKind kind, CancellationToken cancellationToken)
        {
            PrepareCalled = true;
            var script = new HumanizedActionScript(new List<HumanizedAction>
            {
                HumanizedAction.Create(HumanizedActionType.InputText, ActionLocator.Empty, HumanizedActionTiming.Default, HumanizedActionParameters.Empty, request.BehaviorProfile, "input")
            });
            var actions = script.Actions.Select(a => a.Type.ToString()).ToArray();
            var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["script.actionCount"] = actions.Length.ToString(),
                ["script.actions"] = string.Join(",", actions),
                ["plan.actionCount"] = actions.Length.ToString(),
                ["plan.actions"] = string.Join(",", actions),
                ["humanized.plan.count"] = actions.Length.ToString(),
                ["humanized.plan.actions"] = string.Join(",", actions)
            };
            for (var i = 0; i < actions.Length; i++)
            {
                metadata[$"script.actions.{i}"] = actions[i];
                metadata[$"plan.actions.{i}"] = actions[i];
                metadata[$"humanized.plan.actions.{i}"] = actions[i];
            }
            var profile = new BrowserOpenResult(BrowserProfileKind.User, request.BrowserKey, "/tmp/user", false, false, null, true, true, null);
            var resolved = request.Keywords.FirstOrDefault() ?? string.Empty;
            return Task.FromResult(HumanizedActionPlan.Create(kind, request, resolved, profile, new HumanBehaviorProfileOptions(), script, metadata));
        }

        public Task<HumanizedActionOutcome> ExecuteAsync(HumanizedActionPlan plan, CancellationToken cancellationToken)
        {
            ExecutePlanCalled = true;
            if (FailOnExecute)
            {
                var failed = new Dictionary<string, string>(plan.Metadata, StringComparer.OrdinalIgnoreCase)
                {
                    ["execution.status"] = "ERR_NAVIGATION",
                    ["execution.actionCount"] = "0",
                    ["execution.actions"] = string.Empty,
                    ["humanized.execute.status"] = "failed",
                    ["humanized.execute.count"] = "0",
                    ["humanized.execute.actions"] = string.Empty
                };
                return Task.FromResult(HumanizedActionOutcome.Fail("ERR_NAVIGATION", "navigation disabled", failed));
            }

            var success = new Dictionary<string, string>(plan.Metadata, StringComparer.OrdinalIgnoreCase)
            {
                ["execution.status"] = "ok",
                ["execution.actionCount"] = plan.Script.Actions.Count.ToString(),
                ["execution.actions"] = string.Join(",", plan.Script.Actions.Select(a => a.Type.ToString())),
                ["humanized.execute.status"] = "success",
                ["humanized.execute.count"] = plan.Script.Actions.Count.ToString(),
                ["humanized.execute.actions"] = string.Join(",", plan.Script.Actions.Select(a => a.Type.ToString()))
            };
            for (var i = 0; i < plan.Script.Actions.Count; i++)
            {
                success[$"execution.actions.{i}"] = plan.Script.Actions[i].Type.ToString();
                success[$"humanized.execute.actions.{i}"] = plan.Script.Actions[i].Type.ToString();
            }
            success["consistency.warning.0"] = "mock warning";

            return Task.FromResult(HumanizedActionOutcome.Ok(success));
        }

        public Task<HumanizedActionOutcome> ExecuteAsync(HumanizedActionRequest request, HumanizedActionKind kind, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }

    private sealed record NavigatorSample(string UserAgent, string Language, string Timezone, int Width, int Height)
    {
        public NavigatorSample() : this(string.Empty, string.Empty, string.Empty, 0, 0)
        {
        }
    }
}














