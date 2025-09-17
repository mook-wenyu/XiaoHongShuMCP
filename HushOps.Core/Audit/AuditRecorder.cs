using System.Text.Json;
using System.Text.Json.Serialization;

namespace HushOps.Core.Audit;

public sealed class AuditRecorder : IAsyncDisposable, IDisposable
{
    private readonly string _dir;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public AuditRecorder(string directory)
    {
        _dir = directory;
        Directory.CreateDirectory(_dir);
    }

    public void Write(string kind, object payload)
    {
        var ts = DateTimeOffset.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
        var file = Path.Combine(_dir, $"{ts}_{kind}.jsonl");
        var json = JsonSerializer.Serialize(payload, _jsonOptions);
        File.AppendAllText(file, json + Environment.NewLine);
    }

    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

