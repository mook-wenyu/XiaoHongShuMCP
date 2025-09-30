using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HushOps.Servers.XiaoHongShu.Services.Notes;

public interface INoteEngagementService
{
    Task LikeAsync(string keyword, CancellationToken cancellationToken);
    Task FavoriteAsync(string keyword, CancellationToken cancellationToken);
    Task CommentAsync(string keyword, string comment, CancellationToken cancellationToken);
}

public interface INoteCaptureService
{
    Task<NoteCaptureResult> CaptureAsync(NoteCaptureContext context, CancellationToken cancellationToken);
}

public sealed record NoteCaptureContext(
    string Keyword,
    int TargetCount,
    bool IncludeAnalytics,
    bool IncludeRaw,
    string OutputDirectory);

public sealed record NoteCaptureResult(
    IReadOnlyList<NoteRecord> Notes,
    string CsvPath,
    string? RawPath,
    TimeSpan Duration,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record NoteRecord(
    string Id,
    string Title,
    string Author,
    string Url,
    IReadOnlyDictionary<string, string> Metrics,
    IReadOnlyDictionary<string, string> Additional,
    IReadOnlyDictionary<string, string> Metadata);
