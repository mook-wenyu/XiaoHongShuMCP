using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;
using HushOps.Core.Automation.Abstractions;
using HushOps.Core.Humanization;
using Microsoft.Extensions.Logging.Abstractions;
using XiaoHongShuMCP.Services;

namespace Tests.Services.HumanizedInteraction;

/// <summary>
/// HumanizedInteractionService 新增的抽象元素点击重载测试。
/// 验证：调用 HumanClickAsync(IAutoElement) 时，会使用 IBrowserManager.GetAutoPageAsync()
/// 获取抽象页面，并将该页面与传入元素交给 IHumanizedClickPolicy。
/// </summary>
public class HumanizedInteractionServiceTests
{
    private sealed class DummyAutoPage : IAutoPage
    {
        public string PageId { get; } = Guid.NewGuid().ToString("N");
        public INavigator Navigator => throw new NotImplementedException();
        public IInput Input => throw new NotImplementedException();
        public IKeyboard Keyboard => new DummyKeyboard();
        public IClipboard Clipboard => new DummyClipboard();
        public IFilePicker FilePicker => new DummyFilePicker();
        public Task<string> ContentAsync(CancellationToken ct = default) => Task.FromResult(string.Empty);
        public Task<T> EvaluateAsync<T>(string script, CancellationToken ct = default)
        {
            // 简化实现：若脚本包含 window.scrollBy 则返回 true，否则返回默认
            object? val = default(T);
            if (typeof(T) == typeof(bool) && script.Contains("scrollBy", StringComparison.OrdinalIgnoreCase))
            {
                val = true;
            }
            return Task.FromResult((T?)val!);
        }
        public Task<IAutoElement?> QueryAsync(string selector, int timeoutMs = 3000, CancellationToken ct = default) => Task.FromResult<IAutoElement?>(null);
        public Task<IReadOnlyList<IAutoElement>> QueryAllAsync(string selector, int timeoutMs = 3000, CancellationToken ct = default) => Task.FromResult((IReadOnlyList<IAutoElement>)new List<IAutoElement>());
        public Task MouseClickAsync(double x, double y, CancellationToken ct = default) => Task.CompletedTask;
        public Task MouseMoveAsync(double x, double y, CancellationToken ct = default) => Task.CompletedTask;
        public Task MouseWheelAsync(double deltaX, double deltaY, CancellationToken ct = default) => Task.CompletedTask;
        public Task<string> GetUrlAsync(CancellationToken ct = default) => Task.FromResult(string.Empty);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private sealed class DummyKeyboard : IKeyboard
        {
            public Task TypeAsync(string text, int? delayMs = null, CancellationToken ct = default) => Task.CompletedTask;
            public Task PressAsync(string key, int? delayMs = null, CancellationToken ct = default) => Task.CompletedTask;
        }
        private sealed class DummyClipboard : IClipboard
        {
            public Task WriteTextAsync(string text, CancellationToken ct = default) => Task.CompletedTask;
            public Task<string> ReadTextAsync(CancellationToken ct = default) => Task.FromResult(string.Empty);
        }
        private sealed class DummyFilePicker : IFilePicker
        {
            public Task SetFilesAsync(string selector, IEnumerable<string> filePaths, CancellationToken ct = default) => Task.CompletedTask;
            public Task SetFilesAsync(IAutoElement element, IEnumerable<string> filePaths, CancellationToken ct = default) => Task.CompletedTask;
        }
    }

