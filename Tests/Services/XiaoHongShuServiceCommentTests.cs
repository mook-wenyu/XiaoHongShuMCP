using HushOps.Core.Automation.Abstractions;
using HushOps.Core.Humanization;
using Moq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using XiaoHongShuMCP.Services;

namespace XiaoHongShuMCP.Tests.Services;

/// <summary>
/// 针对评论交互强类型化改造的单元测试，使用轻量假实现避免真实 Playwright 依赖。
/// </summary>
[TestFixture]
public class XiaoHongShuServiceCommentTests
{
    [Test]
    public async Task InputCommentWithEnhancedFeaturesAsync_UsesStrongTypedInteraction()
    {
        var inputElement = new FakeAutoElement();
        var autoPage = new FakeAutoPage(new Dictionary<string, IAutoElement?>
        {
            [".comment-input"] = inputElement
        });

        var humanized = new Mock<IHumanizedInteractionService>();
        humanized.Setup(h => h.HumanClickAsync(inputElement))
            .Returns(Task.CompletedTask)
            .Verifiable();
        humanized.Setup(h => h.HumanWaitAsync(HumanWaitType.ThinkingPause, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        humanized.Setup(h => h.InputTextAsync(autoPage, inputElement, "测试内容"))
            .Returns(Task.CompletedTask)
            .Verifiable();

        var dom = new Mock<IDomElementManager>(MockBehavior.Strict);
        dom.Setup(d => d.GetSelectors("CommentInputReady"))
            .Returns(new List<string> { ".comment-input" });

        var service = CreateService(humanized.Object, dom.Object);

        await service.InputCommentWithEnhancedFeaturesAsync(autoPage, "测试内容");

        humanized.Verify();
        Assert.That(inputElement.HasTyped, Is.True);
    }

    [Test]
    public async Task GetCommentAreaStateAsync_ReturnsActiveReadyEnabled()
    {
        var inputElement = new FakeAutoElement(new Dictionary<string, string?>
        {
            ["contenteditable"] = "true"
        });

        var autoPage = new FakeAutoPage(new Dictionary<string, IAutoElement?>
        {
            [".active"] = new FakeAutoElement(),
            [".input"] = inputElement,
            [".submit"] = new FakeAutoElement()
        });

        var humanized = new Mock<IHumanizedInteractionService>();
        var dom = new Mock<IDomElementManager>(MockBehavior.Strict);
        dom.Setup(d => d.GetSelectors("EngageBarActive"))
            .Returns(new List<string> { ".active" });
        dom.Setup(d => d.GetSelectors("CommentInputReady"))
            .Returns(new List<string> { ".input" });
        dom.Setup(d => d.GetSelectors("CommentSubmitEnabled"))
            .Returns(new List<string> { ".submit" });

        var service = CreateService(humanized.Object, dom.Object);
        var (active, ready, submitEnabled) = await service.GetCommentAreaStateAsync(autoPage);

        Assert.Multiple(() =>
        {
            Assert.That(active, Is.True);
            Assert.That(ready, Is.True);
            Assert.That(submitEnabled, Is.True);
        });
    }

    private static XiaoHongShuService CreateService(
        IHumanizedInteractionService humanized,
        IDomElementManager dom,
        ICommentWorkflow? commentWorkflow = null,
        INoteEngagementWorkflow? noteEngagementWorkflow = null,
        INoteDiscoveryService? noteDiscovery = null)
    {
        var logger = Mock.Of<ILogger<XiaoHongShuService>>();
        var loggerFactory = Mock.Of<ILoggerFactory>();
        var browserManager = Mock.Of<IBrowserManager>();
        var accountManager = Mock.Of<IAccountManager>();
        var pageLoadWait = Mock.Of<IPageLoadWaitService>();
        var pageGuard = Mock.Of<IPageStateGuard>();
        var monitor = Mock.Of<IUniversalApiMonitor>();
        var options = Options.Create(new XhsSettings());
        commentWorkflow ??= Mock.Of<ICommentWorkflow>();
        noteEngagementWorkflow ??= Mock.Of<INoteEngagementWorkflow>();
        noteDiscovery ??= Mock.Of<INoteDiscoveryService>();

        return new XiaoHongShuService(
            logger,
            loggerFactory,
            browserManager,
            accountManager,
            humanized,
            dom,
            pageLoadWait,
            pageGuard,
            monitor,
            commentWorkflow,
            noteEngagementWorkflow,
            noteDiscovery,
            options);
    }

    private sealed class FakeAutoPage : IAutoPage
    {
        private readonly IDictionary<string, IAutoElement?> responses;

        public FakeAutoPage(IDictionary<string, IAutoElement?> responses)
        {
            this.responses = responses;
        }

        public string PageId => "fake";
        public INavigator Navigator => throw new NotSupportedException();
        public IInput Input => throw new NotSupportedException();
        public IKeyboard Keyboard => throw new NotSupportedException();
        public IClipboard Clipboard => throw new NotSupportedException();
        public IFilePicker FilePicker => throw new NotSupportedException();

        public Task<string> ContentAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<string> GetUrlAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<T> EvaluateAsync<T>(string script, CancellationToken ct = default) => throw new NotSupportedException();

        public Task<IAutoElement?> QueryAsync(string selector, int timeoutMs = 3000, CancellationToken ct = default)
        {
            responses.TryGetValue(selector, out var element);
            return Task.FromResult(element);
        }

        public Task<IReadOnlyList<IAutoElement>> QueryAllAsync(string selector, int timeoutMs = 3000, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<IAutoElement>>(Array.Empty<IAutoElement>());

        public Task MouseClickAsync(double x, double y, CancellationToken ct = default) => Task.CompletedTask;
        public Task MouseMoveAsync(double x, double y, CancellationToken ct = default) => Task.CompletedTask;
        public Task MouseWheelAsync(double deltaX, double deltaY, CancellationToken ct = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeAutoElement : IAutoElement
    {
        private readonly Dictionary<string, string?> attributes;

        public FakeAutoElement(Dictionary<string, string?>? attributes = null)
        {
            this.attributes = attributes ?? new Dictionary<string, string?>();
        }

        public bool HasTyped { get; private set; }

        public Task ClickAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task TypeAsync(string text, CancellationToken ct = default)
        {
            HasTyped = true;
            return Task.CompletedTask;
        }
        public Task<bool> IsVisibleAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task HoverAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task ScrollIntoViewIfNeededAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task<T?> EvaluateAsync<T>(string script, CancellationToken ct = default) => Task.FromResult(default(T?));
        public Task<BoundingBox?> GetBoundingBoxAsync(CancellationToken ct = default) => Task.FromResult<BoundingBox?>(null);
        public Task<(double x, double y)?> GetCenterAsync(CancellationToken ct = default) => Task.FromResult<(double, double)?>(null);
        public Task<string?> GetAttributeAsync(string name, CancellationToken ct = default)
        {
            attributes.TryGetValue(name, out var value);
            return Task.FromResult(value);
        }
        public Task<string> InnerTextAsync(CancellationToken ct = default) => Task.FromResult(string.Empty);
        public Task<string> GetTagNameAsync(CancellationToken ct = default) => Task.FromResult(string.Empty);
        public Task<IAutoElement?> QuerySelectorAsync(string selector, int timeoutMs = 1000, CancellationToken ct = default) => Task.FromResult<IAutoElement?>(null);
        public Task<ElementVisibilityProbe> ProbeVisibilityAsync(CancellationToken ct = default) => Task.FromResult(new ElementVisibilityProbe());
        public Task<ElementComputedStyleProbe> GetComputedStyleProbeAsync(CancellationToken ct = default) => Task.FromResult(new ElementComputedStyleProbe());
        public Task<ElementTextProbe> TextProbeAsync(CancellationToken ct = default) => Task.FromResult(new ElementTextProbe());
        public Task<ElementClickabilityProbe> GetClickabilityProbeAsync(CancellationToken ct = default) => Task.FromResult(new ElementClickabilityProbe());
    }
    [Test]
    public async Task PostCommentAsync_DelegatesToWorkflow()
    {
        var workflow = new Mock<ICommentWorkflow>();
        var expected = OperationResult<CommentResult>.Ok(new CommentResult(true, "ok", "123"));
        workflow.Setup(w => w.PostCommentAsync("kw", "content", CancellationToken.None))
            .ReturnsAsync(expected)
            .Verifiable();

        var service = CreateService(Mock.Of<IHumanizedInteractionService>(), Mock.Of<IDomElementManager>(), workflow.Object);
        var result = await service.PostCommentAsync("kw", "content");

        workflow.Verify(w => w.PostCommentAsync("kw", "content", CancellationToken.None), Times.Once);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data?.CommentId, Is.EqualTo("123"));
    }
}




