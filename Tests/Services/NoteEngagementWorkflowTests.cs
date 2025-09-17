using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HushOps.Core.Automation.Abstractions;
using Microsoft.Playwright;
using HushOps.Core.Humanization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;
using XiaoHongShuMCP.Services;
using XiaoHongShuMCP.Services.EngagementFlow;

namespace Tests.Services;

[TestFixture]
public class NoteEngagementWorkflowTests
{
    [Test]
    public async Task InteractAsync_ShouldReturnFailure_WhenNotLoggedIn()
    {
        var arrangement = Arrangement();
        arrangement.AccountManager.Setup(a => a.IsLoggedInAsync()).ReturnsAsync(false);

        var workflow = arrangement.CreateWorkflow();
        var result = await workflow.InteractAsync("kw", like: true, favorite: false);

        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorCode, Is.EqualTo("NOT_LOGGED_IN"));
    }

    [Test]
    public async Task InteractAsync_ShouldLikeSuccessfully()
    {
        var arrangement = Arrangement();
        arrangement.AccountManager.Setup(a => a.IsLoggedInAsync()).ReturnsAsync(true);

        var page = Mock.Of<IPage>();
        var autoPage = Mock.Of<IAutoPage>();

        arrangement.BrowserManager.Setup(b => b.GetPageAsync()).ReturnsAsync(page);
        arrangement.BrowserManager.Setup(b => b.GetAutoPageAsync()).ReturnsAsync(autoPage);

        arrangement.PageStateGuard.Setup(g => g.EnsureOnDiscoverOrSearchAsync(autoPage))
            .ReturnsAsync(true);

        arrangement.PageGuardian.Setup(g => g.InspectAsync(page, PageType.NoteDetail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PageStatusInfo { PageType = PageType.NoteDetail, IsPageReady = true });

        arrangement.NoteDiscovery.Setup(d => d.DoesDetailMatchKeywordAsync(page, "kw", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        arrangement.HumanizedInteraction.Setup(h => h.HumanLikeAsync())
            .ReturnsAsync(new InteractionResult(true, "点赞", "未赞", "已赞", "完成"));
        arrangement.HumanizedInteraction.Setup(h => h.HumanBetweenActionsDelayAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        arrangement.ApiMonitor.Setup(m => m.WaitForResponsesAsync(ApiEndpointType.LikeNote, It.IsAny<TimeSpan>(), 1))
            .ReturnsAsync(true);
        arrangement.ApiMonitor.Setup(m => m.GetRawResponses(ApiEndpointType.DislikeNote))
            .Returns(new List<MonitoredApiResponse>());
        arrangement.FeedbackCoordinator.Setup(f => f.ObserveAsync(ApiEndpointType.LikeNote, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiFeedback(true, "ok", new List<string>(), null));
        arrangement.FeedbackCoordinator.Setup(f => f.Audit("点赞", "kw", It.IsAny<FeedbackContext>()));
        arrangement.FeedbackCoordinator.Setup(f => f.Initialize(page, It.IsAny<IReadOnlyCollection<ApiEndpointType>>()));
        arrangement.FeedbackCoordinator.Setup(f => f.Reset(ApiEndpointType.LikeNote));
        arrangement.ApiMonitor.Setup(m => m.ClearMonitoredData(ApiEndpointType.DislikeNote));

        var workflow = arrangement.CreateWorkflow();
        var result = await workflow.InteractAsync("kw", like: true, favorite: false);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data?.Like?.Success, Is.True);
    }

    [Test]
    public async Task UnlikeAsync_ShouldReturnFailure_WhenDetailNotMatched()
    {
        var arrangement = Arrangement();
        arrangement.AccountManager.Setup(a => a.IsLoggedInAsync()).ReturnsAsync(true);

        var page = Mock.Of<IPage>();
        var autoPage = Mock.Of<IAutoPage>();

        arrangement.BrowserManager.Setup(b => b.GetPageAsync()).ReturnsAsync(page);
        arrangement.BrowserManager.Setup(b => b.GetAutoPageAsync()).ReturnsAsync(autoPage);

        arrangement.PageGuardian.Setup(g => g.InspectAsync(page, PageType.NoteDetail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PageStatusInfo { PageType = PageType.NoteDetail, IsPageReady = true });
        arrangement.NoteDiscovery.Setup(d => d.DoesDetailMatchKeywordAsync(page, "kw", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        arrangement.PageStateGuard.Setup(s => s.EnsureOnDiscoverOrSearchAsync(autoPage)).ReturnsAsync(true);
        arrangement.NoteDiscovery.Setup(d => d.FindVisibleMatchingNotesAsync("kw", 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<List<IElementHandle>>.Fail("未找到匹配笔记", ErrorType.ElementNotFound, "NO_MATCHING_NOTES"));

        var workflow = arrangement.CreateWorkflow();
        var result = await workflow.UnlikeAsync("kw");

        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorCode, Is.EqualTo("NO_MATCHING_NOTES"));
    }

    private static TestArrangement Arrangement() => new();

    private sealed class TestArrangement
    {
        public Mock<IBrowserManager> BrowserManager { get; } = new();
        public Mock<IAccountManager> AccountManager { get; } = new();
        public Mock<IPageStateGuard> PageStateGuard { get; } = new();
        public Mock<IPageGuardian> PageGuardian { get; } = new();
        public Mock<INoteDiscoveryService> NoteDiscovery { get; } = new();
        public Mock<IUniversalApiMonitor> ApiMonitor { get; } = new();
        public Mock<IFeedbackCoordinator> FeedbackCoordinator { get; } = new();
        public Mock<IHumanizedInteractionService> HumanizedInteraction { get; } = new();
        public Mock<ILogger<NoteEngagementWorkflow>> Logger { get; } = new();

        public NoteEngagementWorkflow CreateWorkflow()
        {
            var options = Options.Create(new XhsSettings());
            return new NoteEngagementWorkflow(
                Logger.Object,
                BrowserManager.Object,
                AccountManager.Object,
                PageStateGuard.Object,
                PageGuardian.Object,
                NoteDiscovery.Object,
                ApiMonitor.Object,
                FeedbackCoordinator.Object,
                HumanizedInteraction.Object,
                options);
        }
    }
}
