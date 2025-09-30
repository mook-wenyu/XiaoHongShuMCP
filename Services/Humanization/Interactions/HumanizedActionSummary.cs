using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace HushOps.Servers.XiaoHongShu.Services.Humanization.Interactions;

/// <summary>
/// 计划/执行的人机化动作概览，用于将动作数量与名称列表以结构化方式回传调用方。
/// </summary>
public sealed record HumanizedActionSummary(int Count, string[] Actions)
{
    public static HumanizedActionSummary Empty { get; } = new(0, Array.Empty<string>());

    public static HumanizedActionSummary FromActions(IEnumerable<HumanizedAction> actions)
    {
        if (actions is null)
        {
            return Empty;
        }

        var list = actions.Select(static action => action.Type.ToString())
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Select(static name => name.Trim())
            .ToArray();

        return list.Length == 0
            ? Empty
            : new HumanizedActionSummary(list.Length, list);
    }

    public static HumanizedActionSummary FromStrings(IEnumerable<string> actions)
    {
        if (actions is null)
        {
            return Empty;
        }

        var list = actions.Where(static name => !string.IsNullOrWhiteSpace(name))
            .Select(static name => name.Trim())
            .ToArray();

        return list.Length == 0
            ? Empty
            : new HumanizedActionSummary(list.Length, list);
    }
}

public static class HumanizedActionSummaryExtensions
{
    public static HumanizedActionSummary ToSummary(this HumanizedActionScript script)
        => script is null ? HumanizedActionSummary.Empty : HumanizedActionSummary.FromActions(script.Actions);

    public static HumanizedActionSummary ToSummary(this IEnumerable<HumanizedAction> actions)
        => HumanizedActionSummary.FromActions(actions);

    public static HumanizedActionSummary ReadSummary(
        IReadOnlyDictionary<string, string>? metadata,
        string root,
        HumanizedActionSummary? fallback = null)
    {
        var defaultSummary = fallback ?? HumanizedActionSummary.Empty;
        if (metadata is null)
        {
            return defaultSummary;
        }

        var actions = new List<string>();
        var prefix = string.Concat(root, ".actions");
        var index = 0;
        while (metadata.TryGetValue($"{prefix}.{index}", out var value))
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                actions.Add(value.Trim());
            }

            index++;
        }

        if (actions.Count == 0 && metadata.TryGetValue(prefix, out var inline) && !string.IsNullOrWhiteSpace(inline))
        {
            actions.AddRange(inline.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        if (actions.Count > 0)
        {
            return HumanizedActionSummary.FromStrings(actions);
        }

        if (metadata.TryGetValue(string.Concat(root, ".count"), out var countText) &&
            int.TryParse(countText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) &&
            count == 0)
        {
            return HumanizedActionSummary.Empty;
        }

        return defaultSummary;
    }
}
