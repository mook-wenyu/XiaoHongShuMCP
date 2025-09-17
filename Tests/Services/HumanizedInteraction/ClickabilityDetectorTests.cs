using Microsoft.Playwright;
using HushOps.Core.Humanization;

namespace Tests.Services.HumanizedInteraction;

public class ClickabilityDetectorTests
{
    [Test]
    public async Task NoBoundingBox_ShouldBeNotClickable()
    {
        var det = new ClickabilityDetector();
        var el = new FakeAutoElement_NoBox();
        // Evaluate 调用可能发生，但这里不设定，默认返回类型默认值

        var rep = await det.AssessAsync(el);
        Assert.That(rep.IsClickable, Is.False);
        Assert.That(rep.HasBox, Is.False);
        Assert.That(rep.Reason, Does.Contain("尺寸"));
    }
}

internal sealed class FakeAutoElement_NoBox : HushOps.Core.Automation.Abstractions.IAutoElement
{
    public Task ClickAsync(System.Threading.CancellationToken ct = default) => Task.CompletedTask;
    public Task TypeAsync(string text, System.Threading.CancellationToken ct = default) => Task.CompletedTask;
    public Task<bool> IsVisibleAsync(System.Threading.CancellationToken ct = default) => Task.FromResult(false);
    public Task HoverAsync(System.Threading.CancellationToken ct = default) => Task.CompletedTask;
    public Task ScrollIntoViewIfNeededAsync(System.Threading.CancellationToken ct = default) => Task.CompletedTask;
    public Task<T?> EvaluateAsync<T>(string script, System.Threading.CancellationToken ct = default) => Task.FromResult(default(T));
    public Task<HushOps.Core.Automation.Abstractions.BoundingBox?> GetBoundingBoxAsync(System.Threading.CancellationToken ct = default)
        => Task.FromResult<HushOps.Core.Automation.Abstractions.BoundingBox?>(null);
    public Task<(double x, double y)?> GetCenterAsync(System.Threading.CancellationToken ct = default)
        => Task.FromResult<(double, double)?>(null);
    public Task<string?> GetAttributeAsync(string name, System.Threading.CancellationToken ct = default) => Task.FromResult<string?>(null);
    public Task<string> InnerTextAsync(System.Threading.CancellationToken ct = default) => Task.FromResult(string.Empty);
    public Task<string> GetTagNameAsync(System.Threading.CancellationToken ct = default) => Task.FromResult(string.Empty);
    public Task<HushOps.Core.Automation.Abstractions.IAutoElement?> QuerySelectorAsync(string selector, int timeoutMs = 1000, System.Threading.CancellationToken ct = default)
        => Task.FromResult<HushOps.Core.Automation.Abstractions.IAutoElement?>(null);
    public Task<HushOps.Core.Automation.Abstractions.ElementVisibilityProbe> ProbeVisibilityAsync(System.Threading.CancellationToken ct = default)
        => Task.FromResult(new HushOps.Core.Automation.Abstractions.ElementVisibilityProbe{ InViewport=false, VisibleByStyle=false, PointerEventsEnabled=false, CenterOccluded=false});

    public Task<HushOps.Core.Automation.Abstractions.ElementComputedStyleProbe> GetComputedStyleProbeAsync(System.Threading.CancellationToken ct = default)
        => Task.FromResult(new HushOps.Core.Automation.Abstractions.ElementComputedStyleProbe{ Display="", Visibility="hidden", PointerEvents="none", Opacity=1.0, Position="static", OverflowX="", OverflowY="" });

    public Task<HushOps.Core.Automation.Abstractions.ElementTextProbe> TextProbeAsync(System.Threading.CancellationToken ct = default)
        => Task.FromResult(new HushOps.Core.Automation.Abstractions.ElementTextProbe());

    public Task<HushOps.Core.Automation.Abstractions.ElementClickabilityProbe> GetClickabilityProbeAsync(System.Threading.CancellationToken ct = default)
        => Task.FromResult(new HushOps.Core.Automation.Abstractions.ElementClickabilityProbe
        {
            HasBox = false,
            Width = 0,
            Height = 0,
            InViewport = false,
            VisibleByStyle = false,
            PointerEventsEnabled = false,
            CenterOccluded = false,
            Clickable = false
        });
}
