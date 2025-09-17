using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;
using Microsoft.Playwright;
using XiaoHongShuMCP.Services;

namespace Tests.Services;

public class DetectApiFeaturesFromMonitorTests
{
    private static async Task InvokePrivateAsync(object instance, string name, params object[] args)
    {
        var method = instance.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(method, Is.Not.Null, $"未找到私有方法: {name}");
        var task = (Task)method!.Invoke(instance, args)!;
        await task.ConfigureAwait(false);
    }

    private static XiaoHongShuService BuildService(IUniversalApiMonitor monitor)
    {
        var logger = new Mock<ILogger<XiaoHongShuService>>();
        var loggerFactory = new Mock<ILoggerFactory>();
        var browserManager = new Mock<IBrowserManager>();
        var accountManager = new Mock<IAccountManager>();
        var humanized = new Mock<IHumanizedInteractionService>();
        var domMgr = new Mock<IDomElementManager>();
        var pageWait = new Mock<IPageLoadWaitService>();
        var pageGuard = new Mock<IPageStateGuard>();
        var workflow = new Mock<ICommentWorkflow>();
        var opts = Options.Create(new XhsSettings());

        var noteEngagement = new Mock<INoteEngagementWorkflow>();
        var noteDiscovery = new Mock<INoteDiscoveryService>();

        return new XiaoHongShuService(
            logger.Object,
            loggerFactory.Object,
            browserManager.Object,
            accountManager.Object,
            humanized.Object,
            domMgr.Object,
            pageWait.Object,
            pageGuard.Object,
            monitor,
            workflow.Object,
            noteEngagement.Object,
            noteDiscovery.Object,
            opts);
    }

    [Test]
    public async Task DetectApiFeatures_UsesMonitor_NoEvaluate_PopulatesFeatures()
    {
        var monitor = new Mock<IUniversalApiMonitor>(MockBehavior.Strict);
        monitor.Setup(m => m.GetRawResponses(ApiEndpointType.Homefeed))
            .Returns(new List<MonitoredApiResponse> { new MonitoredApiResponse { EndpointType = ApiEndpointType.Homefeed } });
        monitor.Setup(m => m.GetRawResponses(ApiEndpointType.SearchNotes))
            .Returns(new List<MonitoredApiResponse> { new MonitoredApiResponse { EndpointType = ApiEndpointType.SearchNotes } });
        monitor.Setup(m => m.GetRawResponses(ApiEndpointType.Feed))
            .Returns(new List<MonitoredApiResponse>());
        monitor.Setup(m => m.GetRawResponses(ApiEndpointType.Comments))
            .Returns(new List<MonitoredApiResponse>());
        monitor.Setup(m => m.GetRawResponses(ApiEndpointType.CommentPost))
            .Returns(new List<MonitoredApiResponse>());

        var service = BuildService(monitor.Object);
        var page = new Mock<IPage>();
        var status = new PageStatusInfo { CurrentUrl = "https://example/" };

        await InvokePrivateAsync(service, "DetectApiFeatures", page.Object, status);

        Assert.That(status.ApiFeatures, Does.Contain("homefeed"));
        Assert.That(status.ApiFeatures, Does.Contain("search/notes"));
        Assert.That(status.ApiFeatures, Does.Not.Contain("feed"));
        Assert.That(status.ApiFeatures, Does.Not.Contain("comment/page"));
    }
}




