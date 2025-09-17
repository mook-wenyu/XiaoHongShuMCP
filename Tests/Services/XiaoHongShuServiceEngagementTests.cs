using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;
using XiaoHongShuMCP.Services;

namespace Tests.Services;

[TestFixture]
public class XiaoHongShuServiceEngagementTests
{
    [Test]
    public async Task LikeNoteAsync_ShouldDelegateToWorkflow()
    {
        var expected = OperationResult<InteractionResult>.Ok(new InteractionResult(true, "点赞", "前", "后", "完成"));
        var workflow = new Mock<INoteEngagementWorkflow>();
        workflow.Setup(w => w.LikeAsync("kw", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected)
            .Verifiable();

        var service = CreateService(noteEngagementWorkflow: workflow.Object);
        var result = await service.LikeNoteAsync("kw");

        workflow.Verify(w => w.LikeAsync("kw", It.IsAny<CancellationToken>()), Times.Once);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data?.Action, Is.EqualTo("点赞"));
    }

    [Test]
    public async Task FavoriteNoteAsync_ShouldDelegateToWorkflow()
    {
        var expected = OperationResult<InteractionResult>.Ok(new InteractionResult(true, "收藏", "前", "后", "完成"));
        var workflow = new Mock<INoteEngagementWorkflow>();
        workflow.Setup(w => w.FavoriteAsync("kw", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected)
            .Verifiable();

        var service = CreateService(noteEngagementWorkflow: workflow.Object);
        var result = await service.FavoriteNoteAsync("kw");

        workflow.Verify(w => w.FavoriteAsync("kw", It.IsAny<CancellationToken>()), Times.Once);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data?.Action, Is.EqualTo("收藏"));
    }

    [Test]
    public async Task InteractNoteAsync_ShouldDelegateToWorkflow()
    {
        var expected = OperationResult<InteractionBundleResult>.Ok(new InteractionBundleResult(true, null, null, "完成"));
        var workflow = new Mock<INoteEngagementWorkflow>();
        workflow.Setup(w => w.InteractAsync("kw", true, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected)
            .Verifiable();

        var service = CreateService(noteEngagementWorkflow: workflow.Object);
        var result = await service.InteractNoteAsync("kw", true, false);

        workflow.Verify(w => w.InteractAsync("kw", true, false, It.IsAny<CancellationToken>()), Times.Once);
        Assert.That(result.Success, Is.True);
    }

    [Test]
    public async Task UnlikeNoteAsync_ShouldDelegateToWorkflow()
    {
        var expected = OperationResult<InteractionResult>.Ok(new InteractionResult(true, "取消点赞", "已赞", "未赞", "完成"));
        var workflow = new Mock<INoteEngagementWorkflow>();
        workflow.Setup(w => w.UnlikeAsync("kw", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected)
            .Verifiable();

        var service = CreateService(noteEngagementWorkflow: workflow.Object);
        var result = await service.UnlikeNoteAsync("kw");

        workflow.Verify(w => w.UnlikeAsync("kw", It.IsAny<CancellationToken>()), Times.Once);
        Assert.That(result.Success, Is.True);
    }

    [Test]
    public async Task UncollectNoteAsync_ShouldDelegateToWorkflow()
    {
        var expected = OperationResult<InteractionResult>.Ok(new InteractionResult(true, "取消收藏", "已藏", "未藏", "完成"));
        var workflow = new Mock<INoteEngagementWorkflow>();
        workflow.Setup(w => w.UncollectAsync("kw", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected)
            .Verifiable();

        var service = CreateService(noteEngagementWorkflow: workflow.Object);
        var result = await service.UncollectNoteAsync("kw");

        workflow.Verify(w => w.UncollectAsync("kw", It.IsAny<CancellationToken>()), Times.Once);
        Assert.That(result.Success, Is.True);
    }

    private static XiaoHongShuService CreateService(
        INoteEngagementWorkflow? noteEngagementWorkflow = null,
        INoteDiscoveryService? noteDiscovery = null)
    {
        var logger = Mock.Of<ILogger<XiaoHongShuService>>();
        var loggerFactory = Mock.Of<ILoggerFactory>();
        var browserManager = Mock.Of<IBrowserManager>();
        var accountManager = Mock.Of<IAccountManager>();
        var humanized = Mock.Of<IHumanizedInteractionService>();
        var dom = Mock.Of<IDomElementManager>();
        var pageLoadWait = Mock.Of<IPageLoadWaitService>();
        var pageGuard = Mock.Of<IPageStateGuard>();
        var monitor = Mock.Of<IUniversalApiMonitor>();
        var commentWorkflow = Mock.Of<ICommentWorkflow>();
        noteEngagementWorkflow ??= Mock.Of<INoteEngagementWorkflow>();
        noteDiscovery ??= Mock.Of<INoteDiscoveryService>();
        var options = Options.Create(new XhsSettings());

        return new XiaoHongShuService(
            logger,
            loggerFactory,
            browserManager,
            accountManager,
            humanized,
            dom,
            pageLoadWait,
            pageGuard,
            monitor,
            commentWorkflow,
            noteEngagementWorkflow,
            noteDiscovery,
            options);
    }
}
