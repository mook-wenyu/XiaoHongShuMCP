using System.Threading;
using System.Threading.Tasks;
using HushOps.Core.Automation.Abstractions;
using HushOps.Core.Humanization;
using HushOps.Core.Observability;
using NUnit.Framework;

namespace Tests.Core.Humanization;

/// <summary>
/// DomPreflightInspector 指标计数单测：验证 disabled/busy/ready 三路径的计数器增加。
/// </summary>
public class DomPreflightInspectorMetricsTests
{
    private sealed class CapturingMetrics : IMetrics
    {
        public long Ready { get; private set; }
        public long Busy { get; private set; }
        public long Disabled { get; private set; }
        public ICounter CreateCounter(string name, string? description = null)
        {
            return name switch
            {
                "preflight_ready_total" => new C(v => Ready += v),
                "preflight_busy_total" => new C(v => Busy += v),
                "preflight_disabled_total" => new C(v => Disabled += v),
                _ => new C(_ => { })
            };
        }
        public IHistogram CreateHistogram(string name, string? description = null) => new H();
        private sealed class C : ICounter { private readonly System.Action<long> add; public C(System.Action<long> a){add=a;} public void Add(long value, in LabelSet labels) => add(value);}        
        private sealed class H : IHistogram { public void Record(double value, in LabelSet labels) { } }
    }

    private sealed class FakeEl : IAutoElement
    {
        private readonly string? role; private readonly bool dis; private readonly bool busy;
        public FakeEl(string? role, bool dis, bool busy) { this.role = role; this.dis = dis; this.busy = busy; }
        public Task ClickAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task TypeAsync(string text, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> IsVisibleAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task HoverAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task ScrollIntoViewIfNeededAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task<T?> EvaluateAsync<T>(string script, CancellationToken ct = default) => Task.FromResult(default(T));
        public Task<BoundingBox?> GetBoundingBoxAsync(CancellationToken ct = default) => Task.FromResult<BoundingBox?>(new BoundingBox());
        public Task<(double x, double y)?> GetCenterAsync(CancellationToken ct = default) => Task.FromResult<(double, double)?>( (0,0) );
        public Task<string?> GetAttributeAsync(string name, CancellationToken ct = default)
        {
            if (name == "role") return Task.FromResult<string?>(role);
            if (name == "aria-disabled") return Task.FromResult<string?>(dis ? "true" : null);
            if (name == "disabled") return Task.FromResult<string?>(dis ? string.Empty : null);
            if (name == "aria-busy") return Task.FromResult<string?>(busy ? "true" : null);
            if (name == "data-loading") return Task.FromResult<string?>(busy ? "true" : null);
            if (name == "tabindex") return Task.FromResult<string?>("0");
            return Task.FromResult<string?>(null);
        }
        public Task<string> InnerTextAsync(CancellationToken ct = default) => Task.FromResult(string.Empty);
        public Task<string> GetTagNameAsync(CancellationToken ct = default) => Task.FromResult("button");
        public Task<IAutoElement?> QuerySelectorAsync(string selector, int timeoutMs = 1000, CancellationToken ct = default)
            => Task.FromResult<IAutoElement?>(busy ? this : null);
        public Task<ElementVisibilityProbe> ProbeVisibilityAsync(CancellationToken ct = default) => Task.FromResult(new ElementVisibilityProbe());
        public Task<ElementComputedStyleProbe> GetComputedStyleProbeAsync(CancellationToken ct = default) => Task.FromResult(new ElementComputedStyleProbe());
        public Task<ElementTextProbe> TextProbeAsync(CancellationToken ct = default) => Task.FromResult(new ElementTextProbe());
        public Task<ElementClickabilityProbe> GetClickabilityProbeAsync(CancellationToken ct = default) => Task.FromResult(new ElementClickabilityProbe());
    }

    // 去除 IReadonlyJsEvaluator 相关：预检器已基于强类型探针实现

    [Test]
    public async Task Metrics_Should_Count_Disabled_Busy_Ready()
    {
        var m = new CapturingMetrics();
        var insp = new DomPreflightInspector(m);

        await insp.InspectAsync(new FakeEl("button", dis: true, busy: false));
        await insp.InspectAsync(new FakeEl("button", dis: false, busy: true));
        await insp.InspectAsync(new FakeEl("button", dis: false, busy: false));

        Assert.That(m.Disabled, Is.EqualTo(1));
        Assert.That(m.Busy, Is.EqualTo(1));
        Assert.That(m.Ready, Is.EqualTo(1));
    }
}
