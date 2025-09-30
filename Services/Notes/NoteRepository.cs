using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HushOps.Servers.XiaoHongShu.Services.Notes;

/// <summary>
/// 中文：从本地种子数据集中检索小红书笔记并提供过滤/排序能力。
/// </summary>
public sealed class NoteRepository : INoteRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    private readonly ILogger<NoteRepository> _logger;
    private readonly string _seedPath;
    private readonly Lazy<IReadOnlyList<NoteRecord>> _notes;

    public NoteRepository(ILogger<NoteRepository> logger, IHostEnvironment environment)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        ArgumentNullException.ThrowIfNull(environment);

        _seedPath = Path.Combine(environment.ContentRootPath, "storage", "analytics", "seed-notes.json");
        _notes = new Lazy<IReadOnlyList<NoteRecord>>(LoadNotes, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public Task<IReadOnlyList<NoteRecord>> QueryAsync(string keyword, int targetCount, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var notes = _notes.Value;
        IEnumerable<NoteRecord> query = notes;

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var term = keyword.Trim();
            query = query.Where(note => MatchKeyword(note, term));
        }

        // 固定使用 score 降序排序（comprehensive 默认行为）
        query = query.OrderByDescending(note => TryParseDouble(GetValueOrDefault(note.Metrics, "score")));

        var result = query.Take(Math.Clamp(targetCount, 1, 200)).ToList();
        if (result.Count == 0)
        {
            _logger.LogInformation("[NoteRepository] 未找到匹配笔记 keyword={Keyword} target={Target}", keyword, targetCount);
        }

        return Task.FromResult<IReadOnlyList<NoteRecord>>(result);
    }

    private IReadOnlyList<NoteRecord> LoadNotes()
    {
        if (!File.Exists(_seedPath))
        {
            _logger.LogWarning("[NoteRepository] 种子数据不存在 path={Path}", _seedPath);
            return Array.Empty<NoteRecord>();
        }

        try
        {
            using var stream = File.OpenRead(_seedPath);
            var seeds = JsonSerializer.Deserialize<List<SeedNote>>(stream, SerializerOptions) ?? new List<SeedNote>();
            var mapped = seeds
                .Select(static seed => seed.ToRecord())
                .ToList();
            _logger.LogInformation("[NoteRepository] 已加载笔记种子 {Count}", mapped.Count);
            return mapped;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[NoteRepository] 解析种子数据失败 path={Path}", _seedPath);
            return Array.Empty<NoteRecord>();
        }
    }

    private static bool MatchKeyword(NoteRecord note, string term)
    {
        if (note.Title.Contains(term, StringComparison.OrdinalIgnoreCase) || note.Author.Contains(term, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (note.Metadata.TryGetValue("keyword", out var keyword) && keyword.Contains(term, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var tags = GetValueOrDefault(note.Additional, "tags");
        if (!string.IsNullOrWhiteSpace(tags))
        {
            foreach (var tag in tags.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                if (tag.Contains(term, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static string GetValueOrDefault(IReadOnlyDictionary<string, string> dictionary, string key)
        => dictionary.TryGetValue(key, out var value) ? value : string.Empty;

    private static DateTimeOffset TryParseDate(string value)
        => DateTimeOffset.TryParse(value, out var date) ? date : DateTimeOffset.MinValue;

    private static int TryParseInt(string value)
        => int.TryParse(value, out var number) ? number : 0;

    private static double TryParseDouble(string value)
        => double.TryParse(value, out var number) ? number : 0d;

    private sealed record SeedNote(
        string Id,
        string Title,
        string Author,
        string Url,
        Dictionary<string, string>? Metrics,
        Dictionary<string, string>? Additional,
        Dictionary<string, string>? Metadata)
    {
        public NoteRecord ToRecord()
        {
            var metrics = new ReadOnlyDictionary<string, string>(Metrics ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
            var additional = new ReadOnlyDictionary<string, string>(Additional ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
            var metadata = new ReadOnlyDictionary<string, string>(Metadata ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
            return new NoteRecord(Id, Title, Author, Url, metrics, additional, metadata);
        }
    }
}