    private sealed class DummyAutoElement : IAutoElement
    {
        public Task ClickAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task TypeAsync(string text, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> IsVisibleAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task HoverAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task ScrollIntoViewIfNeededAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task<T?> EvaluateAsync<T>(string script, CancellationToken ct = default) => Task.FromResult(default(T));
        public Task<BoundingBox?> GetBoundingBoxAsync(CancellationToken ct = default) => Task.FromResult<BoundingBox?>(new BoundingBox{X=0,Y=0,Width=10,Height=10});
        public Task<(double x, double y)?> GetCenterAsync(CancellationToken ct = default) => Task.FromResult<(double, double)?>( (5,5) );
        public Task<string?> GetAttributeAsync(string name, CancellationToken ct = default) => Task.FromResult<string?>(null);
        public Task<string> InnerTextAsync(CancellationToken ct = default) => Task.FromResult(string.Empty);
        public Task<string> GetTagNameAsync(CancellationToken ct = default) => Task.FromResult(string.Empty);
        public Task<IAutoElement?> QuerySelectorAsync(string selector, int timeoutMs = 1000, CancellationToken ct = default) => Task.FromResult<IAutoElement?>(null);
        public Task<ElementVisibilityProbe> ProbeVisibilityAsync(CancellationToken ct = default)
            => Task.FromResult(new ElementVisibilityProbe{ InViewport=true, VisibleByStyle=true, PointerEventsEnabled=true, CenterOccluded=false});

        public Task<ElementComputedStyleProbe> GetComputedStyleProbeAsync(CancellationToken ct = default)
            => Task.FromResult(new ElementComputedStyleProbe{ Display="block", Visibility="visible", PointerEvents="auto", Opacity=1.0, Position="static", OverflowX="visible", OverflowY="visible"});

        public Task<ElementTextProbe> TextProbeAsync(CancellationToken ct = default)
            => Task.FromResult(new ElementTextProbe());

        public Task<ElementClickabilityProbe> GetClickabilityProbeAsync(CancellationToken ct = default)
            => Task.FromResult(new ElementClickabilityProbe
            {
                HasBox = true,
                Width = 10,
                Height = 10,
                InViewport = true,
                VisibleByStyle = true,
                PointerEventsEnabled = true,
                CenterOccluded = false,
                Clickable = true
            });
    }

    [Test]
    public async Task HumanScroll_WithAutoPage_ShouldNotThrow()
    {
        var delayManager = new Mock<IDelayManager>(MockBehavior.Strict);
        delayManager.Setup(d => d.WaitAsync(It.IsAny<HumanWaitType>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask);

        var svc = new HumanizedInteractionService(
            new Mock<IBrowserManager>().Object,
            delayManager.Object,
            new Mock<IElementFinder>().Object,
            Array.Empty<ITextInputStrategy>(),
            new Mock<IDomElementManager>().Object,
            Options.Create(new XhsSettings()),
            new Mock<IHumanizedClickPolicy>().Object,
            NullLogger<HumanizedInteractionService>.Instance);

        var autoPage = new DummyAutoPage();
        await svc.HumanScrollAsync(autoPage, 500, true);
        Assert.Pass();
    }

    [Test]
    public async Task HumanClick_WithAutoElement_DelegatesToClickPolicy()
    {
        var browserManager = new Mock<IBrowserManager>(MockBehavior.Strict);
        var delayManager = new Mock<IDelayManager>(MockBehavior.Strict);
        var elementFinder = new Mock<IElementFinder>(MockBehavior.Strict);
        var domMgr = new Mock<IDomElementManager>(MockBehavior.Strict);
        var clickPolicy = new Mock<IHumanizedClickPolicy>(MockBehavior.Strict);

        var autoPage = new DummyAutoPage();
        var autoEl = new DummyAutoElement();

        browserManager.Setup(b => b.GetAutoPageAsync()).ReturnsAsync(autoPage);
        // 延时调用直接返回
        delayManager.Setup(d => d.WaitAsync(It.IsAny<HumanWaitType>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask);

        clickPolicy.Setup(p => p.ClickAsync(autoPage, autoEl, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new ClickDecision { Success = true, Path = "regular" });

        var svc = new HumanizedInteractionService(
            browserManager.Object,
            delayManager.Object,
            elementFinder.Object,
            Enumerable.Empty<ITextInputStrategy>(),
            domMgr.Object,
            Options.Create(new XhsSettings()),
            clickPolicy.Object,
            NullLogger<HumanizedInteractionService>.Instance);

        await svc.HumanClickAsync(autoEl);

        clickPolicy.Verify(p => p.ClickAsync(autoPage, autoEl, It.IsAny<CancellationToken>()), Times.Once);
    }
}
