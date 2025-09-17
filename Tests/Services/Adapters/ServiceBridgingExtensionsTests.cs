using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HushOps.Core.Automation.Abstractions;
using Microsoft.Playwright;
using Moq;
using NUnit.Framework;
using XiaoHongShuMCP.Services;

namespace Tests.Services.Adapters;

/// <summary>
/// ServiceBridgingExtensions 适配层单元测试：验证 IPage/IElementHandle 扩展在不破坏业务代码的前提下正确桥接至 IAuto* 强类型接口。
/// </summary>
public class ServiceBridgingExtensionsTests
{
    [Test]
    public void UniversalApiMonitor_SetupMonitor_With_IPage_Should_Delegate_To_IAutoPage()
    {
        var monitor = new Mock<IUniversalApiMonitor>(MockBehavior.Strict);
        monitor
            .Setup(m => m.SetupMonitor(It.IsAny<IAutoPage>(), It.IsAny<HashSet<ApiEndpointType>>()))
            .Returns(true)
            .Verifiable();

        var page = new Mock<IPage>(MockBehavior.Strict);
        var endpoints = new HashSet<ApiEndpointType> { ApiEndpointType.Feed, ApiEndpointType.Comments };

        var ok = monitor.Object.SetupMonitor(page.Object, endpoints);

        Assert.That(ok, Is.True, "扩展重载应返回底层强类型实现的结果");
        monitor.Verify(m => m.SetupMonitor(It.IsAny<IAutoPage>(), endpoints), Times.Once);
    }

    [Test]
    public async Task PageStateGuard_EnsureOnDiscoverOrSearch_With_IPage_Should_Delegate_To_IAutoPage()
    {
        var guard = new Mock<IPageStateGuard>(MockBehavior.Strict);
        guard
            .Setup(g => g.EnsureOnDiscoverOrSearchAsync(It.IsAny<IAutoPage>()))
            .ReturnsAsync(true)
            .Verifiable();

        var page = new Mock<IPage>(MockBehavior.Strict);
        var ok = await guard.Object.EnsureOnDiscoverOrSearchAsync(page.Object);

        Assert.That(ok, Is.True, "扩展重载应返回底层强类型实现的结果");
        guard.Verify(g => g.EnsureOnDiscoverOrSearchAsync(It.IsAny<IAutoPage>()), Times.Once);
    }

    [Test]
    public async Task PageLoadWaitService_WaitForPageLoad_With_IPage_Should_Delegate_To_IAutoPage()
    {
        var wait = new Mock<IPageLoadWaitService>(MockBehavior.Strict);
        wait
            .Setup(w => w.WaitForPageLoadAsync(It.IsAny<IAutoPage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PageLoadWaitResult.CreateSuccess(PageLoadStrategy.Load, TimeSpan.FromMilliseconds(1)))
            .Verifiable();

        var page = new Mock<IPage>(MockBehavior.Strict);
        var result = await wait.Object.WaitForPageLoadAsync(page.Object);

        Assert.That(result.Success, Is.True, "扩展重载应返回底层强类型实现的成功结果");
        wait.Verify(w => w.WaitForPageLoadAsync(It.IsAny<IAutoPage>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
