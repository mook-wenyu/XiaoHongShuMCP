using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HushOps.Servers.XiaoHongShu.Infrastructure.ToolExecution;
using Microsoft.Extensions.Logging;

namespace HushOps.Servers.XiaoHongShu.Services.Notes;

public sealed class NoteCaptureService : INoteCaptureService
{
    private readonly INoteRepository _repository;
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<NoteCaptureService> _logger;

    public NoteCaptureService(
        INoteRepository repository,
        IFileSystem fileSystem,
        ILogger<NoteCaptureService> logger)
    {
        _repository = repository;
        _fileSystem = fileSystem;
        _logger = logger;
    }

    public async Task<NoteCaptureResult> CaptureAsync(NoteCaptureContext context, CancellationToken cancellationToken)
    {
        var stopwatch = ValueStopwatch.StartNew();
        var notes = await _repository.QueryAsync(context.Keyword, context.TargetCount, cancellationToken).ConfigureAwait(false);
        if (notes.Count == 0)
        {
            throw new InvalidOperationException("未采集到任何笔记，请检查关键词或画像标签是否有效。");
        }

        var outputDir = string.IsNullOrWhiteSpace(context.OutputDirectory) ? "./logs/note-capture" : context.OutputDirectory.Trim();
        var absoluteDir = _fileSystem.Path.GetFullPath(outputDir);
        _fileSystem.Directory.CreateDirectory(absoluteDir);

        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);
        var csvPath = _fileSystem.Path.Combine(absoluteDir, $"note-capture-{timestamp}.csv");
        WriteCsv(csvPath, notes);

        string? rawPath = null;
        if (context.IncludeRaw)
        {
            rawPath = _fileSystem.Path.Combine(absoluteDir, $"note-capture-{timestamp}.json");
            WriteRawJson(rawPath, notes);
        }

        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["keyword"] = context.Keyword,
            ["targetCount"] = context.TargetCount.ToString(CultureInfo.InvariantCulture),
            ["includeAnalytics"] = context.IncludeAnalytics.ToString()
        };

        return new NoteCaptureResult(
            Notes: notes,
            CsvPath: csvPath,
            RawPath: rawPath,
            Duration: stopwatch.GetElapsedTime(),
            Metadata: metadata);
    }

    private void WriteCsv(string path, IReadOnlyList<NoteRecord> notes)
    {
        using var stream = _fileSystem.File.Create(path);
        using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        var metricKeys = CollectMetricKeys(notes);
        var headers = new List<string>(capacity: 4 + metricKeys.Count)
        {
            "title",
            "author",
            "url",
            "id"
        };
        headers.AddRange(metricKeys);

        writer.WriteLine(string.Join(',', headers.Select(EscapeCsv)));

        foreach (var note in notes)
        {
            var row = new List<string>(capacity: headers.Count)
            {
                note.Title,
                note.Author,
                note.Url,
                note.Id
            };

            foreach (var key in metricKeys)
            {
                row.Add(note.Metrics.TryGetValue(key, out var value) ? value ?? string.Empty : string.Empty);
            }

            writer.WriteLine(string.Join(',', row.Select(EscapeCsv)));
        }
    }

    private void WriteRawJson(string path, IReadOnlyList<NoteRecord> notes)
    {
        using var stream = _fileSystem.File.Create(path);
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
        {
            Indented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
        JsonSerializer.Serialize(writer, notes);
    }

    private static string EscapeCsv(string value)
    {
        if (value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0)
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }

    private static IList<string> CollectMetricKeys(IReadOnlyList<NoteRecord> notes)
    {
        var keys = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var note in notes)
        {
            foreach (var key in note.Metrics.Keys)
            {
                if (seen.Add(key))
                {
                    keys.Add(key);
                }
            }
        }

        return keys;
    }
}

public interface INoteRepository
{
    Task<IReadOnlyList<NoteRecord>> QueryAsync(string keyword, int targetCount, CancellationToken cancellationToken);
}

public interface IFileSystem
{
    string CurrentDirectory { get; }
    IFile File { get; }
    IDirectory Directory { get; }
    IPath Path { get; }
}

public interface IFile
{
    Stream Create(string path);
}

public interface IDirectory
{
    void CreateDirectory(string path);
    bool Exists(string path);
}

public interface IPath
{
    string GetFullPath(string path);
    string Combine(params string[] paths);
}

internal readonly struct ValueStopwatch
{
    private readonly long _start;

    private ValueStopwatch(long start) => _start = start;

    public static ValueStopwatch StartNew() => new(DateTime.UtcNow.Ticks);

    public TimeSpan GetElapsedTime() => TimeSpan.FromTicks(DateTime.UtcNow.Ticks - _start);
}
