using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HushOps.Servers.XiaoHongShu.Infrastructure.ToolExecution;
using HushOps.Servers.XiaoHongShu.Services.Browser;
using HushOps.Servers.XiaoHongShu.Services.Browser.Fingerprint;
using HushOps.Servers.XiaoHongShu.Services.Browser.Network;
using HushOps.Servers.XiaoHongShu.Services.Humanization;
using HushOps.Servers.XiaoHongShu.Services.Humanization.Interactions;
using HushOps.Servers.XiaoHongShu.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HushOps.Servers.XiaoHongShu.Tests.Tools;

public sealed class InteractionStepToolTests
{
    [Fact]
    public async Task ScrollBrowseAsync_ShouldExecuteScrollActions()
    {
        // Arrange
        var mockService = new StubHumanizedActionService();
        var mockBrowserAutomation = new StubBrowserAutomationService();
        var mockExecutor = new StubHumanizedInteractionExecutor();
        var tool = new InteractionStepTool(
            mockBrowserAutomation,
            mockService,
            mockExecutor,
            NullLogger<InteractionStepTool>.Instance);

        // Act
        var result = await tool.ScrollBrowseAsync("user", "default", CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("ScrollBrowse", result.Data!.ActionType);
        Assert.Equal("已拟人化滚动浏览当前页面", result.Data.Description);
        Assert.True(mockService.ExecuteCalled);
        Assert.Equal(HumanizedActionKind.ScrollBrowse, mockService.LastKind);
    }

    [Fact]
    public async Task ScrollBrowseAsync_WithDefaultParameters_ShouldUseDefaults()
    {
        // Arrange
        var mockService = new StubHumanizedActionService();
        var mockBrowserAutomation = new StubBrowserAutomationService();
        var mockExecutor = new StubHumanizedInteractionExecutor();
        var tool = new InteractionStepTool(
            mockBrowserAutomation,
            mockService,
            mockExecutor,
            NullLogger<InteractionStepTool>.Instance);

        // Act
        var result = await tool.ScrollBrowseAsync(null, null, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(mockService.LastRequest);
        Assert.Equal("user", mockService.LastRequest!.BrowserKey);
        Assert.Equal("default", mockService.LastRequest.BehaviorProfile);
    }

    [Fact]
    public async Task ScrollBrowseAsync_WhenServiceFails_ShouldReturnFailure()
    {
        // Arrange
        var mockService = new StubHumanizedActionService(success: false);
        var mockBrowserAutomation = new StubBrowserAutomationService();
        var mockExecutor = new StubHumanizedInteractionExecutor();
        var tool = new InteractionStepTool(
            mockBrowserAutomation,
            mockService,
            mockExecutor,
            NullLogger<InteractionStepTool>.Instance);

        // Act
        var result = await tool.ScrollBrowseAsync("user", "default", CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.Data);
        Assert.Contains("滚动浏览失败", result.ErrorMessage ?? "");
    }

    private sealed class StubHumanizedActionService : IHumanizedActionService
    {
        private readonly bool _success;

        public StubHumanizedActionService(bool success = true)
        {
            _success = success;
        }

        public bool ExecuteCalled { get; private set; }
        public HumanizedActionKind? LastKind { get; private set; }
        public HumanizedActionRequest? LastRequest { get; private set; }

        public Task<HumanizedActionPlan> PrepareAsync(HumanizedActionRequest request, HumanizedActionKind kind, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<HumanizedActionOutcome> ExecuteAsync(HumanizedActionPlan plan, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<HumanizedActionOutcome> ExecuteAsync(HumanizedActionRequest request, HumanizedActionKind kind, CancellationToken cancellationToken)
        {
            ExecuteCalled = true;
            LastKind = kind;
            LastRequest = request;

            return Task.FromResult(_success
                ? HumanizedActionOutcome.Ok(new Dictionary<string, string>())
                : HumanizedActionOutcome.Fail("error", "滚动浏览失败", new Dictionary<string, string>()));
        }
    }

    private sealed class StubBrowserAutomationService : IBrowserAutomationService
    {
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

        public Task<BrowserPageContext> EnsurePageContextAsync(string profileKey, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }

    private sealed class StubHumanizedInteractionExecutor : IHumanizedInteractionExecutor
    {
        public Task ExecuteAsync(Microsoft.Playwright.IPage page, HumanizedAction action, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task ExecuteAsync(Microsoft.Playwright.IPage page, HumanizedActionScript script, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}