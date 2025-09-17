using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;
using XiaoHongShuMCP.Services;
using ServiceXhsSettings = XiaoHongShuMCP.Services.XhsSettings;
using HushOps.Core.Runtime.Playwright.AntiDetection;
using Microsoft.Playwright;

namespace XiaoHongShuMCP.Tests.Services;

/// <summary>
/// 验证：浏览器后台连接服务在连接成功后，会集中调用反检测管线对上下文执行策略应用。
/// </summary>
public class BrowserConnectionHostedServiceTests
{
    [Test]
    public async Task Execute_Should_Apply_AntiDetectionPipeline_On_Connect()
    {
        // Arrange
        var services = new ServiceCollection();

        var account = new Mock<IAccountManager>(MockBehavior.Strict);
        account.SetupSequence(a => a.IsLoggedInAsync())
               .ReturnsAsync(false)
               .ReturnsAsync(true);
        account.Setup(a => a.ConnectToBrowserAsync())
               .ReturnsAsync(OperationResult<bool>.Ok(false));
        services.AddSingleton(account.Object);

        var ctx = new Mock<IBrowserContext>(MockBehavior.Strict);
        var mgr = new Mock<IBrowserManager>(MockBehavior.Strict);
        mgr.Setup(m => m.GetBrowserContextAsync()).ReturnsAsync(ctx.Object);
        mgr.Setup(m => m.EnsureSessionFreshAsync(It.IsAny<CancellationToken>()))
           .Returns(Task.CompletedTask)
           .Verifiable();
        services.AddSingleton(mgr.Object);

        var pipeline = new Mock<IPlaywrightAntiDetectionPipeline>(MockBehavior.Strict);
        pipeline.Setup(p => p.ApplyAsync(ctx.Object, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask)
                .Verifiable();
        services.AddSingleton(pipeline.Object);

        var waitResult = PageLoadWaitResult.CreateSuccess(PageLoadStrategy.DOMContentLoaded, TimeSpan.Zero);
        var waitService = new Mock<IPageLoadWaitService>();
        waitService.Setup(s => s.WaitForPageLoadAsync(It.IsAny<HushOps.Core.Automation.Abstractions.IAutoPage>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(waitResult);
        services.AddSingleton(waitService.Object);
        services.AddSingleton<IDomElementManager>(Mock.Of<IDomElementManager>());
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        var serviceSettings = new ServiceXhsSettings();
        serviceSettings.BrowserSettings.Connection.InitialDelaySeconds = 0;
        serviceSettings.BrowserSettings.Connection.RetryIntervalSeconds = 1;
        serviceSettings.BrowserSettings.Connection.RetryIntervalMaxSeconds = 2;
        serviceSettings.BrowserSettings.Connection.HealthCheckIntervalSeconds = 1;
        serviceSettings.BrowserSettings.Connection.CookieRenewalThresholdMinutes = 0;
        serviceSettings.BrowserSettings.Connection.ContextRecycleMinutes = 0;
        var options = Options.Create(serviceSettings);
        services.AddSingleton<IOptions<ServiceXhsSettings>>(_ => options);

        var sp = services.BuildServiceProvider();
        var svc = new BrowserConnectionHostedService(sp, new NullLogger<BrowserConnectionHostedService>(), pipeline.Object, options);

        // Act: 直接调用受保护的 ExecuteAsync（通过反射）
        var mi = typeof(BrowserConnectionHostedService).GetMethod("ExecuteAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.That(mi, Is.Not.Null);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));
        var task = (Task)mi!.Invoke(svc, new object?[] { cts.Token })!;
        await task;

        // Assert
        pipeline.Verify(p => p.ApplyAsync(ctx.Object, It.IsAny<CancellationToken>()), Times.Once);
        account.Verify(a => a.ConnectToBrowserAsync(), Times.Once);
        mgr.Verify(m => m.EnsureSessionFreshAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce());
    }

    [Test]
    public async Task Execute_Should_Skip_Reconnect_When_AlreadyLoggedIn()
    {
        var services = new ServiceCollection();

        var account = new Mock<IAccountManager>(MockBehavior.Strict);
        account.Setup(a => a.IsLoggedInAsync()).ReturnsAsync(true);
        services.AddSingleton(account.Object);

        var browserMgr = new Mock<IBrowserManager>(MockBehavior.Strict);
        browserMgr.Setup(m => m.EnsureSessionFreshAsync(It.IsAny<CancellationToken>()))
                   .Returns(Task.CompletedTask)
                   .Verifiable();
        services.AddSingleton(browserMgr.Object);

        var pipeline = new Mock<IPlaywrightAntiDetectionPipeline>(MockBehavior.Strict);
        services.AddSingleton(pipeline.Object);

        services.AddSingleton<IPageLoadWaitService>(Mock.Of<IPageLoadWaitService>());
        services.AddSingleton<IDomElementManager>(Mock.Of<IDomElementManager>());
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        var serviceSettings = new ServiceXhsSettings();
        serviceSettings.BrowserSettings.Connection.InitialDelaySeconds = 0;
        serviceSettings.BrowserSettings.Connection.RetryIntervalSeconds = 1;
        serviceSettings.BrowserSettings.Connection.RetryIntervalMaxSeconds = 1;
        serviceSettings.BrowserSettings.Connection.HealthCheckIntervalSeconds = 1;
        serviceSettings.BrowserSettings.Connection.CookieRenewalThresholdMinutes = 0;
        serviceSettings.BrowserSettings.Connection.ContextRecycleMinutes = 0;
        var options = Options.Create(serviceSettings);
        services.AddSingleton<IOptions<ServiceXhsSettings>>(_ => options);

        var sp = services.BuildServiceProvider();
        var svc = new BrowserConnectionHostedService(sp, new NullLogger<BrowserConnectionHostedService>(), pipeline.Object, options);

        var mi = typeof(BrowserConnectionHostedService).GetMethod("ExecuteAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.That(mi, Is.Not.Null);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(120));
        var task = (Task)mi!.Invoke(svc, new object?[] { cts.Token })!;
        await task;

        pipeline.Verify(p => p.ApplyAsync(It.IsAny<IBrowserContext>(), It.IsAny<CancellationToken>()), Times.Never);
        account.Verify(a => a.ConnectToBrowserAsync(), Times.Never);
        browserMgr.Verify(m => m.EnsureSessionFreshAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce());
    }
}
