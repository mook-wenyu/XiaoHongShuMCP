using System;
using System.Collections.Generic;
using System.IO;
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

/// <summary>
/// ResumableSearchOperation 破坏性变更后的构造与失败路径测试（中文注释）。
/// </summary>
[TestFixture]
public class ResumableSearchOperationTests
{
    [Test]
    public void Ctor_Should_Throw_When_Dependencies_Missing()
    {
        var logger = Mock.Of<ILogger<ResumableSearchOperation>>();
        Assert.Throws<ArgumentNullException>(() => new ResumableSearchOperation(
            logger: logger,
            keyword: "kw",
            maxResults: 3,
            sortBy: "comprehensive",
            noteType: "all",
            publishTime: "all",
            maxAttempts: 3,
            browser: null!,
            pageGuard: null!,
            human: null!,
            pageWait: null!,
            universalApiMonitor: null!));
    }

    [Test]
    public async Task Run_Should_Return_Failed_Checkpoint_When_UamSetup_Fails()
    {
        var logger = Mock.Of<ILogger<ResumableSearchOperation>>();
        var mockBrowser = new Moq.Mock<IBrowserManager>();
        var mockGuard = new Moq.Mock<IPageStateGuard>();
        var mockHuman = new Moq.Mock<IHumanizedInteractionService>();
        var mockWait = new Moq.Mock<IPageLoadWaitService>();
        var mockUam = new Moq.Mock<IUniversalApiMonitor>();
        var mockLocator = new Moq.Mock<ILocatorPolicyStack>();
        mockLocator.Setup(l => l.AcquireAsync(Moq.It.IsAny<HushOps.Core.Automation.Abstractions.IAutoPage>(), Moq.It.IsAny<LocatorHint>(), Moq.It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new LocatorAcquireResult { Element = null, Strategy = string.Empty });

        // Browser/page
        var mockAutoPage = new Moq.Mock<IAutoPage>();
        var mockKeyboard = new Moq.Mock<IKeyboard>();
        mockKeyboard.Setup(k => k.PressAsync(Moq.It.IsAny<string>(), Moq.It.IsAny<int?>(), Moq.It.IsAny<System.Threading.CancellationToken>())).Returns(System.Threading.Tasks.Task.CompletedTask);
        mockAutoPage.SetupGet(p => p.Keyboard).Returns(mockKeyboard.Object);
        mockBrowser.Setup(b => b.GetAutoPageAsync()).ReturnsAsync(mockAutoPage.Object);
        mockGuard.Setup(g => g.EnsureOnDiscoverOrSearchAsync(It.IsAny<HushOps.Core.Automation.Abstractions.IAutoPage>())).ReturnsAsync(true);
        mockUam.Setup(u => u.SetupMonitor(It.IsAny<HushOps.Core.Automation.Abstractions.IAutoPage>(), It.IsAny<HashSet<ApiEndpointType>>()))
               .Returns(false); // 故意失败

        var op = new ResumableSearchOperation(
            logger: logger,
            keyword: "kw",
            maxResults: 3,
            sortBy: "comprehensive",
            noteType: "all",
            publishTime: "all",
            maxAttempts: 3,
            browser: mockBrowser.Object,
            pageGuard: mockGuard.Object,
            human: mockHuman.Object,
            pageWait: mockWait.Object,
            universalApiMonitor: mockUam.Object,
            locatorPolicy: mockLocator.Object);

        var dir = Path.Combine(Path.GetTempPath(), $"xhs_ckpt_{Guid.NewGuid():N}");
        await using var repo = new FileJsonCheckpointRepository(dir);
        var ctx = new OperationContext { OperationId = "search:kw:fail", Repository = repo, CancellationToken = CancellationToken.None };

        var r = await op.RunOrResumeAsync(ctx);
        Assert.That(r.Completed, Is.False);
        Assert.That(r.LastCheckpoint.Stage, Is.EqualTo("aggregate"));
        Assert.That(r.LastCheckpoint.LastError, Is.Not.Null);
    }
}
