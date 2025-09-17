using Microsoft.Playwright;
using NUnit.Framework;
using HushOps.Core.Humanization;

namespace Tests.Services.HumanizedInteraction;

/// <summary>
/// DomPreflightInspector 单元测试：验证禁用/忙碌/就绪三类路径。
/// </summary>
public class DomPreflightInspectorTests
{
    [Test]
    public async Task AriaDisabled_ShouldNotReady()
    {
        var fake = new FakeAutoElement(
            html: "<button aria-disabled=\"true\">点赞</button>",
            role: "button", disabled: true, busy: false
        );
        var insp = new DomPreflightInspector();
        var rep = await insp.InspectAsync(fake);

        Assert.That(rep.IsReady, Is.False);
        Assert.That(rep.IsDisabled, Is.True);
        Assert.That(rep.Reason, Does.Contain("禁用"));
    }

    [Test]
    public async Task BusySpinner_ShouldNotReady()
    {
        var fake = new FakeAutoElement(
            html: "<button class=\"loading\"><span class=spinner></span></button>",
            role: "button", disabled: false, busy: true
        );
        var insp = new DomPreflightInspector();
        var rep = await insp.InspectAsync(fake);

        Assert.That(rep.IsReady, Is.False);
        Assert.That(rep.IsBusy, Is.True);
        Assert.That(rep.Reason, Does.Contain("忙碌"));
    }

    [Test]
    public async Task NormalButton_ShouldReady()
    {
        var fake = new FakeAutoElement(
            html: "<button class=\"reds-button-new\">点赞</button>",
            role: "button", disabled: false, busy: false
        );
        var insp = new DomPreflightInspector();
        var rep = await insp.InspectAsync(fake);

        Assert.That(rep.IsReady, Is.True);
        Assert.That(rep.Reason, Does.Contain("就绪"));
    }
}

internal sealed class FakeAutoElement : HushOps.Core.Automation.Abstractions.IAutoElement
{
    private readonly string _html;
    private readonly string _role;
    private readonly bool _disabled;
    private readonly bool _busy;
    public FakeAutoElement(string html, string role, bool disabled, bool busy) { _html = html; _role = role; _disabled = disabled; _busy = busy; }
    public Task ClickAsync(System.Threading.CancellationToken ct = default) => Task.CompletedTask;
    public Task TypeAsync(string text, System.Threading.CancellationToken ct = default) => Task.CompletedTask;
    public Task<bool> IsVisibleAsync(System.Threading.CancellationToken ct = default) => Task.FromResult(true);
    public Task HoverAsync(System.Threading.CancellationToken ct = default) => Task.CompletedTask;
    public Task ScrollIntoViewIfNeededAsync(System.Threading.CancellationToken ct = default) => Task.CompletedTask;
    public Task<T?> EvaluateAsync<T>(string script, System.Threading.CancellationToken ct = default) => Task.FromResult(default(T));
    public Task<HushOps.Core.Automation.Abstractions.BoundingBox?> GetBoundingBoxAsync(System.Threading.CancellationToken ct = default)
        => Task.FromResult<HushOps.Core.Automation.Abstractions.BoundingBox?>(new HushOps.Core.Automation.Abstractions.BoundingBox { X=0, Y=0, Width=10, Height=10});
    public Task<(double x, double y)?> GetCenterAsync(System.Threading.CancellationToken ct = default) => Task.FromResult<(double, double)?>((5,5));
    public Task<string?> GetAttributeAsync(string name, System.Threading.CancellationToken ct = default)
        => Task.FromResult<string?>(name switch { "role" => _role, "aria-disabled" => _disabled?"true":null, "disabled" => _disabled?string.Empty:null, "aria-busy" => _busy?"true":null, "data-loading" => _busy?"true":null, _ => null });
    public Task<string> InnerTextAsync(System.Threading.CancellationToken ct = default) => Task.FromResult(string.Empty);
    public Task<string> GetTagNameAsync(System.Threading.CancellationToken ct = default) => Task.FromResult("button");
    public Task<HushOps.Core.Automation.Abstractions.IAutoElement?> QuerySelectorAsync(string selector, int timeoutMs = 1000, System.Threading.CancellationToken ct = default)
        => Task.FromResult<HushOps.Core.Automation.Abstractions.IAutoElement?>(_busy ? this : null);
    public Task<HushOps.Core.Automation.Abstractions.ElementVisibilityProbe> ProbeVisibilityAsync(System.Threading.CancellationToken ct = default)
        => Task.FromResult(new HushOps.Core.Automation.Abstractions.ElementVisibilityProbe{ InViewport=true, VisibleByStyle=true, PointerEventsEnabled=true, CenterOccluded=false});

    public Task<HushOps.Core.Automation.Abstractions.ElementComputedStyleProbe> GetComputedStyleProbeAsync(System.Threading.CancellationToken ct = default)
        => Task.FromResult(new HushOps.Core.Automation.Abstractions.ElementComputedStyleProbe{ Display="block", Visibility="visible", PointerEvents="auto", Opacity=1.0, Position="static", OverflowX="visible", OverflowY="visible"});

    public Task<HushOps.Core.Automation.Abstractions.ElementTextProbe> TextProbeAsync(System.Threading.CancellationToken ct = default)
        => Task.FromResult(new HushOps.Core.Automation.Abstractions.ElementTextProbe{ InnerText = string.Empty, TextContent = string.Empty, InnerTextLength = 0, TextContentLength = 0});

    public Task<HushOps.Core.Automation.Abstractions.ElementClickabilityProbe> GetClickabilityProbeAsync(System.Threading.CancellationToken ct = default)
        => Task.FromResult(new HushOps.Core.Automation.Abstractions.ElementClickabilityProbe
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

// 去除只读评估测试桩：预检器已改为强类型探针实现，无需 Evaluate。
