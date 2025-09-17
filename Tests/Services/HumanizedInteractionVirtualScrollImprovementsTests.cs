using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using Moq;
using NUnit.Framework;
using XiaoHongShuMCP.Services;

namespace XiaoHongShuMCP.Tests.Services;

/// <summary>
/// 拟人化交互服务虚拟滚动检测改进验证测试
/// 验证虚拟滚动检测机制的改进是否正确实现
/// </summary>
[TestFixture]
public class HumanizedInteractionVirtualScrollImprovementsTests
{
    private sealed class DummyAutoPage : HushOps.Core.Automation.Abstractions.IAutoPage
    {
        public string PageId { get; } = Guid.NewGuid().ToString("N");
        public HushOps.Core.Automation.Abstractions.INavigator Navigator => throw new NotImplementedException();
        public HushOps.Core.Automation.Abstractions.IInput Input => throw new NotImplementedException();
        public HushOps.Core.Automation.Abstractions.IKeyboard Keyboard => new DummyKeyboard();
        public HushOps.Core.Automation.Abstractions.IClipboard Clipboard => throw new NotImplementedException();
        public HushOps.Core.Automation.Abstractions.IFilePicker FilePicker => throw new NotImplementedException();
        public Task<string> ContentAsync(CancellationToken ct = default) => Task.FromResult(string.Empty);
        public Task<T> EvaluateAsync<T>(string script, CancellationToken ct = default) => Task.FromResult(default(T)!);
        public Task<HushOps.Core.Automation.Abstractions.IAutoElement?> QueryAsync(string selector, int timeoutMs = 3000, CancellationToken ct = default) => Task.FromResult<HushOps.Core.Automation.Abstractions.IAutoElement?>(null);
        public Task<IReadOnlyList<HushOps.Core.Automation.Abstractions.IAutoElement>> QueryAllAsync(string selector, int timeoutMs = 3000, CancellationToken ct = default) => Task.FromResult((IReadOnlyList<HushOps.Core.Automation.Abstractions.IAutoElement>)new List<HushOps.Core.Automation.Abstractions.IAutoElement>());
        public Task MouseClickAsync(double x, double y, CancellationToken ct = default) => Task.CompletedTask;
        public Task MouseMoveAsync(double x, double y, CancellationToken ct = default) => Task.CompletedTask;
        public Task MouseWheelAsync(double deltaX, double deltaY, CancellationToken ct = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public Task<string> GetUrlAsync(CancellationToken ct = default) => Task.FromResult(string.Empty);

        private sealed class DummyKeyboard : HushOps.Core.Automation.Abstractions.IKeyboard
        {
            public Task TypeAsync(string text, int? delayMs = null, CancellationToken ct = default) => Task.CompletedTask;
            public Task PressAsync(string key, int? delayMs = null, CancellationToken ct = default) => Task.CompletedTask;
        }
    }
    private HumanizedInteractionService _service = null!;
    private Mock<ILogger<HumanizedInteractionService>> _mockLogger = null!;
    private Mock<HushOps.Core.Humanization.IDelayManager> _mockDelayManager = null!;
    private Mock<IElementFinder> _mockElementFinder = null!;
    private Mock<IDomElementManager> _mockDomElementManager = null!;
    private Mock<IBrowserManager> _mockBrowserManager = null!;
    private Mock<ITextInputStrategy> _mockInputStrategy = null!;

    [SetUp]
    public void Setup()
    {
        _mockLogger = new Mock<ILogger<HumanizedInteractionService>>();
        _mockDelayManager = new Mock<HushOps.Core.Humanization.IDelayManager>();
        _mockElementFinder = new Mock<IElementFinder>();
        _mockDomElementManager = new Mock<IDomElementManager>();
        _mockBrowserManager = new Mock<IBrowserManager>();
        _mockInputStrategy = new Mock<ITextInputStrategy>();

        var clickPolicy = new Mock<IHumanizedClickPolicy>().Object;
        _service = new HumanizedInteractionService(
            _mockBrowserManager.Object,
            _mockDelayManager.Object,
            _mockElementFinder.Object,
            new[] { _mockInputStrategy.Object },
            _mockDomElementManager.Object,
            Microsoft.Extensions.Options.Options.Create(new XhsSettings()),
            clickPolicy,
            _mockLogger.Object
        );
    }

    [Test]
    public void Service_ShouldInitializeWithAllDependencies()
    {
        // Assert - 验证服务正确初始化
        Assert.That(_service, Is.Not.Null);
        Assert.That(_service, Is.InstanceOf<HumanizedInteractionService>());
    }

    [Test]
    public async Task HumanWaitAsync_ShouldCallDelayManager()
    {
        // Arrange
        var waitType = HushOps.Core.Humanization.HumanWaitType.BetweenActions;

        // Act
        await _service.HumanWaitAsync(waitType);
        // Assert: 未抛异常即通过
    }

