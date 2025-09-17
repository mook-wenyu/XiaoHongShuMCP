using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using HushOps.Core.Automation.Abstractions;
using HushOps.Core.Resumable;
using HushOps.Persistence;
using XiaoHongShuMCP.Services;
using XiaoHongShuMCP.Services.Resumable;

namespace Tests.Services.Resumable;

[TestFixture]
public class ResumableInteractOperationTests
{
    [Test]
    public async Task Interact_Like_And_Favorite_Should_Complete()
    {
        var mockBrowser = new Mock<IBrowserManager>();
        var mockGuard = new Mock<IPageStateGuard>();
        var mockHuman = new Mock<IHumanizedInteractionService>();
        var mockWait = new Mock<IPageLoadWaitService>();
        var mockUam = new Mock<IUniversalApiMonitor>();
        var logger = Mock.Of<ILogger<ResumableInteractOperation>>();
        var mockLocator = new Mock<ILocatorPolicyStack>();
        mockLocator.Setup(l => l.AcquireAsync(It.IsAny<IAutoPage>(), It.IsAny<LocatorHint>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new LocatorAcquireResult { Element = null, Strategy = string.Empty });
        var mockAutoPage = new Mock<IAutoPage>();
        var mockKeyboard = new Mock<IKeyboard>();
        mockKeyboard.Setup(k => k.PressAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>())).Returns(System.Threading.Tasks.Task.CompletedTask);
        mockAutoPage.SetupGet(p => p.Keyboard).Returns(mockKeyboard.Object);
        mockBrowser.Setup(b => b.GetAutoPageAsync()).ReturnsAsync(mockAutoPage.Object);
        mockGuard.Setup(g => g.EnsureOnDiscoverOrSearchAsync(It.IsAny<IAutoPage>())).ReturnsAsync(true);
        mockHuman.Setup(h => h.HumanBetweenActionsDelayAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        mockHuman.Setup(h => h.HumanLikeAsync()).ReturnsAsync(new InteractionResult(true, "点赞", "未点赞", "已点赞", "ok"));
        mockHuman.Setup(h => h.HumanFavoriteAsync(It.IsAny<IAutoPage>())).ReturnsAsync(new InteractionResult(true, "收藏", "未收藏", "已收藏", "ok"));
        mockWait.Setup(w => w.WaitForPageLoadAsync(It.IsAny<IAutoPage>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(PageLoadWaitResult.CreateSuccess(PageLoadStrategy.DOMContentLoaded, TimeSpan.FromMilliseconds(50)));
        // 定位阶段：SearchInput 可无，结果卡片必须能找到
        mockHuman.Setup(h => h.FindElementAsync(It.IsAny<IAutoPage>(), "SearchInput", It.IsAny<int>(), It.IsAny<int>()))
                 .ReturnsAsync((IAutoElement?)null);
        var cardEl = new Mock<IAutoElement>().Object;
        mockHuman.Setup(h => h.FindElementAsync(It.IsAny<IAutoPage>(), "FirstSearchResult", It.IsAny<int>(), It.IsAny<int>()))
                 .ReturnsAsync(cardEl);
        mockHuman.Setup(h => h.HumanClickAsync(It.IsAny<IAutoElement>())).Returns(Task.CompletedTask);
        // Verify 阶段：active 按钮存在
        mockHuman.Setup(h => h.FindElementAsync(It.IsAny<IAutoPage>(), "likeButtonActive", It.IsAny<int>(), It.IsAny<int>()))
                 .ReturnsAsync(new Mock<IAutoElement>().Object);
        mockHuman.Setup(h => h.FindElementAsync(It.IsAny<IAutoPage>(), "favoriteButtonActive", It.IsAny<int>(), It.IsAny<int>()))
                 .ReturnsAsync(new Mock<IAutoElement>().Object);
        mockUam.Setup(u => u.SetupMonitor(It.IsAny<IAutoPage>(), It.IsAny<HashSet<ApiEndpointType>>())).Returns(true);
        mockUam.Setup(u => u.ClearMonitoredData(It.IsAny<ApiEndpointType?>()));
        mockUam.Setup(u => u.StopMonitoringAsync()).Returns(Task.CompletedTask);
        mockUam.Setup(u => u.WaitForResponsesAsync(ApiEndpointType.LikeNote, It.IsAny<TimeSpan>(), 1)).ReturnsAsync(true);
        mockUam.Setup(u => u.WaitForResponsesAsync(ApiEndpointType.CollectNote, It.IsAny<TimeSpan>(), 1)).ReturnsAsync(true);

        var op = new ResumableInteractOperation(
            logger,
            mockBrowser.Object,
            mockGuard.Object,
            mockHuman.Object,
            mockWait.Object,
            mockUam.Object,
            mockLocator.Object,
            metrics: null);

        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"xhs_ckpt_{Guid.NewGuid():N}");
        await using var repo = new FileJsonCheckpointRepository(dir);
        var init = InteractCheckpoint.CreateInitial("kw", doLike: true, doFavorite: true, maxAttempts: 3);
        await repo.SaveAsync(CheckpointSerializer.Pack("interact:kw:11:test", 1, init));
        var ctx = new OperationContext { OperationId = "interact:kw:11:test", Repository = repo, CancellationToken = default };

        var r = await op.RunOrResumeAsync(ctx);
        Assert.That(r.Completed, Is.True);
        Assert.That(r.LastCheckpoint.LikeResult?.Success, Is.True);
        Assert.That(r.LastCheckpoint.FavoriteResult?.Success, Is.True);
    }

    [Test]
    public async Task Interact_Verify_Uses_LocatorPolicy_When_Api_NotConfirmed()
    {
        var mockBrowser = new Mock<IBrowserManager>();
        var mockGuard = new Mock<IPageStateGuard>();
        var mockHuman = new Mock<IHumanizedInteractionService>();
        var mockWait = new Mock<IPageLoadWaitService>();
        var mockUam = new Mock<IUniversalApiMonitor>();
        var logger = Mock.Of<ILogger<ResumableInteractOperation>>();
        var mockAutoPage = new Mock<IAutoPage>();
        var mockKeyboard = new Mock<IKeyboard>();
        mockKeyboard.Setup(k => k.PressAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>())).Returns(System.Threading.Tasks.Task.CompletedTask);
        mockAutoPage.SetupGet(p => p.Keyboard).Returns(mockKeyboard.Object);
        mockBrowser.Setup(b => b.GetAutoPageAsync()).ReturnsAsync(mockAutoPage.Object);
        mockGuard.Setup(g => g.EnsureOnDiscoverOrSearchAsync(It.IsAny<IAutoPage>())).ReturnsAsync(true);
        mockHuman.Setup(h => h.HumanBetweenActionsDelayAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        mockHuman.Setup(h => h.HumanLikeAsync()).ReturnsAsync(new InteractionResult(true, "点赞", "未点赞", "已点赞", "ok"));
        mockHuman.Setup(h => h.HumanFavoriteAsync(It.IsAny<IAutoPage>())).ReturnsAsync(new InteractionResult(true, "收藏", "未收藏", "已收藏", "ok"));
        mockWait.Setup(w => w.WaitForPageLoadAsync(It.IsAny<IAutoPage>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(PageLoadWaitResult.CreateSuccess(PageLoadStrategy.DOMContentLoaded, TimeSpan.FromMilliseconds(50)));
        mockHuman.Setup(h => h.FindElementAsync(It.IsAny<IAutoPage>(), "SearchInput", It.IsAny<int>(), It.IsAny<int>()))
                 .ReturnsAsync((IAutoElement?)null);
        var cardEl = new Mock<IAutoElement>().Object;
        mockHuman.Setup(h => h.FindElementAsync(It.IsAny<IAutoPage>(), "FirstSearchResult", It.IsAny<int>(), It.IsAny<int>()))
                 .ReturnsAsync(cardEl);
        mockHuman.Setup(h => h.HumanClickAsync(It.IsAny<IAutoElement>())).Returns(Task.CompletedTask);
        // UAM 不确认（返回 false），由 DOM Verify（策略栈）确认
        mockUam.Setup(u => u.SetupMonitor(It.IsAny<IAutoPage>(), It.IsAny<HashSet<ApiEndpointType>>())).Returns(true);
        mockUam.Setup(u => u.ClearMonitoredData(It.IsAny<ApiEndpointType?>()));
        mockUam.Setup(u => u.StopMonitoringAsync()).Returns(Task.CompletedTask);
        mockUam.Setup(u => u.WaitForResponsesAsync(ApiEndpointType.LikeNote, It.IsAny<TimeSpan>(), 1)).ReturnsAsync(false);
        mockUam.Setup(u => u.WaitForResponsesAsync(ApiEndpointType.CollectNote, It.IsAny<TimeSpan>(), 1)).ReturnsAsync(false);

        var mockLocator = new Mock<ILocatorPolicyStack>();
        mockLocator.Setup(l => l.AcquireAsync(It.IsAny<IAutoPage>(), It.IsAny<LocatorHint>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new LocatorAcquireResult { Element = new Mock<IAutoElement>().Object, Strategy = "alias" });

        var op = new ResumableInteractOperation(
            logger,
            mockBrowser.Object,
            mockGuard.Object,
            mockHuman.Object,
            mockWait.Object,
            mockUam.Object,
            mockLocator.Object,
            metrics: null);

        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"xhs_ckpt_{Guid.NewGuid():N}");
        await using var repo = new FileJsonCheckpointRepository(dir);
        var init = InteractCheckpoint.CreateInitial("kw", doLike: true, doFavorite: true, maxAttempts: 1);
        await repo.SaveAsync(CheckpointSerializer.Pack("interact:kw:verify:dom", 1, init));
        var ctx = new OperationContext { OperationId = "interact:kw:verify:dom", Repository = repo, CancellationToken = default };

        var r = await op.RunOrResumeAsync(ctx);
        Assert.That(r.Completed, Is.True);
        Assert.That(r.LastCheckpoint.LikeResult?.Success, Is.True);
        Assert.That(r.LastCheckpoint.FavoriteResult?.Success, Is.True);
        Assert.That(r.LastCheckpoint.LastError, Is.Null);
    }
}
