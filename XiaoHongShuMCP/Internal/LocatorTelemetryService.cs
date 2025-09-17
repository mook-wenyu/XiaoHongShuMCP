using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;

namespace XiaoHongShuMCP.Internal;

internal static class LocatorTelemetryService
{
    public static object GetLocatorTelemetrySummary(int top, IServiceProvider sp)
    {
        var tel = sp.GetRequiredService<HushOps.Core.Selectors.ISelectorTelemetry>();
        var stats = tel.GetStats();
        var items = new List<object>();
        foreach (var (alias, selMap) in stats)
        {
            var topItems = selMap
                .OrderByDescending(kv => kv.Value.SuccessRate)
                .ThenByDescending(kv => kv.Value.Attempts)
                .Take(Math.Max(1, top))
                .Select(kv => new
                {
                    Selector = kv.Key,
                    kv.Value.Attempts,
                    kv.Value.Successes,
                    kv.Value.SuccessRate,
                    kv.Value.AvgElapsedMs
                })
                .ToArray();
            items.Add(new { Alias = alias, Top = topItems });
        }
        return new { status = "ok", items };
    }

    public static async Task<object> DumpLocatorTelemetrySnapshot(string outputDir, IServiceProvider sp)
    {
        Directory.CreateDirectory(outputDir);
        var tel = sp.GetRequiredService<HushOps.Core.Selectors.ISelectorTelemetry>();
        await tel.WriteSnapshotAsync(outputDir, CancellationToken.None);
        return new { status = "ok", outputDir };
    }

    public static object ListWeakLocators(double successThreshold, int minCount, string? aliasFilter, IServiceProvider sp)
    {
        var tel = sp.GetRequiredService<HushOps.Core.Selectors.ISelectorTelemetry>();
        var stats = tel.GetStats();
        var list = new List<object>();
        foreach (var (alias, selMap) in stats)
        {
            if (!string.IsNullOrWhiteSpace(aliasFilter) && !alias.Contains(aliasFilter!, StringComparison.OrdinalIgnoreCase))
                continue;
            foreach (var (sel, st) in selMap)
            {
                if (st.Attempts >= minCount && st.SuccessRate < successThreshold)
                    list.Add(new { Alias = alias, Selector = sel, st.Attempts, st.SuccessRate, st.AvgElapsedMs });
            }
        }
        return new { status = "ok", count = list.Count, items = list };
    }
}

