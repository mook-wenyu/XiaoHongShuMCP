using System;
using System.Collections.Generic;
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
public class ResumableHomefeedOperationTests
{
    [Test]
    public async Task Homefeed_Loop_Should_Reach_Target()
    {
        var mockBrowser = new Mock<IBrowserManager>();
        var mockGuard = new Mock<IPageStateGuard>();
        var mockHuman = new Mock<IHumanizedInteractionService>();
        var mockWait = new Mock<IPageLoadWaitService>();
        var mockUam = new Mock<IUniversalApiMonitor>();
        var logger = Mock.Of<ILogger<ResumableHomefeedOperation>>();
        var mockLocator = new Mock<ILocatorPolicyStack>();
        mockLocator.Setup(l => l.AcquireAsync(It.IsAny<IAutoPage>(), It.IsAny<LocatorHint>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new LocatorAcquireResult { Element = null, Strategy = string.Empty });
        var mockAutoPage = new Mock<IAutoPage>();
        mockBrowser.Setup(b => b.GetAutoPageAsync()).ReturnsAsync(mockAutoPage.Object);
        mockGuard.Setup(g => g.EnsureOnDiscoverOrSearchAsync(It.IsAny<IAutoPage>())).ReturnsAsync(true);
        mockHuman.Setup(h => h.HumanScrollAsync(It.IsAny<IAutoPage>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<System.Threading.CancellationToken>()))
                 .Returns(Task.CompletedTask);
        mockUam.Setup(u => u.SetupMonitor(It.IsAny<IAutoPage>(), It.IsAny<HashSet<ApiEndpointType>>())).Returns(true);
        mockUam.Setup(u => u.ClearMonitoredData(It.IsAny<ApiEndpointType?>()));
        mockUam.Setup(u => u.StopMonitoringAsync()).Returns(Task.CompletedTask);
        mockUam.SetupSequence(u => u.WaitForResponsesAsync(ApiEndpointType.Homefeed, It.IsAny<TimeSpan>(), 1))
               .ReturnsAsync(true)
               .ReturnsAsync(true);
        mockUam.SetupSequence(u => u.GetMonitoredNoteDetails(ApiEndpointType.Homefeed))
               .Returns(new List<NoteDetail>{ new(){ Id="a"}, new(){ Id="b"} })
               .Returns(new List<NoteDetail>{ new(){ Id="b"}, new(){ Id="c"} });

        var op = new ResumableHomefeedOperation(
            logger,
            targetMax: 3,
            maxAttempts: 5,
            browser: mockBrowser.Object,
            pageGuard: mockGuard.Object,
            human: mockHuman.Object,
            pageWait: mockWait.Object,
            uam: mockUam.Object,
            locator: mockLocator.Object,
            metrics: null);

        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"xhs_ckpt_{Guid.NewGuid():N}");
        await using var repo = new FileJsonCheckpointRepository(dir);
        var ctx = new OperationContext { OperationId = "homefeed:test", Repository = repo, CancellationToken = default };
        var r = await op.RunOrResumeAsync(ctx);
        Assert.That(r.Completed, Is.True);
        Assert.That(r.LastCheckpoint.Aggregated, Is.EqualTo(3));
        Assert.That(r.LastCheckpoint.LastBatch, Is.EqualTo(1));
    }
}
