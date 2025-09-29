using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace HushOps.Servers.XiaoHongShu.Services.Humanization.Interactions;

/// <summary>
/// 中文：从元数据中解析拟人化计划/执行摘要与告警。
/// English: Parses humanized action summaries and warnings from metadata payloads.
/// </summary>
public static class HumanizedActionMetadataReader
{
    public static HumanizedActionTelemetry Read(
        IReadOnlyDictionary<string, string>? metadata,
        HumanizedActionSummary? plannedFallback = null,
        HumanizedActionSummary? executedFallback = null,
        string warningPrefix = "consistency.warning.")
    {
        var planned = HumanizedActionSummaryExtensions.ReadSummary(
            metadata,
            "humanized.plan",
            plannedFallback ?? HumanizedActionSummary.Empty);

        var executed = HumanizedActionSummaryExtensions.ReadSummary(
            metadata,
            "humanized.execute",
            executedFallback ?? planned);

        var warnings = ReadIndexed(metadata, warningPrefix);
        return new HumanizedActionTelemetry(planned, executed, warnings);
    }

    public static IReadOnlyList<string> ReadIndexed(
        IReadOnlyDictionary<string, string>? metadata,
        string prefix)
    {
        if (metadata is null)
        {
            return Array.Empty<string>();
        }

        var indexed = metadata
            .Where(kv => kv.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(kv => new
            {
                Key = kv.Key.Substring(prefix.Length),
                kv.Value
            })
            .Where(item => int.TryParse(item.Key, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            .Select(item => new
            {
                Index = int.Parse(item.Key, CultureInfo.InvariantCulture),
                item.Value
            })
            .OrderBy(item => item.Index)
            .Select(item => item.Value)
            .ToArray();

        if (indexed.Length > 0)
        {
            return indexed;
        }

        var summaryKey = prefix.TrimEnd('.');
        if (metadata.TryGetValue(summaryKey, out var summary) && !string.IsNullOrWhiteSpace(summary))
        {
            return summary.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        return Array.Empty<string>();
    }
}

public sealed record HumanizedActionTelemetry(
    HumanizedActionSummary Planned,
    HumanizedActionSummary Executed,
    IReadOnlyList<string> Warnings);
