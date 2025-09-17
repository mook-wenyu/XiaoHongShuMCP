using FluentAssertions;
using HushOps.Core.Network;

namespace HushOps.Core.Tests;

public class ResponseAggregatorTests
{
    [Fact]
    public void Deduplicate_Within_Window()
    {
        var cls = new EndpointClassifier().AddUrlRule("/api/v1/items", "items.list", "http");
        var agg = new ResponseAggregator(cls, TimeSpan.FromSeconds(5));
        var e1 = new ApiEvent(EventKind.Http, "https://x/api/v1/items", 200, "{\"items\":[1]}", DateTimeOffset.UtcNow);
        var e2 = e1 with { Timestamp = DateTimeOffset.UtcNow.AddSeconds(1) };
        var k1 = agg.OnEvent(e1);
        var k2 = agg.OnEvent(e2);
        k1.Endpoint.Should().Be("items.list");
        k2.Endpoint.Should().Be("items.list");
    }
}

