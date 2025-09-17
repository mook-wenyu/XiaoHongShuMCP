using System.Text.Json;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Moq;
using XiaoHongShuMCP.Internal;
using XiaoHongShuMCP.Services;
using XiaoHongShuMCP.Tools;

namespace XiaoHongShuMCP.Tests.Tools;

/// <summary>
/// MCP 工具集测试
/// </summary>
[TestFixture]
public class XiaoHongShuToolsTests
{
    private Mock<IAccountManager> _mockAccountManager = null!;
    private Mock<IXiaoHongShuService> _mockXiaoHongShuService = null!;
    private Mock<IBrowserManager> _mockBrowserManager = null!;
    private Mock<IHumanizedInteractionService> _mockHumanizedInteraction = null!;
    private Mock<IMcpElicitationClient> _mockElicitationClient = null!;
    private IServiceProvider _serviceProvider = null!;
    private XiaoHongShuTools _tools = null!;
    private Mock<HushOps.Core.Selectors.ISelectorTelemetry> _mockTelemetry = null!;

    [SetUp]
    public void SetUp()
    {
        _mockAccountManager = new Mock<IAccountManager>();
        _mockXiaoHongShuService = new Mock<IXiaoHongShuService>();
        _mockBrowserManager = new Mock<IBrowserManager>();
        _mockHumanizedInteraction = new Mock<IHumanizedInteractionService>();
        _mockElicitationClient = new Mock<IMcpElicitationClient>();

        _mockTelemetry = new Mock<HushOps.Core.Selectors.ISelectorTelemetry>();
        _mockTelemetry.Setup(t => t.GetStats())
            .Returns(new Dictionary<string, IReadOnlyDictionary<string, HushOps.Core.Selectors.SelectorStat>>
            {
                ["likeButton@Detail"] = new Dictionary<string, HushOps.Core.Selectors.SelectorStat>
                {
                    [".like-wrapper:has(use[href='#like'])"] = new HushOps.Core.Selectors.SelectorStat
                    {
                        Attempts = 10, Successes = 8, SuccessRate = 0.8, AvgElapsedMs = 50
                    },
                    [".like-wrapper:has(use[href='#liked'])"] = new HushOps.Core.Selectors.SelectorStat
                    {
                        Attempts = 5, Successes = 1, SuccessRate = 0.2, AvgElapsedMs = 120
                    }
                },
                ["favoriteButton@Detail"] = new Dictionary<string, HushOps.Core.Selectors.SelectorStat>
                {
                    [".collect-wrapper:has(use[href='#collect'])"] = new HushOps.Core.Selectors.SelectorStat
                    {
                        Attempts = 6, Successes = 5, SuccessRate = 0.8333, AvgElapsedMs = 60
                    }
                }
            });

        _mockBrowserManager.Setup(b => b.GetAutoPageAsync())
            .ReturnsAsync(Mock.Of<HushOps.Core.Automation.Abstractions.IAutoPage>());
        _mockHumanizedInteraction.Setup(h => h.HumanScrollAsync(It.IsAny<HushOps.Core.Automation.Abstractions.IAutoPage>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var services = new ServiceCollection();
        services.AddSingleton(_mockAccountManager.Object);
        services.AddSingleton(_mockXiaoHongShuService.Object);
        services.AddSingleton(_mockBrowserManager.Object);
        services.AddSingleton(_mockHumanizedInteraction.Object);
        services.AddSingleton(_mockTelemetry.Object);
        services.AddSingleton<IMcpElicitationClient>(_mockElicitationClient.Object);
        services.AddSingleton<XiaoHongShuTools>();

        _serviceProvider = services.BuildServiceProvider();
        _tools = _serviceProvider.GetRequiredService<XiaoHongShuTools>();
    }

    [Test]
    public void GetLocatorTelemetrySummary_ShouldReturn_TopItems()
    {
        var res = LocatorTelemetryService.GetLocatorTelemetrySummary(2, _serviceProvider);
        var dict = JsonSerializer.Serialize(res);
        Assert.That(dict, Does.Contain("\"status\":\"ok\""));
        Assert.That(dict, Does.Contain("likeButton@Detail"));
    }

    [Test]
    public async Task DumpLocatorTelemetrySnapshot_ShouldInvoke_Write_and_ReturnOk()
    {
        var temp = Path.Combine(Path.GetTempPath(), "telemetry_ut_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);

        _mockTelemetry.Setup(t => t.WriteSnapshotAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable();

        var res = await LocatorTelemetryService.DumpLocatorTelemetrySnapshot(temp, _serviceProvider);
        var json = JsonSerializer.Serialize(res);
        using var doc = JsonDocument.Parse(json);
        Assert.That(doc.RootElement.GetProperty("status").GetString(), Is.EqualTo("ok"));
        Assert.That(doc.RootElement.GetProperty("outputDir").GetString(), Is.EqualTo(temp));
        _mockTelemetry.Verify();
    }

    [Test]
    public void ListWeakLocators_ShouldReturnItems_BelowThreshold()
    {
        var res = LocatorTelemetryService.ListWeakLocators(0.5, 1, aliasFilter: "likeButton", _serviceProvider);
        var json = JsonSerializer.Serialize(res);
        Assert.That(json, Does.Contain("\"status\":\"ok\""));
        Assert.That(json, Does.Contain("\"count\":"));
    }

    [Test]
    public async Task ConnectToBrowser_WhenSuccessful_ReturnsConnectionResult()
    {
        _mockAccountManager.Setup(x => x.ConnectToBrowserAsync())
            .ReturnsAsync(OperationResult<bool>.Ok(true));

        var result = await _tools.ConnectToBrowser();

        Assert.That(result.IsConnected, Is.True);
        Assert.That(result.IsLoggedIn, Is.True);
        Assert.That(result.Message, Is.EqualTo("浏览器连接并已登录"));
    }

    [Test]
    public async Task ConnectToBrowser_WhenFailed_ReturnsErrorResult()
    {
        _mockAccountManager.Setup(x => x.ConnectToBrowserAsync())
            .ReturnsAsync(OperationResult<bool>.Fail("连接失败", ErrorType.BrowserError, "BROWSER_ERROR"));

        var result = await _tools.ConnectToBrowser();

        Assert.That(result.IsConnected, Is.False);
        Assert.That(result.IsLoggedIn, Is.False);
        Assert.That(result.Message, Is.EqualTo("连接失败"));
        Assert.That(result.ErrorCode, Is.EqualTo("BROWSER_ERROR"));
    }

    [Test]
    public async Task ConnectToBrowser_WhenException_ReturnsExceptionResult()
    {
        _mockAccountManager.Setup(x => x.ConnectToBrowserAsync())
            .ThrowsAsync(new Exception("测试异常"));

        var result = await _tools.ConnectToBrowser();

        Assert.That(result.IsConnected, Is.False);
        Assert.That(result.IsLoggedIn, Is.False);
        Assert.That(result.Message, Contains.Substring("连接异常"));
        Assert.That(result.ErrorCode, Is.EqualTo("CONNECTION_EXCEPTION"));
    }

    [Test]
    public async Task PostComment_WhenElicitationCancelled_ShouldAbort()
    {
        _mockElicitationClient
            .Setup(c => c.TryElicitAsync(It.IsAny<IMcpServer>(), It.IsAny<ElicitRequestParams>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ElicitResult
            {
                Action = "cancelled",
                Content = new Dictionary<string, JsonElement>
                {
                    ["confirm"] = JsonSerializer.SerializeToElement(false)
                }
            });

        var result = await _tools.PostComment("kw", "comment", server: Mock.Of<IMcpServer>());

        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorCode, Is.EqualTo("COMMENT_CANCELLED"));
        _mockXiaoHongShuService.Verify(x => x.PostCommentAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task PostComment_WhenElicitationProvidesUpdatedContent_ShouldUseNewText()
    {
        var updatedContent = "修订后的内容";
        _mockElicitationClient
            .Setup(c => c.TryElicitAsync(It.IsAny<IMcpServer>(), It.IsAny<ElicitRequestParams>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ElicitResult
            {
                Action = "submit",
                Content = new Dictionary<string, JsonElement>
                {
                    ["confirm"] = JsonSerializer.SerializeToElement(true),
                    ["updatedContent"] = JsonSerializer.SerializeToElement(updatedContent)
                }
            });

        var expectedResult = new CommentResult(true, "ok", "id", null);
        _mockXiaoHongShuService
            .Setup(s => s.PostCommentAsync("kw", updatedContent))
            .ReturnsAsync(OperationResult<CommentResult>.Ok(expectedResult));

        var result = await _tools.PostComment("kw", "原始内容", server: Mock.Of<IMcpServer>());

        Assert.That(result.Success, Is.True);
        _mockXiaoHongShuService.VerifyAll();
    }

    [Test]
    public async Task InteractNote_NewSignature_ShouldCall_Service()
    {
        var ok = new InteractionBundleResult(true, new InteractionResult(true, "点赞", "off", "on", "ok", null), null, "done");
        _mockXiaoHongShuService
            .Setup(x => x.InteractNoteAsync("关键词", "do", "none"))
            .ReturnsAsync(OperationResult<InteractionBundleResult>.Ok(ok));

        var res = await _tools.InteractNote("关键词", likeAction: "do", favoriteAction: "none");
        Assert.That(res.Success, Is.True);
        _mockXiaoHongShuService.VerifyAll();
    }

    [Test]
    public async Task Unlike_And_Uncollect_Tools_ShouldCall_Service()
    {
        _mockXiaoHongShuService
            .Setup(x => x.UnlikeNoteAsync("kw"))
            .ReturnsAsync(OperationResult<InteractionResult>.Ok(new InteractionResult(true, "取消点赞", "on", "off", "ok", null)));
        _mockXiaoHongShuService
            .Setup(x => x.UncollectNoteAsync("kw"))
            .ReturnsAsync(OperationResult<InteractionResult>.Ok(new InteractionResult(true, "取消收藏", "on", "off", "ok", null)));

        var r1 = await _tools.UnlikeNote("kw");
        var r2 = await _tools.UncollectNote("kw");
        Assert.That(r1.Success, Is.True);
        Assert.That(r2.Success, Is.True);
    }

    [TearDown]
    public void TearDown()
    {
        (_serviceProvider as IDisposable)?.Dispose();
        _serviceProvider = null!;
    }
}
