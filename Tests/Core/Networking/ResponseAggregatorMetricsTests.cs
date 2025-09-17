using System.Threading.Tasks;
using HushOps.Core.Automation.Abstractions;
using HushOps.Core.Networking;
using HushOps.Core.Observability;
using NUnit.Framework;

namespace Tests.Core.Networking;

public class ResponseAggregatorMetricsTests
{
    private sealed class CapturingMetrics : IMetrics
    {
        public long Total { get; private set; }
        public double LastWindow { get; private set; }
        private sealed class C : ICounter { private readonly System.Action<long, LabelSet> a; public C(System.Action<long, LabelSet> a){this.a=a;} public void Add(long value, in LabelSet labels)=>a(value, labels);}        
        private sealed class H : IHistogram { private readonly System.Action<double, LabelSet> r; public H(System.Action<double, LabelSet> r){this.r=r;} public void Record(double value, in LabelSet labels)=>r(value, labels);}        
        public ICounter CreateCounter(string name, string? description = null) => name=="net_aggregated_total" ? new C((v,l)=> Total += v) : new C((_,__)=>{});
        public IHistogram CreateHistogram(string name, string? description = null) => name=="net_window_items" ? new H((v,l)=> LastWindow = v) : new H((_,__)=>{});
    }

    private sealed class EventHttp : INetworkEvent
    {
        public required string Url { get; init; }
        public int? Status { get; init; }
        public string? Payload { get; init; }
        public INetworkResponse? Http { get; init; }
        public NetworkEventKind Kind => NetworkEventKind.HttpResponse;
        public NetworkDirection? Direction => null;
        public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
        public double? RttMs { get; init; }
    }

    private sealed class Classifier : IEndpointClassifier
    { public string? Classify(NetworkEventKind kind, string url, int? status, string? payload, NetworkDirection? direction) => url.Contains("homefeed")?"Homefeed":null; }

    [Test]
    public void Aggregator_Should_Record_Counters()
    {
        var m = new CapturingMetrics();
        var agg = new ResponseAggregator(System.TimeSpan.FromMinutes(2), m);
        var cls = new Classifier();
        agg.OnEvent(new EventHttp{Url="https://x.com/api/sns/web/v1/homefeed", Status=200, Payload="{}"}, cls);
        agg.OnEvent(new EventHttp{Url="https://x.com/api/sns/web/v1/homefeed?id=1", Status=200, Payload="{}"}, cls);
        var list = agg.GetResponses("Homefeed");
        Assert.That(m.Total, Is.EqualTo(2));
        Assert.That(list.Count, Is.EqualTo(2));
        Assert.That(m.LastWindow, Is.GreaterThanOrEqualTo(2));
    }

    private sealed class EventWs : INetworkEvent
    {
        public required string Url { get; init; }
        public string? Payload { get; init; }
        public NetworkDirection? Direction { get; init; }
        public NetworkEventKind Kind => NetworkEventKind.WebSocketFrame;
        public int? Status => null;
        public INetworkResponse? Http => null;
        public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
        public double? RttMs => null;
    }
    [Test]
    public void Aggregator_Should_Handle_WebSocket_Events()
    {
        var m = new CapturingMetrics();
        var agg = new ResponseAggregator(System.TimeSpan.FromMinutes(2), m);
        var cls = new Classifier();
        // 两条 WS 帧事件（同一 URL 不同载荷）应计入 unknown 端点
        agg.OnEvent(new EventWs{ Url = "wss://x.com/ws/stream", Payload = "hello", Direction = NetworkDirection.Inbound }, cls);
        agg.OnEvent(new EventWs{ Url = "wss://x.com/ws/stream", Payload = "world", Direction = NetworkDirection.Outbound }, cls);
        var snapshot = agg.GetResponses("unknown");
        Assert.That(snapshot.Count, Is.GreaterThanOrEqualTo(2));
    }
}
