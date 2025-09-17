using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using XiaoHongShuMCP.Services;
using XiaoHongShuMCP.Services.Resumable;
using HushOps.Core.Resumable;
using HushOps.Persistence;
using HushOps.Core.Automation.Abstractions;

namespace Tests.Services.Resumable;

/// <summary>
/// 监听+滚动分页循环的最小单元测试（通过 Mock 避免真实浏览器）。
/// - 关键点：不触发 Keyboard/Evaluate；FindElement 返回 null，HumanScroll 为 no-op。
/// - Monitor 返回两轮数据：a,b → b,c；Aggregated 应到 3 并完成。
/// </summary>
[TestFixture]
public class ResumableSearchMonitorLoopTests
{
    [Test]
    public async Task MonitorLoop_Should_Aggregate_To_Target_And_Complete()
    {
        // Mocks
        var mockBrowser = new Mock<IBrowserManager>();
        var mockGuard = new Mock<IPageStateGuard>();
        var mockHuman = new Mock<IHumanizedInteractionService>();
        var mockWait = new Mock<IPageLoadWaitService>();
        var mockUam = new Mock<IUniversalApiMonitor>();
        var mockLocator = new Mock<ILocatorPolicyStack>();
        mockLocator.Setup(l => l.AcquireAsync(It.IsAny<IAutoPage>(), It.IsAny<LocatorHint>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new LocatorAcquireResult { Element = null, Strategy = string.Empty });
        var logger = Mock.Of<ILogger<ResumableSearchOperation>>();

        // Page/IPage mocks（避免触发 Keyboard 调用：FindElement 返回 null 即可）
        var mockAutoPage = new Mock<IAutoPage>();
        var mockKeyboard = new Mock<IKeyboard>();
        mockKeyboard.Setup(k => k.PressAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>())).Returns(System.Threading.Tasks.Task.CompletedTask);
        mockAutoPage.SetupGet(p => p.Keyboard).Returns(mockKeyboard.Object);
        mockBrowser.Setup(b => b.GetAutoPageAsync()).ReturnsAsync(mockAutoPage.Object);

        mockGuard.Setup(g => g.EnsureOnDiscoverOrSearchAsync(It.IsAny<IAutoPage>())).ReturnsAsync(true);
        mockHuman.Setup(h => h.FindElementAsync(It.IsAny<IAutoPage>(), "SearchInput", It.IsAny<int>(), It.IsAny<int>()))
                 .ReturnsAsync((IAutoElement?)null);
        mockHuman.Setup(h => h.HumanScrollAsync(It.IsAny<IAutoPage>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask);
        mockWait.Setup(w => w.WaitForPageLoadAsync(It.IsAny<IAutoPage>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(PageLoadWaitResult.CreateSuccess(PageLoadStrategy.DOMContentLoaded, TimeSpan.FromMilliseconds(100)));

        mockUam.Setup(u => u.SetupMonitor(It.IsAny<IAutoPage>(), It.IsAny<HashSet<ApiEndpointType>>()))
               .Returns(true);
        mockUam.Setup(u => u.ClearMonitoredData(It.IsAny<ApiEndpointType?>()));
        mockUam.Setup(u => u.StopMonitoringAsync()).Returns(Task.CompletedTask);
        mockUam.SetupSequence(u => u.WaitForResponsesAsync(ApiEndpointType.SearchNotes, It.IsAny<TimeSpan>(), 1))
               .ReturnsAsync(true)
               .ReturnsAsync(true);
        // 两轮数据：a,b → b,c
        mockUam.SetupSequence(u => u.GetMonitoredNoteDetails(ApiEndpointType.SearchNotes))
               .Returns(new List<NoteDetail>{ new(){ Id="a", Title="A"}, new(){ Id="b", Title="B"} })
               .Returns(new List<NoteDetail>{ new(){ Id="b", Title="B"}, new(){ Id="c", Title="C"} });

        // 构建 Operation（目标 3）
        var op = new ResumableSearchOperation(
            logger: logger,
            keyword: "kw",
            maxResults: 3,
            sortBy: "comprehensive",
            noteType: "all",
            publishTime: "all",
            maxAttempts: 5,
            browser: mockBrowser.Object,
            pageGuard: mockGuard.Object,
            human: mockHuman.Object,
            pageWait: mockWait.Object,
            universalApiMonitor: mockUam.Object,
            locatorPolicy: mockLocator.Object,
            rateLimiter: null);

        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"xhs_ckpt_{Guid.NewGuid():N}");
        await using var repo = new FileJsonCheckpointRepository(dir);
        var ctx = new OperationContext { OperationId = "search:kw:loop", Repository = repo, CancellationToken = default };

        var r = await op.RunOrResumeAsync(ctx);
        Assert.That(r.Completed, Is.True);
        Assert.That(r.LastCheckpoint.Aggregated, Is.EqualTo(3));
        Assert.That(r.LastCheckpoint.LastBatch, Is.EqualTo(1));
    }
}
