using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using XiaoHongShuMCP.Services;
using HushOps.Core.Automation.Abstractions;

namespace Tests.Services.HumanizedInteraction;

/// <summary>
/// 验证 HumanizedClickPolicy 在坐标点击路径下，会调用 MouseMove 多步并最终 MouseClick。
/// </summary>
public class HumanizedClickPolicyTrajectoryTests
{
    private sealed class FakeElement : IAutoElement
    {
        public Task ClickAsync(System.Threading.CancellationToken ct = default) => Task.FromException(new Exception("regular click fail for test"));
        public Task TypeAsync(string text, System.Threading.CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> IsVisibleAsync(System.Threading.CancellationToken ct = default) => Task.FromResult(true);
        public Task HoverAsync(System.Threading.CancellationToken ct = default) => Task.CompletedTask;
        public Task ScrollIntoViewIfNeededAsync(System.Threading.CancellationToken ct = default) => Task.CompletedTask;
        public Task<T?> EvaluateAsync<T>(string script, System.Threading.CancellationToken ct = default) => Task.FromResult(default(T));
        public Task<BoundingBox?> GetBoundingBoxAsync(System.Threading.CancellationToken ct = default) => Task.FromResult<BoundingBox?>(new BoundingBox{X=300,Y=200,Width=40,Height=20});
        public Task<(double x, double y)?> GetCenterAsync(System.Threading.CancellationToken ct = default) => Task.FromResult<(double, double)?>( (320,210) );
        public Task<string?> GetAttributeAsync(string name, System.Threading.CancellationToken ct = default) => Task.FromResult<string?>(null);
        public Task<string> InnerTextAsync(System.Threading.CancellationToken ct = default) => Task.FromResult(string.Empty);
        public Task<string> GetTagNameAsync(System.Threading.CancellationToken ct = default) => Task.FromResult("div");
        public Task<IAutoElement?> QuerySelectorAsync(string selector, int timeoutMs = 1000, System.Threading.CancellationToken ct = default) => Task.FromResult<IAutoElement?>(null);
        public Task<ElementVisibilityProbe> ProbeVisibilityAsync(System.Threading.CancellationToken ct = default) => Task.FromResult(new ElementVisibilityProbe());
        public Task<ElementComputedStyleProbe> GetComputedStyleProbeAsync(System.Threading.CancellationToken ct = default) => Task.FromResult(new ElementComputedStyleProbe());
        public Task<ElementTextProbe> TextProbeAsync(System.Threading.CancellationToken ct = default) => Task.FromResult(new ElementTextProbe());
        public Task<ElementClickabilityProbe> ProbeClickabilityAsync(System.Threading.CancellationToken ct = default) => Task.FromResult(new ElementClickabilityProbe{Clickable=true,HasBox=true,InViewport=true,VisibleByStyle=true,PointerEventsEnabled=true,CenterOccluded=false});
        public Task<ElementClickabilityProbe> GetClickabilityProbeAsync(System.Threading.CancellationToken ct = default) => Task.FromResult(new ElementClickabilityProbe{Clickable=true,HasBox=true,InViewport=true,VisibleByStyle=true,PointerEventsEnabled=true,CenterOccluded=false});
    }

    [Test]
    public async Task CoordinatePath_Should_Use_MouseMove_Before_Click()
    {
        var page = new Mock<IAutoPage>(MockBehavior.Strict);
        // 任意次 MouseMove（至少一次）
        page.Setup(p => p.MouseMoveAsync(It.IsAny<double>(), It.IsAny<double>(), default)).Returns(Task.CompletedTask);
        // 最终 Click
        page.Setup(p => p.MouseClickAsync(It.IsAny<double>(), It.IsAny<double>(), default)).Returns(Task.CompletedTask);

        var delay = new Mock<HushOps.Core.Humanization.IDelayManager>();
        delay.Setup(d => d.WaitAsync(It.IsAny<HushOps.Core.Humanization.HumanWaitType>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
             .Returns(Task.CompletedTask);
        var detector = new Mock<HushOps.Core.Humanization.IClickabilityDetector>();
        detector.Setup(d => d.AssessAsync(It.IsAny<IAutoElement>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new HushOps.Core.Humanization.ClickabilityReport{ IsClickable = true, HasBox = true, IsInViewport = true, IsVisible = true, PointerEventsEnabled = true });
        var preflight = new Mock<HushOps.Core.Humanization.IDomPreflightInspector>();
        preflight.Setup(p => p.InspectAsync(It.IsAny<IAutoElement>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new HushOps.Core.Humanization.DomPreflightReport{ IsReady = true, Reason = "ok" });

        var policy = new HumanizedClickPolicy(delay.Object, detector.Object, preflight.Object,
            Microsoft.Extensions.Options.Options.Create(new XiaoHongShuMCP.Services.XhsSettings
            {
                InteractionPolicy = new XiaoHongShuMCP.Services.XhsSettings.InteractionPolicySection
                {
                    EnableJsInjectionFallback = false
                }
            }));

        var decision = await policy.ClickAsync(page.Object, new FakeElement());
        var ok = decision.Success;
        Assert.That(decision.Path, Is.EqualTo("regular").Or.EqualTo("dispatch").Or.EqualTo("coordinate"));
        Assert.That(ok, Is.True);
        Assert.That(decision.Path, Is.EqualTo("coordinate"));
    }
}
