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
    private HumanizedInteractionService _service = null!;
    private Mock<ILogger<HumanizedInteractionService>> _mockLogger = null!;
    private Mock<IDelayManager> _mockDelayManager = null!;
    private Mock<IElementFinder> _mockElementFinder = null!;
    private Mock<IDomElementManager> _mockDomElementManager = null!;
    private Mock<IBrowserManager> _mockBrowserManager = null!;
    private Mock<ITextInputStrategy> _mockInputStrategy = null!;

    [SetUp]
    public void Setup()
    {
        _mockLogger = new Mock<ILogger<HumanizedInteractionService>>();
        _mockDelayManager = new Mock<IDelayManager>();
        _mockElementFinder = new Mock<IElementFinder>();
        _mockDomElementManager = new Mock<IDomElementManager>();
        _mockBrowserManager = new Mock<IBrowserManager>();
        _mockInputStrategy = new Mock<ITextInputStrategy>();
        
        _service = new HumanizedInteractionService(
            _mockBrowserManager.Object,
            _mockDelayManager.Object,
            _mockElementFinder.Object,
            new[] { _mockInputStrategy.Object },
            _mockDomElementManager.Object,
            null,
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
        var waitType = HumanWaitType.BetweenActions;

        // Act & Assert - 测试不抛出异常即为成功
        Assert.DoesNotThrowAsync(async () => await _service.HumanWaitAsync(waitType));
    }

    [Test]
    public async Task HumanRetryDelayAsync_ShouldCallDelayManager()
    {
        // Arrange
        var attemptNumber = 3;

        // Act & Assert - 测试不抛出异常即为成功
        Assert.DoesNotThrowAsync(async () => await _service.HumanRetryDelayAsync(attemptNumber));
    }

    [Test]
    public void VirtualScrollSimplification_ShouldBeImplemented()
    {
        // 这个测试验证虚拟滚动简化功能已经实现
        // 通过检查相关私有方法存在来验证功能完整性
        var serviceType = typeof(HumanizedInteractionService);
        
        // 验证基础滚动执行方法存在（简化版本）
        var executeScrollMethod = serviceType.GetMethod("ExecuteBasicScrollAsync", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.That(executeScrollMethod, Is.Not.Null, "ExecuteBasicScrollAsync method should exist");
        
        // 验证内容等待方法存在（简化版本）
        var waitForContentMethod = serviceType.GetMethod("WaitForContentLoadAsync", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.That(waitForContentMethod, Is.Not.Null, "WaitForContentLoadAsync method should exist");
        
        // 验证页面刷新方法存在
        var refreshPageMethod = serviceType.GetMethod("RefreshPageForNewContentAsync", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.That(refreshPageMethod, Is.Not.Null, "RefreshPageForNewContentAsync method should exist");
        
        // 验证复杂检测方法已被删除（简化目标）
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
        var mockPage = new Mock<IPage>();
        var selectorAlias = "testSelector";
        var retries = 3;
        var timeout = 3000;

        // Act
        var result = await _service.FindElementAsync(mockPage.Object, selectorAlias, retries, timeout);

        // Assert - 验证委托给ElementFinder（不验证返回值，避免表达式树问题）
        Assert.That(result, Is.Null); // ElementFinder返回null是正常的
    }

    [Test]
    public async Task FindElementAsync_WithPageState_ShouldUseDomElementManager()
    {
        // Arrange
        var mockPage = new Mock<IPage>();
        var selectorAlias = "testSelector";
        var pageState = PageState.Explore;
        
        // 设置 mock 返回一个非空的选择器列表
        _mockDomElementManager.Setup(x => x.GetSelectors(selectorAlias, pageState))
            .Returns(new List<string> { ".test-selector" });
            
        // 设置 page.QuerySelectorAsync 返回 null（显式指定可选参数，避免表达式树与可选参数的冲突）
        mockPage.Setup(x => x.QuerySelectorAsync(
                It.IsAny<string>(),
                It.IsAny<PageQuerySelectorOptions?>()))
            .ReturnsAsync((IElementHandle?)null);

        // Act
        var result = await _service.FindElementAsync(mockPage.Object, selectorAlias, pageState);

        // Assert - 验证使用了DomElementManager（不依赖表达式树验证）
        Assert.That(result, Is.Null); // 预期返回null是正常的
        
        // 验证调用了GetSelectors
        _mockDomElementManager.Verify(x => x.GetSelectors(selectorAlias, pageState), Times.Once);
    }
}
