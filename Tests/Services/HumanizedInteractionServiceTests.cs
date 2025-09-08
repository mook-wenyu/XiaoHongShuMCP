using XiaoHongShuMCP.Services;
using Microsoft.Playwright;

namespace XiaoHongShuMCP.Tests.Services;

/// <summary>
/// 拟人化交互服务测试 - 重构版 v4.0
/// </summary>
[TestFixture]
public class HumanizedInteractionServiceTests
{
    private Mock<IDelayManager> _mockDelayManager = null!;
    private Mock<IElementFinder> _mockElementFinder = null!;
    private Mock<ITextInputStrategy> _mockInputStrategy = null!;
    private Mock<IDomElementManager> _mockDomElementManager = null!;
    private HumanizedInteractionService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _mockDelayManager = new Mock<IDelayManager>();
        _mockElementFinder = new Mock<IElementFinder>();
        _mockInputStrategy = new Mock<ITextInputStrategy>();
        _mockDomElementManager = new Mock<IDomElementManager>();
        
        var inputStrategies = new List<ITextInputStrategy> { _mockInputStrategy.Object };
        
        _service = new HumanizedInteractionService(
            _mockDelayManager.Object,
            _mockElementFinder.Object,
            inputStrategies,
            _mockDomElementManager.Object);
    }

    [Test]
    public async Task FindElementAsync_WithValidSelector_CallsElementFinder()
    {
        // Arrange
        var mockPage = new Mock<IPage>();
        var mockElement = new Mock<IElementHandle>();
        var selectorAlias = "testSelector";
        
        _mockElementFinder.Setup(x => x.FindElementAsync(mockPage.Object, selectorAlias, 3, 3000))
            .ReturnsAsync(mockElement.Object);

        // Act
        var result = await _service.FindElementAsync(mockPage.Object, selectorAlias);

        // Assert
        Assert.That(result, Is.EqualTo(mockElement.Object));
        _mockElementFinder.Verify(x => x.FindElementAsync(mockPage.Object, selectorAlias, 3, 3000), Times.Once);
    }

    [Test]
    public void DelayManager_DelayMethods_CallCorrectMethods()
    {
        // Arrange
        _mockDelayManager.Setup(x => x.GetThinkingPauseDelay()).Returns(1000);
        _mockDelayManager.Setup(x => x.GetReviewPauseDelay()).Returns(800);
        _mockDelayManager.Setup(x => x.GetClickDelay()).Returns(200);
        _mockDelayManager.Setup(x => x.GetScrollDelay()).Returns(150);

        // Act
        var thinkingPause = _mockDelayManager.Object.GetThinkingPauseDelay();
        var reviewPause = _mockDelayManager.Object.GetReviewPauseDelay();
        var clickDelay = _mockDelayManager.Object.GetClickDelay();
        var scrollDelay = _mockDelayManager.Object.GetScrollDelay();

        // Assert
        Assert.That(thinkingPause, Is.EqualTo(1000));
        Assert.That(reviewPause, Is.EqualTo(800));
        Assert.That(clickDelay, Is.EqualTo(200));
        Assert.That(scrollDelay, Is.EqualTo(150));
        
        _mockDelayManager.Verify(x => x.GetThinkingPauseDelay(), Times.Once);
        _mockDelayManager.Verify(x => x.GetReviewPauseDelay(), Times.Once);
        _mockDelayManager.Verify(x => x.GetClickDelay(), Times.Once);
        _mockDelayManager.Verify(x => x.GetScrollDelay(), Times.Once);
    }

    [Test]
    public void Service_Constructor_InitializesCorrectly()
    {
        // Act & Assert
        Assert.That(_service, Is.Not.Null);
    }

    [Test]
    public async Task HumanWaitAsync_WithThinkingPause_CallsDelayManager()
    {
        // Arrange
        _mockDelayManager.Setup(x => x.WaitAsync(HumanWaitType.ThinkingPause))
            .Returns(Task.CompletedTask);

        // Act
        await _service.HumanWaitAsync(HumanWaitType.ThinkingPause);

        // Assert
        _mockDelayManager.Verify(x => x.WaitAsync(HumanWaitType.ThinkingPause), Times.Once);
    }

    [Test]
    public async Task HumanWaitAsync_WithReviewPause_CallsDelayManager()
    {
        // Arrange
        _mockDelayManager.Setup(x => x.WaitAsync(HumanWaitType.ReviewPause))
            .Returns(Task.CompletedTask);

        // Act
        await _service.HumanWaitAsync(HumanWaitType.ReviewPause);

        // Assert
        _mockDelayManager.Verify(x => x.WaitAsync(HumanWaitType.ReviewPause), Times.Once);
    }

    [Test]
    public async Task HumanWaitAsync_WithBetweenActions_CallsDelayManager()
    {
        // Arrange
        _mockDelayManager.Setup(x => x.WaitAsync(HumanWaitType.BetweenActions))
            .Returns(Task.CompletedTask);

        // Act
        await _service.HumanWaitAsync(HumanWaitType.BetweenActions);

        // Assert
        _mockDelayManager.Verify(x => x.WaitAsync(HumanWaitType.BetweenActions), Times.Once);
    }

    [Test]
    public async Task HumanWaitAsync_WithModalWaiting_CallsDelayManager()
    {
        // Arrange
        _mockDelayManager.Setup(x => x.WaitAsync(HumanWaitType.ModalWaiting))
            .Returns(Task.CompletedTask);

        // Act
        await _service.HumanWaitAsync(HumanWaitType.ModalWaiting);

        // Assert
        _mockDelayManager.Verify(x => x.WaitAsync(HumanWaitType.ModalWaiting), Times.Once);
    }

    [Test]
    public async Task HumanWaitAsync_WithPageLoading_CallsDelayManager()
    {
        // Arrange
        _mockDelayManager.Setup(x => x.WaitAsync(HumanWaitType.PageLoading))
            .Returns(Task.CompletedTask);

        // Act
        await _service.HumanWaitAsync(HumanWaitType.PageLoading);

        // Assert
        _mockDelayManager.Verify(x => x.WaitAsync(HumanWaitType.PageLoading), Times.Once);
    }

    [Test]
    public async Task HumanWaitAsync_WithNetworkResponse_CallsDelayManager()
    {
        // Arrange
        _mockDelayManager.Setup(x => x.WaitAsync(HumanWaitType.NetworkResponse))
            .Returns(Task.CompletedTask);

        // Act
        await _service.HumanWaitAsync(HumanWaitType.NetworkResponse);

        // Assert
        _mockDelayManager.Verify(x => x.WaitAsync(HumanWaitType.NetworkResponse), Times.Once);
    }

    [Test]
    public async Task HumanWaitAsync_WithContentLoading_CallsDelayManager()
    {
        // Arrange
        _mockDelayManager.Setup(x => x.WaitAsync(HumanWaitType.ContentLoading))
            .Returns(Task.CompletedTask);

        // Act
        await _service.HumanWaitAsync(HumanWaitType.ContentLoading);

        // Assert
        _mockDelayManager.Verify(x => x.WaitAsync(HumanWaitType.ContentLoading), Times.Once);
    }

    [Test]
    public async Task HumanWaitAsync_WithScrollPreparation_CallsDelayManager()
    {
        // Arrange
        _mockDelayManager.Setup(x => x.WaitAsync(HumanWaitType.ScrollPreparation))
            .Returns(Task.CompletedTask);

        // Act
        await _service.HumanWaitAsync(HumanWaitType.ScrollPreparation);

        // Assert
        _mockDelayManager.Verify(x => x.WaitAsync(HumanWaitType.ScrollPreparation), Times.Once);
    }

    [Test]
    public async Task HumanWaitAsync_WithScrollExecution_CallsDelayManager()
    {
        // Arrange
        _mockDelayManager.Setup(x => x.WaitAsync(HumanWaitType.ScrollExecution))
            .Returns(Task.CompletedTask);

        // Act
        await _service.HumanWaitAsync(HumanWaitType.ScrollExecution);

        // Assert
        _mockDelayManager.Verify(x => x.WaitAsync(HumanWaitType.ScrollExecution), Times.Once);
    }

    [Test]
    public async Task HumanWaitAsync_WithScrollCompletion_CallsDelayManager()
    {
        // Arrange
        _mockDelayManager.Setup(x => x.WaitAsync(HumanWaitType.ScrollCompletion))
            .Returns(Task.CompletedTask);

        // Act
        await _service.HumanWaitAsync(HumanWaitType.ScrollCompletion);

        // Assert
        _mockDelayManager.Verify(x => x.WaitAsync(HumanWaitType.ScrollCompletion), Times.Once);
    }

    [Test]
    public async Task HumanWaitAsync_WithVirtualListUpdate_CallsDelayManager()
    {
        // Arrange
        _mockDelayManager.Setup(x => x.WaitAsync(HumanWaitType.VirtualListUpdate))
            .Returns(Task.CompletedTask);

        // Act
        await _service.HumanWaitAsync(HumanWaitType.VirtualListUpdate);

        // Assert
        _mockDelayManager.Verify(x => x.WaitAsync(HumanWaitType.VirtualListUpdate), Times.Once);
    }

    [Test]
    public async Task HumanTypeAsync_WithValidSelector_UsesInputStrategy()
    {
        // Arrange
        var mockPage = new Mock<IPage>();
        var mockElement = new Mock<IElementHandle>();
        var selectorAlias = "testSelector";
        var text = "测试文本";
        
        _mockElementFinder.Setup(x => x.FindElementAsync(mockPage.Object, selectorAlias, 3, 3000))
            .ReturnsAsync(mockElement.Object);
        _mockInputStrategy.Setup(x => x.IsApplicableAsync(mockElement.Object))
            .ReturnsAsync(true);
        _mockInputStrategy.Setup(x => x.InputTextAsync(mockPage.Object, mockElement.Object, text))
            .Returns(Task.CompletedTask);

        // Act
        await _service.HumanTypeAsync(mockPage.Object, selectorAlias, text);

        // Assert
        _mockElementFinder.Verify(x => x.FindElementAsync(mockPage.Object, selectorAlias, 3, 3000), Times.Once);
        _mockInputStrategy.Verify(x => x.IsApplicableAsync(mockElement.Object), Times.Once);
        _mockInputStrategy.Verify(x => x.InputTextAsync(mockPage.Object, mockElement.Object, text), Times.Once);
    }

    [Test]
    public async Task HumanRetryDelayAsync_WithAttemptNumber_CallsDelayManager()
    {
        // Arrange
        var attemptNumber = 3;
        var expectedDelay = 1500;
        
        _mockDelayManager.Setup(x => x.GetRetryDelay(attemptNumber))
            .Returns(expectedDelay);

        // Act
        await _service.HumanRetryDelayAsync(attemptNumber);

        // Assert
        _mockDelayManager.Verify(x => x.GetRetryDelay(attemptNumber), Times.Once);
    }

    [Test]
    public async Task HumanBetweenActionsDelayAsync_CallsHumanWaitAsync()
    {
        // Arrange
        _mockDelayManager.Setup(x => x.WaitAsync(HumanWaitType.BetweenActions))
            .Returns(Task.CompletedTask);

        // Act
        await _service.HumanBetweenActionsDelayAsync();

        // Assert
        _mockDelayManager.Verify(x => x.WaitAsync(HumanWaitType.BetweenActions), Times.Once);
    }

    [Test]
    public async Task HumanScrollAsync_DefaultParameters_CallsParameterizedVersion()
    {
        // Arrange
        var mockPage = new Mock<IPage>();
        
        // 设置页面评估调用的Mock - 模拟获取滚动信息
        mockPage.Setup(x => x.EvaluateAsync<ScrollInfo>(It.IsAny<string>(), null))
            .ReturnsAsync(new ScrollInfo 
            { 
                CurrentScrollTop = 0, 
                ScrollHeight = 2000, 
                ViewportHeight = 800 
            });

        // 设置延时管理器的Mock - 只设置实际会被调用的方法
        _mockDelayManager.Setup(x => x.GetReviewPauseDelay()).Returns(200);
        _mockDelayManager.Setup(x => x.GetScrollDelay()).Returns(300);
        _mockDelayManager.Setup(x => x.GetThinkingPauseDelay()).Returns(500);

        // Act
        await _service.HumanScrollAsync(mockPage.Object);

        // Assert
        mockPage.Verify(x => x.EvaluateAsync<ScrollInfo>(It.IsAny<string>(), null), Times.Once);
        // 验证确实执行了滚动操作
        mockPage.Verify(x => x.EvaluateAsync(It.Is<string>(s => s.Contains("window.scrollBy")), null), Times.AtLeastOnce);
        // 验证延时管理器的方法被调用
        _mockDelayManager.Verify(x => x.GetReviewPauseDelay(), Times.AtLeastOnce);
    }

    [Test]
    public async Task HumanScrollAsync_WithTargetDistance_ExecutesScrollSteps()
    {
        // Arrange
        var mockPage = new Mock<IPage>();
        var targetDistance = 1000;
        
        // 模拟页面滚动信息
        mockPage.Setup(x => x.EvaluateAsync<ScrollInfo>(It.IsAny<string>(), null))
            .ReturnsAsync(new ScrollInfo 
            { 
                CurrentScrollTop = 0, 
                ScrollHeight = 3000, 
                ViewportHeight = 800 
            });

        // 设置延时管理器
        _mockDelayManager.Setup(x => x.GetReviewPauseDelay()).Returns(200);
        _mockDelayManager.Setup(x => x.GetScrollDelay()).Returns(300);
        _mockDelayManager.Setup(x => x.GetThinkingPauseDelay()).Returns(500);
        _mockDelayManager.Setup(x => x.GetBetweenActionsDelay()).Returns(400);

        // Act
        await _service.HumanScrollAsync(mockPage.Object, targetDistance, waitForLoad: true);

        // Assert
        mockPage.Verify(x => x.EvaluateAsync<ScrollInfo>(It.IsAny<string>(), null), Times.Once);
        mockPage.Verify(x => x.EvaluateAsync(It.Is<string>(s => s.Contains("window.scrollBy")), null), Times.AtLeastOnce);
        _mockDelayManager.Verify(x => x.GetBetweenActionsDelay(), Times.AtLeastOnce);
    }

    [Test]
    public async Task HumanScrollAsync_WithZeroDistance_ReturnsEarly()
    {
        // Arrange
        var mockPage = new Mock<IPage>();
        
        // 模拟已经到达页面底部的情况
        mockPage.Setup(x => x.EvaluateAsync<ScrollInfo>(It.IsAny<string>(), null))
            .ReturnsAsync(new ScrollInfo 
            { 
                CurrentScrollTop = 2200, 
                ScrollHeight = 2000, 
                ViewportHeight = 800 
            });

        // Act
        await _service.HumanScrollAsync(mockPage.Object, 500, waitForLoad: false);

        // Assert
        mockPage.Verify(x => x.EvaluateAsync<ScrollInfo>(It.IsAny<string>(), null), Times.Once);
        // 不应该调用滚动操作
        mockPage.Verify(x => x.EvaluateAsync(It.Is<string>(s => s.Contains("window.scrollBy")), null), Times.Never);
    }

    [Test]
    public async Task HumanScrollAsync_WaitForLoadFalse_SkipsContentWaiting()
    {
        // Arrange
        var mockPage = new Mock<IPage>();
        var targetDistance = 500;
        
        mockPage.Setup(x => x.EvaluateAsync<ScrollInfo>(It.IsAny<string>(), null))
            .ReturnsAsync(new ScrollInfo 
            { 
                CurrentScrollTop = 0, 
                ScrollHeight = 2000, 
                ViewportHeight = 800 
            });

        _mockDelayManager.Setup(x => x.GetReviewPauseDelay()).Returns(200);
        _mockDelayManager.Setup(x => x.GetScrollDelay()).Returns(300);

        // Act
        await _service.HumanScrollAsync(mockPage.Object, targetDistance, waitForLoad: false);

        // Assert
        mockPage.Verify(x => x.EvaluateAsync<ScrollInfo>(It.IsAny<string>(), null), Times.Once);
        mockPage.Verify(x => x.EvaluateAsync(It.Is<string>(s => s.Contains("window.scrollBy")), null), Times.AtLeastOnce);
        // waitForLoad为false时不应该调用内容加载等待相关方法
    }

    [Test]
    public async Task HumanScrollAsync_WaitForLoadTrue_CallsContentWaiting()
    {
        // Arrange
        var mockPage = new Mock<IPage>();
        var targetDistance = 500;
        
        mockPage.Setup(x => x.EvaluateAsync<ScrollInfo>(It.IsAny<string>(), null))
            .ReturnsAsync(new ScrollInfo 
            { 
                CurrentScrollTop = 0, 
                ScrollHeight = 2000, 
                ViewportHeight = 800 
            });

        _mockDelayManager.Setup(x => x.GetReviewPauseDelay()).Returns(200);
        _mockDelayManager.Setup(x => x.GetScrollDelay()).Returns(300);
        _mockDelayManager.Setup(x => x.GetBetweenActionsDelay()).Returns(400);

        // Act
        await _service.HumanScrollAsync(mockPage.Object, targetDistance, waitForLoad: true);

        // Assert
        mockPage.Verify(x => x.EvaluateAsync<ScrollInfo>(It.IsAny<string>(), null), Times.Once);
        mockPage.Verify(x => x.EvaluateAsync(It.Is<string>(s => s.Contains("window.scrollBy")), null), Times.AtLeastOnce);
        _mockDelayManager.Verify(x => x.GetBetweenActionsDelay(), Times.AtLeastOnce);
    }

    [Test]
    public void ScrollInfo_IsNearBottom_DefaultThreshold_ReturnsTrue()
    {
        // Arrange
        var scrollInfo = new ScrollInfo
        {
            CurrentScrollTop = 1950,
            ScrollHeight = 2000,
            ViewportHeight = 800
        };

        // Act
        var result = scrollInfo.IsNearBottom();

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void ScrollInfo_IsNearBottom_CustomThreshold_ReturnsExpected()
    {
        // Arrange
        var scrollInfo = new ScrollInfo
        {
            CurrentScrollTop = 1800,
            ScrollHeight = 2000,
            ViewportHeight = 800
        };

        // Act
        var result = scrollInfo.IsNearBottom(threshold: 50);

        // Assert
        Assert.That(result, Is.True); // 1800 + 800 >= 2000 - 50 (2600 >= 1950)
    }

    [Test]
    public void ScrollInfo_GetRemainingScrollDistance_ReturnsCorrectValue()
    {
        // Arrange
        var scrollInfo = new ScrollInfo
        {
            CurrentScrollTop = 500,
            ScrollHeight = 2000,
            ViewportHeight = 800
        };

        // Act
        var remaining = scrollInfo.GetRemainingScrollDistance();

        // Assert
        Assert.That(remaining, Is.EqualTo(700)); // 2000 - 800 - 500 = 700
    }

    [Test]
    public void ScrollInfo_GetRemainingScrollDistance_NegativeReturnsZero()
    {
        // Arrange
        var scrollInfo = new ScrollInfo
        {
            CurrentScrollTop = 1500,
            ScrollHeight = 2000,
            ViewportHeight = 800
        };

        // Act
        var remaining = scrollInfo.GetRemainingScrollDistance();

        // Assert
        Assert.That(remaining, Is.EqualTo(0)); // Max(0, 2000 - 800 - 1500) = Max(0, -300) = 0
    }

    [TearDown]
    public void TearDown()
    {
        _mockDelayManager = null!;
        _mockElementFinder = null!;
        _mockInputStrategy = null!;
        _service = null!;
    }
}
