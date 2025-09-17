using System.Threading.Tasks;
using Microsoft.Playwright;
using Moq;
using NUnit.Framework;
using HushOps.Core.Runtime.Playwright;
using HushOps.Core.Automation.Abstractions;

namespace Tests.Core.Automation;

/// <summary>
/// Playwright 适配器单元测试（中文注释）：
/// - 采用 Moq 模拟 IPage/ILocator，验证点击与输入路径；
/// - 验证导航异常映射为领域错误类型（NavigationError）。
/// </summary>
public class PlaywrightAdapterTests
{
    [Test]
    public async Task Input_Click_calls_Locator_Click_once()
    {
        var page = new Mock<IPage>(MockBehavior.Strict);
        var locator = new Mock<ILocator>(MockBehavior.Strict);
        locator.Setup(x => x.First).Returns(locator.Object);
        locator.Setup(x => x.HoverAsync(It.IsAny<LocatorHoverOptions?>())).Returns(Task.CompletedTask);
        locator.Setup(x => x.WaitForAsync(It.IsAny<LocatorWaitForOptions>())).Returns(Task.CompletedTask);
        locator.Setup(x => x.ClickAsync(It.IsAny<LocatorClickOptions>())).Returns(Task.CompletedTask).Verifiable();

        page.Setup(p => p.Locator("#btn", It.IsAny<PageLocatorOptions?>())).Returns(locator.Object);

        var input = new PrivateAccessor(page.Object).CreateInput();
        await input.ClickAsync("#btn");

        locator.Verify(x => x.ClickAsync(It.IsAny<LocatorClickOptions>()), Times.Once);
    }

    [Test]
    public void Navigator_Goto_wraps_PlaywrightException_as_NavigationError()
    {
        var page = new Mock<IPage>(MockBehavior.Strict);
        page
            .Setup(p => p.GotoAsync("https://example.com", It.IsAny<Microsoft.Playwright.PageGotoOptions>()))
            .ThrowsAsync(new PlaywrightException("fail"));

        var nav = new PrivateAccessor(page.Object).CreateNavigator();
        Assert.ThrowsAsync<NavigationError>(async () => await nav.GoToAsync("https://example.com"));
    }

    private sealed class PrivateAccessor
    {
        private readonly IPage page;
        public PrivateAccessor(IPage page) => this.page = page;
        public IInput CreateInput() => (IInput)typeof(PlaywrightBrowserDriver)
            .Assembly
            .CreateInstance("HushOps.Core.Runtime.Playwright.PlaywrightInput",
                ignoreCase: false,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public,
                binder: null,
                args: new object[] { page },
                culture: null,
                activationAttributes: null)!;

        public INavigator CreateNavigator() => (INavigator)typeof(PlaywrightBrowserDriver)
            .Assembly
            .CreateInstance("HushOps.Core.Runtime.Playwright.PlaywrightNavigator",
                ignoreCase: false,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public,
                binder: null,
                args: new object[] { page },
                culture: null,
                activationAttributes: null)!;
    }
}
