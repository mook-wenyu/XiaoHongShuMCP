using FluentAssertions;
using HushOps.Core.Network;

namespace HushOps.Core.Tests;

public class EndpointClassifierTests
{
    [Fact]
    public void Classify_Url_Match_First()
    {
        var c = new EndpointClassifier()
            .AddUrlRule(@"/api/v1/login", "auth.login", "http");
        var e = new ApiEvent(EventKind.Http, "https://x.com/api/v1/login", 200, null, DateTimeOffset.UtcNow);
        var k = c.Classify(e);
        k.Endpoint.Should().Be("auth.login");
        k.Kind.Should().Be("http");
        k.StatusClass.Should().Be("2xx");
    }

    [Fact]
    public void Classify_Body_Template_Second()
    {
        var c = new EndpointClassifier()
            .AddBodyRule("feed", "feed.list", "http");
        var e = new ApiEvent(EventKind.Http, "https://x.com/unknown", 200, "{\"feed\":[1]}", DateTimeOffset.UtcNow);
        var k = c.Classify(e);
        k.Endpoint.Should().Be("feed.list");
    }

    [Fact]
    public void Classify_Ws_Semantics_Third()
    {
        var c = new EndpointClassifier()
            .AddWebSocketRule("ping", "ws.ping");
        var e = new ApiEvent(EventKind.WebSocket, "wss://x/ws", null, "{type: 'ping'}", DateTimeOffset.UtcNow);
        var k = c.Classify(e);
        k.Endpoint.Should().Be("ws.ping");
        k.Kind.Should().Be("ws");
    }
}

