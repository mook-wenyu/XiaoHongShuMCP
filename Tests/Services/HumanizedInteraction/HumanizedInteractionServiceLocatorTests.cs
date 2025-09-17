using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Moq;
using NUnit.Framework;
using XiaoHongShuMCP.Services;
using HushOps.Core.Automation.Abstractions;
using Microsoft.Extensions.Options;

namespace Tests.Services.HumanizedInteraction;

/// <summary>
/// 验证 HumanizedInteractionService 在定位元素时仅依赖 Locator 与 DOM 选择器，未命中时直接返回空。
/// </summary>
public class HumanizedInteractionServiceLocatorTests
{
    private HumanizedInteractionService CreateService(
        Mock<IDomElementManager> dom,
        Mock<IBrowserManager>? browser = null)
    {
        browser ??= new Mock<IBrowserManager>();

        var delay = new Mock<HushOps.Core.Humanization.IDelayManager>();
        delay.Setup(d => d.WaitAsync(It.IsAny<HushOps.Core.Humanization.HumanWaitType>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
             .Returns(Task.CompletedTask);

        return new HumanizedInteractionService(
            browser.Object,
            delay.Object,
            new Mock<IElementFinder>().Object,
            Array.Empty<ITextInputStrategy>(),
            dom.Object,
            Options.Create(new XhsSettings()),
            new Mock<IHumanizedClickPolicy>().Object);
    }

    [Test]
    public async Task FindElementAsync_IPage_UsesLocatorBeforeSelectors()
    {
        var dom = new Mock<IDomElementManager>(MockBehavior.Strict);
        var locator = new Mock<ILocator>(MockBehavior.Strict);
        var handle = Mock.Of<IElementHandle>();
        locator.Setup(l => l.ElementHandleAsync(It.IsAny<LocatorElementHandleOptions>()))
               .ReturnsAsync(handle)
               .Verifiable();

        dom.Setup(d => d.CreateLocator(It.IsAny<IPage>(), "SearchInput", PageState.Auto))
           .Returns(locator.Object);
        dom.Setup(d => d.GetSelectors("SearchInput")).Returns(new System.Collections.Generic.List<string>());

        var svc = CreateService(dom);
        var page = new Mock<IPage>(MockBehavior.Strict);

        var result = await svc.FindElementAsync(page.Object, "SearchInput", retries: 2, timeout: 2000);

        Assert.That(result, Is.EqualTo(handle));
        locator.Verify(l => l.ElementHandleAsync(It.IsAny<LocatorElementHandleOptions>()), Times.AtLeastOnce);
        dom.Verify(d => d.GetSelectors("SearchInput"), Times.AtLeastOnce);
    }

    [Test]
    public async Task FindElementAsync_IPage_ReturnsNullWhenLocatorMissing()
    {
        var dom = new Mock<IDomElementManager>(MockBehavior.Strict);
        dom.Setup(d => d.CreateLocator(It.IsAny<IPage>(), "FallbackAlias", PageState.Auto)).Returns((ILocator?)null);
        dom.Setup(d => d.GetSelectors("FallbackAlias")).Returns(new System.Collections.Generic.List<string>());

        var svc = CreateService(dom);
        var page = new Mock<IPage>(MockBehavior.Strict);

        var result = await svc.FindElementAsync(page.Object, "FallbackAlias", retries: 1, timeout: 500);

        Assert.That(result, Is.Null, "未找到 Locator/选择器 时应返回 null。");
    }
}
