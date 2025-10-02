using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using HushOps.Servers.XiaoHongShu.Infrastructure.ToolExecution;
using HushOps.Servers.XiaoHongShu.Services.Browser;
using HushOps.FingerprintBrowser.Core;
using HushOps.Servers.XiaoHongShu.Services.Browser.Network;
using HushOps.Servers.XiaoHongShu.Services.Humanization.Interactions;
using HushOps.Servers.XiaoHongShu.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Playwright;
using Xunit;

namespace HushOps.Servers.XiaoHongShu.Tests.Tools;

public sealed class LowLevelInteractionToolTests
{
    [Fact]
    public async Task ExecuteAsync_WithValidRequest_ShouldExecuteAction()
    {
        // Arrange
        var mockBrowserAutomation = new StubBrowserAutomationService();
        var mockExecutor = new StubHumanizedInteractionExecutor();
        var tool = new LowLevelInteractionTool(
            mockBrowserAutomation,
            mockExecutor,
            NullLogger<LowLevelInteractionTool>.Instance);

        var request = new LowLevelActionRequest(
            BrowserKey: "user",
            BehaviorProfile: "default",
            ActionType: HumanizedActionType.Click,
            Target: new ActionLocator(Role: AriaRole.Button),
            Parameters: null,
            Timing: null);

        // Act
        var result = await tool.ExecuteAsync(request, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("Click", result.Data!.ActionType);
        Assert.True(mockExecutor.ExecuteCalled);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullRequest_ShouldThrowArgumentNullException()
    {
        // Arrange
        var mockBrowserAutomation = new StubBrowserAutomationService();
        var mockExecutor = new StubHumanizedInteractionExecutor();
        var tool = new LowLevelInteractionTool(
            mockBrowserAutomation,
            mockExecutor,
            NullLogger<LowLevelInteractionTool>.Instance);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            tool.ExecuteAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task ExecuteAsync_WithDefaultParameters_ShouldUseDefaults()
    {
        // Arrange
        var mockBrowserAutomation = new StubBrowserAutomationService();
        var mockExecutor = new StubHumanizedInteractionExecutor();
        var tool = new LowLevelInteractionTool(
            mockBrowserAutomation,
            mockExecutor,
            NullLogger<LowLevelInteractionTool>.Instance);

        var request = new LowLevelActionRequest(
            BrowserKey: "",
            BehaviorProfile: "",
            ActionType: HumanizedActionType.Wheel,
            Target: null,
            Parameters: null,
            Timing: null);

        // Act
        var result = await tool.ExecuteAsync(request, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Metadata);
        Assert.Equal("user", result.Metadata!["browserKey"]);
        Assert.Equal("default", result.Metadata["behaviorProfile"]);
    }

    [Fact]
    public async Task ExecuteAsync_WithAllParameters_ShouldReturnCorrectMetadata()
    {
        // Arrange
        var mockBrowserAutomation = new StubBrowserAutomationService();
        var mockExecutor = new StubHumanizedInteractionExecutor();
        var tool = new LowLevelInteractionTool(
            mockBrowserAutomation,
            mockExecutor,
            NullLogger<LowLevelInteractionTool>.Instance);

        var request = new LowLevelActionRequest(
            BrowserKey: "custom-browser",
            BehaviorProfile: "custom-profile",
            ActionType: HumanizedActionType.InputText,
            Target: new ActionLocator(Role: AriaRole.Textbox, Placeholder: "搜索"),
            Parameters: new HumanizedActionParameters(text: "测试文本"),
            Timing: HumanizedActionTiming.Default);

        // Act
        var result = await tool.ExecuteAsync(request, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Metadata);
        Assert.Equal("custom-browser", result.Metadata!["browserKey"]);
        Assert.Equal("custom-profile", result.Metadata["behaviorProfile"]);
        Assert.Equal("InputText", result.Metadata["actionType"]);
        Assert.Equal("stub-ua", result.Metadata["fingerprintUserAgent"]);
    }

    // Stub implementations
    private sealed class StubBrowserAutomationService : IBrowserAutomationService
    {
        public Task<BrowserPageContext> EnsurePageContextAsync(string profileKey, CancellationToken cancellationToken)
        {
            var stubProfile = new BrowserOpenResult(
                Kind: BrowserProfileKind.User,
                ProfileKey: "stub",
                ProfilePath: "stub-path",
                IsNewProfile: false,
                UsedFallbackPath: false,
                ProfileDirectoryName: "stub-dir",
                AlreadyOpen: false,
                AutoOpened: false,
                SessionMetadata: null);

            var stubFingerprint = new FingerprintProfile(
                ProfileKey: "stub",
                ProfileType: ProfileType.User,
                UserAgent: "stub-ua",
                Platform: "Win32",
                ViewportWidth: 1920,
                ViewportHeight: 1080,
                Locale: "zh-CN",
                TimezoneId: "UTC",
                HardwareConcurrency: 8,
                Vendor: "Google Inc.",
                WebglVendor: "Intel Inc.",
                WebglRenderer: "ANGLE (Intel, Intel(R) UHD Graphics Direct3D11 vs_5_0 ps_5_0, D3D11)",
                CanvasSeed: 0.5,
                WebglSeed: 0.5);

            var stubNetwork = new NetworkSessionContext(
                ProxyId: "stub-proxy",
                ExitIp: "",
                AverageLatencyMs: 100.0,
                FailureRate: 0.0,
                BandwidthSimulated: false,
                ProxyAddress: "",
                DelayMinMs: 50,
                DelayMaxMs: 100,
                MaxRetryAttempts: 3,
                RetryBaseDelayMs: 1000,
                MitigationCount: 0);

            var stubContext = new BrowserPageContext(
                Profile: stubProfile,
                Fingerprint: stubFingerprint,
                Network: stubNetwork,
                Page: null!);

            return Task.FromResult(stubContext);
        }

        public Task<BrowserOpenResult> OpenAsync(BrowserOpenRequest request, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<BrowserOpenResult> EnsureProfileAsync(string profileKey, string? profilePath, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public bool TryGetOpenProfile(string profileKey, out BrowserOpenResult? result)
        {
            throw new NotImplementedException();
        }

        public IReadOnlyDictionary<string, BrowserOpenResult> OpenProfiles => new Dictionary<string, BrowserOpenResult>();

        public Task NavigateRandomAsync(string browserKey, string keyword, bool waitForLoad, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task NavigateKeywordAsync(string browserKey, string keyword, bool waitForLoad, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }

    private sealed class StubHumanizedInteractionExecutor : IHumanizedInteractionExecutor
    {
        public bool ExecuteCalled { get; private set; }

        public Task ExecuteAsync(IPage page, HumanizedAction action, CancellationToken cancellationToken)
        {
            ExecuteCalled = true;
            return Task.CompletedTask;
        }

        public Task ExecuteAsync(IPage page, HumanizedActionScript script, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}


