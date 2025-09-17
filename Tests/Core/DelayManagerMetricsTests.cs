using System.Threading.Tasks;
using NUnit.Framework;
using HushOps.Core.Humanization;
using HushOps.Core.Observability;

namespace Tests.Core;

/// <summary>
/// DelayManager 指标测试：验证 human_delay_ms 被记录，且包含 wait_type 与 multiplier 标签。
/// </summary>
public class DelayManagerMetricsTests
{
    private sealed class CaptureMetrics : IMetrics
    {
        public double? LastValue;
        public LabelSet? LastLabels;
        private sealed class Hst : IHistogram
        {
            private readonly DelayManagerMetricsTests.CaptureMetrics _owner;
            public Hst(DelayManagerMetricsTests.CaptureMetrics owner) { _owner = owner; }
            public void Record(double value, in LabelSet labels) { _owner.LastValue = value; _owner.LastLabels = labels; }
        }
        private sealed class Ctr : ICounter { public void Add(long value, in LabelSet labels) { } }
        public ICounter CreateCounter(string name, string? description = null) => new Ctr();
        public IHistogram CreateHistogram(string name, string? description = null) => new Hst(this);
    }

    private sealed class ConstPacing : IPacingAdvisor
    {
        private readonly double _mult;
        public ConstPacing(double mult) { _mult = mult; }
        public void NotifyHttpStatus(int statusCode) { }
        public void ObserveRtt(System.TimeSpan rtt) { }
        public double CurrentMultiplier => _mult;
    }

    [Test]
    public async Task WaitAsync_Should_Record_Histogram_With_Labels()
    {
        var metrics = new CaptureMetrics();
        var pacing = new ConstPacing(1.5);
        var dm = new DelayManager(pacing, metrics);
        await dm.WaitAsync(HumanWaitType.TypingCharacter);

        Assert.That(metrics.LastValue.HasValue, Is.True);
        Assert.That(metrics.LastLabels.HasValue, Is.True);
        var map = metrics.LastLabels!.Value.Labels;
        Assert.That(map.ContainsKey("wait_type"), Is.True);
        Assert.That(map.ContainsKey("multiplier"), Is.True);
        Assert.That(map["wait_type"]?.ToString(), Is.EqualTo("TypingCharacter"));
        Assert.That(map["multiplier"], Is.EqualTo(1.5));
    }
}

