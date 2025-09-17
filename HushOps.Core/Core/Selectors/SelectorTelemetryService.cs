using System.Text.Json;

namespace HushOps.Core.Selectors;

/// <summary>
/// 选择器遥测实现（Core）：按成功率/平均耗时/首次命中序进行重排；支持快照导出。
/// </summary>
public sealed class SelectorTelemetryService : ISelectorTelemetry
{
    private sealed class Stat
    {
        public long Attempts;
        public long Successes;
        public long SuccessElapsedMsSum;
        public long LastHitTicks;
        public long FirstSuccessAttemptOrderMin = long.MaxValue;
    }

    private readonly Dictionary<string, Dictionary<string, Stat>> _stats = new(StringComparer.Ordinal);

    public void RecordAttempt(string alias, string selector, bool success, long elapsedMs, int attemptOrder)
    {
        if (!_stats.TryGetValue(alias, out var map))
        {
            map = new Dictionary<string, Stat>(StringComparer.Ordinal);
            _stats[alias] = map;
        }
        if (!map.TryGetValue(selector, out var s))
        {
            s = new Stat();
            map[selector] = s;
        }
        s.Attempts++;
        if (success)
        {
            s.Successes++;
            s.SuccessElapsedMsSum += Math.Max(0, elapsedMs);
            s.LastHitTicks = DateTime.UtcNow.Ticks;
            if (attemptOrder >= 1) s.FirstSuccessAttemptOrderMin = Math.Min(s.FirstSuccessAttemptOrderMin, attemptOrder);
        }
    }

    public IEnumerable<string> OrderByTelemetry(string alias, IEnumerable<string> originalOrder)
    {
        var list = originalOrder.ToList();
        if (!_stats.TryGetValue(alias, out var map) || map.Count == 0) return list;

        return list
            .Select(sel => (sel, stat: map.TryGetValue(sel, out var s) ? s : null))
            .OrderByDescending(t => (t.stat?.Successes ?? 0) / (double)Math.Max(1, t.stat?.Attempts ?? 1))
            .ThenBy(t => t.stat is { Successes: > 0 } ? (t.stat.SuccessElapsedMsSum / (double)t.stat.Successes) : double.MaxValue)
            .ThenBy(t => t.stat?.FirstSuccessAttemptOrderMin ?? long.MaxValue)
            .Select(t => t.sel)
            .ToList();
    }

    public async Task WriteSnapshotAsync(string directory, CancellationToken ct = default)
    {
        try
        {
            Directory.CreateDirectory(directory);
            var snapshot = _stats.ToDictionary(
                a => a.Key,
                a => a.Value.ToDictionary(
                    s => s.Key,
                    s => new
                    {
                        attempts = s.Value.Attempts,
                        successes = s.Value.Successes,
                        successRate = s.Value.Attempts == 0 ? 0 : (double)s.Value.Successes / s.Value.Attempts,
                        avgElapsedMs = s.Value.Successes == 0 ? (object?)null : s.Value.SuccessElapsedMsSum / (double)s.Value.Successes,
                        lastHitUtc = s.Value.LastHitTicks == 0 ? (object?)null : new DateTime(s.Value.LastHitTicks, DateTimeKind.Utc),
                        firstSuccessAttemptOrderMin = s.Value.FirstSuccessAttemptOrderMin == long.MaxValue ? (object?)null : s.Value.FirstSuccessAttemptOrderMin
                    }
                ));
            var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
            var path = Path.Combine(directory, $"selectors_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json");
            await File.WriteAllTextAsync(path, json, ct);
        }
        catch { }
    }

    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, SelectorStat>> GetStats()
    {
        var dict = new Dictionary<string, IReadOnlyDictionary<string, SelectorStat>>(StringComparer.Ordinal);
        foreach (var (alias, map) in _stats)
        {
            var inner = new Dictionary<string, SelectorStat>(StringComparer.Ordinal);
            foreach (var (selector, s) in map)
            {
                inner[selector] = new SelectorStat
                {
                    Attempts = s.Attempts,
                    Successes = s.Successes,
                    SuccessRate = s.Attempts == 0 ? 0 : (double)s.Successes / s.Attempts,
                    AvgElapsedMs = s.Successes == 0 ? null : s.SuccessElapsedMsSum / (double)s.Successes,
                    LastHitUtc = s.LastHitTicks == 0 ? null : new DateTime(s.LastHitTicks, DateTimeKind.Utc),
                    FirstSuccessAttemptOrderMin = s.FirstSuccessAttemptOrderMin == long.MaxValue ? null : s.FirstSuccessAttemptOrderMin
                };
            }
            dict[alias] = inner;
        }
        return dict;
    }
}

