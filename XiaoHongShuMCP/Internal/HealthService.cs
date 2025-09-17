using System.ComponentModel;
using System.Text.Json.Serialization;

namespace XiaoHongShuMCP.Internal;

internal static class HealthService
{
    private static readonly DateTime _startedAtUtc = DateTime.UtcNow;

    public sealed record HealthStatus(
        string Service,
        string Version,
        string Status,
        double UptimeSeconds,
        object? Limits = null,
        [property: JsonPropertyName("errorCode")] string? ErrorCode = null,
        [property: JsonPropertyName("message")] string? Message = null,
        [property: JsonPropertyName("retriable")] bool? Retriable = null,
        [property: JsonPropertyName("requestId")] string? RequestId = null
    );

    public static Task<HealthStatus> GetHealth(
        string? requestId = null)
    {
        var asm = typeof(HealthService).Assembly.GetName();
        var ver = asm.Version?.ToString() ?? "0.0.0";
        var up = (DateTime.UtcNow - _startedAtUtc).TotalSeconds;
        return Task.FromResult(new HealthStatus("xiaohongshu-mcp", ver, "ok", up, null, null, null, null, requestId));
    }
}


