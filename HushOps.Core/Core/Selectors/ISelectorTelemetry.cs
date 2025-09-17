using System.Collections.Generic;

namespace HushOps.Core.Selectors;

/// <summary>
/// 选择器遥测接口：记录别名-候选选择器在运行期的命中率/耗时/命中顺序，并支持基于历史统计的优先级重排。
/// </summary>
public interface ISelectorTelemetry
{
    void RecordAttempt(string alias, string selector, bool success, long elapsedMs, int attemptOrder);
    IEnumerable<string> OrderByTelemetry(string alias, IEnumerable<string> originalOrder);
    Task WriteSnapshotAsync(string directory, CancellationToken ct = default);
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, SelectorStat>> GetStats();
}

/// <summary>
/// 单个选择器统计信息（只读快照）。
/// </summary>
public sealed class SelectorStat
{
    public long Attempts { get; init; }
    public long Successes { get; init; }
    public double SuccessRate { get; init; }
    public double? AvgElapsedMs { get; init; }
    public DateTime? LastHitUtc { get; init; }
    public long? FirstSuccessAttemptOrderMin { get; init; }
}

