using System;
using System.Threading;
using System.Threading.Tasks;
using HushOps.Core.Automation.Abstractions;
using HushOps.Core.Humanization;
using HushOps.Core.Runtime.Playwright;
using HushOps.Core.Runtime.Playwright.AntiDetection;
using Microsoft.Playwright;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;
using XiaoHongShuMCP.Services;

namespace Tests.Services.HumanizedInteraction;

/// <summary>
/// HumanizedClickPolicy 注入兜底门控测试（中文注释）：
/// - 默认关闭时，不应执行注入兜底；
/// - 当 InteractionPolicy 与 AntiDetection 策略均允许时，通过 AntiDetectionPipeline 受控执行注入。
/// </summary>
public class HumanizedClickPolicyTests
{
    private static HumanizedClickPolicy CreatePolicy(
        bool enableJsInjectionFallback,
        IPlaywrightAntiDetectionPipeline? anti = null)
    {
        var delay = new Mock<IDelayManager>();
        delay.Setup(d => d.WaitAsync(It.IsAny<HumanWaitType>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var detector = new Mock<IClickabilityDetector>();
        detector.Setup(x => x.AssessAsync(It.IsAny<IAutoElement>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ClickabilityReport { IsClickable = true, Reason = "ok" });

        var preflight = new Mock<IDomPreflightInspector>();
        preflight.Setup(p => p.InspectAsync(It.IsAny<IAutoElement>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DomPreflightReport { IsReady = true, IsDisabled = false, Reason = "ready" });

        var settings = new XiaoHongShuMCP.Services.XhsSettings
        {
            InteractionPolicy = new XiaoHongShuMCP.Services.XhsSettings.InteractionPolicySection
            {
                EnableJsInjectionFallback = enableJsInjectionFallback,
                EnableJsReadEval = true
            }
        };

        return new HumanizedClickPolicy(
            delay.Object,
            detector.Object,
            preflight.Object,
            Options.Create(settings),
            metrics: null,
            logger: new Mock<ILogger<HumanizedClickPolicy>>().Object,
            pacing: null,
            anti: anti);
    }

    private static (IAutoPage page, IAutoElement element) CreatePageAndElement(out Mock<IElementHandle> handleMock)
    {
        var page = new Mock<IAutoPage>();
        // 坐标回退路径：通过返回 null 使其失败，从而让测试命中注入路径（若开启）
        var h = new Mock<IElementHandle>(MockBehavior.Strict);
        handleMock = h;
        var element = PlaywrightAutoFactory.Wrap(h.Object);
        return (page.Object, element);
    }

    [Test]
    public void ClickAsync_Should_Not_Inject_When_Disabled()
    {
        var anti = new Mock<IPlaywrightAntiDetectionPipeline>(MockBehavior.Strict);
        var policy = CreatePolicy(enableJsInjectionFallback: false, anti: anti.Object);
        var (page, element) = CreatePageAndElement(out var handle);
        // 设置底层 IElementHandle 行为以驱动 PlaywrightElementFromHandle 的方法
        handle.Setup(h => h.ScrollIntoViewIfNeededAsync(It.IsAny<ElementHandleScrollIntoViewIfNeededOptions>()))
              .Returns(Task.CompletedTask);
        handle.Setup(h => h.ClickAsync(It.IsAny<ElementHandleClickOptions>()))
              .ThrowsAsync(new Exception("click fail"));
        handle.Setup(h => h.BoundingBoxAsync()).ReturnsAsync((ElementHandleBoundingBoxResult?)null);
        handle.Setup(h => h.HoverAsync(It.IsAny<ElementHandleHoverOptions>())).Returns(Task.CompletedTask);

        Assert.ThrowsAsync<Exception>(async () => await policy.ClickAsync(page, element));
        anti.Verify(a => a.TryUiInjectionAsync(It.IsAny<IElementHandle>(), It.IsAny<string>(), It.IsAny<Func<IElementHandle, Task>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ClickAsync_Should_Inject_Via_Pipeline_When_Enabled()
    {
        // 设置环境变量使 AntiDetection 策略允许注入
        Environment.SetEnvironmentVariable("XHS__AntiDetection__EnableJsInjectionFallback", "true");

        var anti = new Mock<IPlaywrightAntiDetectionPipeline>(MockBehavior.Strict);
        anti.Setup(a => a.TryUiInjectionAsync(It.IsAny<IElementHandle>(), "click.dispatchEvent", It.IsAny<Func<IElementHandle, Task>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var policy = CreatePolicy(enableJsInjectionFallback: true, anti: anti.Object);
        var (page, element) = CreatePageAndElement(out var handle);
        handle.Setup(h => h.ScrollIntoViewIfNeededAsync(It.IsAny<ElementHandleScrollIntoViewIfNeededOptions>()))
              .Returns(Task.CompletedTask);
        handle.Setup(h => h.ClickAsync(It.IsAny<ElementHandleClickOptions>()))
              .ThrowsAsync(new Exception("click fail"));
        handle.Setup(h => h.BoundingBoxAsync()).ReturnsAsync((ElementHandleBoundingBoxResult?)null);
        handle.Setup(h => h.HoverAsync(It.IsAny<ElementHandleHoverOptions>())).Returns(Task.CompletedTask);

        var result = await policy.ClickAsync(page, element);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Path, Is.EqualTo("dispatch"));
        anti.Verify(a => a.TryUiInjectionAsync(It.IsAny<IElementHandle>(), "click.dispatchEvent", It.IsAny<Func<IElementHandle, Task>>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