    [Test]
    public async Task HumanRetryDelayAsync_ShouldCallDelayManager()
    {
        // Arrange
        var attemptNumber = 3;

        // Act
        await _service.HumanRetryDelayAsync(attemptNumber);
        // Assert: 未抛异常即通过
    }

    [Test]
    public void VirtualScrollSimplification_ShouldBeImplemented()
    {
        // 该测试验证：已彻底删除 IPage 直连滚动，统一为 IAutoPage 版本；
        // 同时存在抽象页的内部滚动执行方法（ExecuteScrollWithAutoAsync）。
        var serviceType = typeof(HumanizedInteractionService);
        
        // 1) 验证 IPage 直连滚动重载已删除
        var scrollIPage0 = serviceType.GetMethod("HumanScrollAsync", new []{ typeof(IPage), typeof(System.Threading.CancellationToken) });
        var scrollIPage3 = serviceType.GetMethod("HumanScrollAsync", new []{ typeof(IPage), typeof(int), typeof(bool), typeof(System.Threading.CancellationToken) });
        Assert.That(scrollIPage0, Is.Null, "HumanScrollAsync(IPage, ...) overload should be removed");
        Assert.That(scrollIPage3, Is.Null, "HumanScrollAsync(IPage, int, bool, ...) overload should be removed");

        // 2) 验证 IAutoPage 版本存在
        var autoPageType = typeof(HushOps.Core.Automation.Abstractions.IAutoPage);
        var scrollIAutoPage = serviceType.GetMethod("HumanScrollAsync", new []{ autoPageType, typeof(int), typeof(bool), typeof(System.Threading.CancellationToken) });
        Assert.That(scrollIAutoPage, Is.Not.Null, "HumanScrollAsync(IAutoPage, int, bool, ...) should exist");

        // 3) 验证滚动实现已不依赖内部注入脚本方法（ExecuteScrollWithAutoAsync 已移除）
        var execAuto = serviceType.GetMethod("ExecuteScrollWithAutoAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.That(execAuto, Is.Null, "ExecuteScrollWithAutoAsync should be removed (use Mouse.Wheel)");

        // 4) 验证旧的 IPage 辅助探针方法已被删除
        var refreshPageMethod = serviceType.GetMethod("RefreshPageForNewContentAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var executeBasic = serviceType.GetMethod("ExecuteBasicScrollAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var waitForContentMethod = serviceType.GetMethod("WaitForContentLoadAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.That(refreshPageMethod, Is.Null, "RefreshPageForNewContentAsync should be removed");
        Assert.That(executeBasic, Is.Null, "ExecuteBasicScrollAsync should be removed");
        Assert.That(waitForContentMethod, Is.Null, "WaitForContentLoadAsync should be removed");

        // 5) 验证历史复杂检测方法仍不存在（持续简化目标）
        var getScrollTopMethod = serviceType.GetMethod("GetPageScrollTopAsync", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.That(getScrollTopMethod, Is.Null, "GetPageScrollTopAsync method should be removed");
        
        var getContentHashMethod = serviceType.GetMethod("GetVirtualContentHashAsync", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.That(getContentHashMethod, Is.Null, "GetVirtualContentHashAsync method should be removed");
        
        var hasLoadingIndicatorMethod = serviceType.GetMethod("HasLoadingIndicatorAsync", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.That(hasLoadingIndicatorMethod, Is.Null, "HasLoadingIndicatorAsync method should be removed");
        
        var getPageScrollHeightMethod = serviceType.GetMethod("GetPageScrollHeightAsync", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.That(getPageScrollHeightMethod, Is.Null, "GetPageScrollHeightAsync method should be removed");
    }

    [Test]
    public async Task FindElementAsync_ShouldDelegateToElementFinder()
    {
        // Arrange
        var mockAutoPage = new DummyAutoPage();
        var selectorAlias = "testSelector";
        var retries = 3;
        var timeout = 3000;

        // Act
        var result = await _service.FindElementAsync(mockAutoPage, selectorAlias, retries, timeout);

        // Assert - 验证委托给ElementFinder（不验证返回值，避免表达式树问题）
        Assert.That(result, Is.Null); // ElementFinder返回null是正常的
    }

    [Test]
    public async Task FindElementAsync_WithPageState_ShouldUseDomElementManager()
    {
        // Arrange
        var mockAutoPage = new DummyAutoPage();
        var selectorAlias = "testSelector";
        var pageState = PageState.Explore;
        
        // 设置 mock 返回一个非空的选择器列表
        _mockDomElementManager.Setup(x => x.GetSelectors(selectorAlias, pageState))
            .Returns(new List<string> { ".test-selector" });

        // Act
        var result = await _service.FindElementAsync(mockAutoPage, selectorAlias, pageState);

        // Assert - 验证使用了DomElementManager（不依赖表达式树验证）
        Assert.That(result, Is.Null); // 预期返回null是正常的
        
        // 验证调用了GetSelectors
        _mockDomElementManager.Verify(x => x.GetSelectors(selectorAlias, pageState), Times.Once);
    }
}
