namespace HushOps.Core.Network;

public sealed record EndpointKey(string Endpoint, string Kind, string StatusClass)
{
    public override string ToString() => $"{Endpoint}|{Kind}|{StatusClass}";
}

public enum EventKind { Http, WebSocket, Worker }

public sealed record ApiEvent(EventKind Kind, string Endpoint, int? StatusCode, string? BodySample, DateTimeOffset Timestamp);

