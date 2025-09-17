using NUnit.Framework;
using HushOps.Core.Networking;
using HushOps.Core.Automation.Abstractions;

namespace Tests.Core.Networking;

/// <summary>
/// 验证 WS 事件被聚合且在分类器返回 null 时计入 unknown 分组。
/// </summary>
public class ResponseAggregatorWsTests
{
    private sealed class DummyWsEvent : INetworkEvent
    {
        public NetworkEventKind Kind => NetworkEventKind.WebSocketFrame;
        public string Url => "wss://example/ws";
        public int? Status => null;
        public NetworkDirection? Direction => NetworkDirection.Inbound;
        public string? Payload => "{\"type\":\"ping\"}";
        public INetworkResponse? Http => null;
        public DateTime TimestampUtc { get; } = DateTime.UtcNow;
        public double? RttMs => null;
    }

    private sealed class NullClassifier : IEndpointClassifier
    {
        public string? Classify(NetworkEventKind kind, string url, int? status, string? payload, NetworkDirection? direction) => null;
    }

    [Test]
    public void WsEvent_Aggregated_To_Unknown_When_NoClassification()
    {
        var agg = new ResponseAggregator(TimeSpan.FromSeconds(30), null);
        agg.OnEvent(new DummyWsEvent(), new NullClassifier());
        var items = agg.GetResponses("unknown");
        Assert.That(items.Count, Is.EqualTo(1));
        Assert.That(items[0].Kind, Is.EqualTo(NetworkEventKind.WebSocketFrame));
    }
}
