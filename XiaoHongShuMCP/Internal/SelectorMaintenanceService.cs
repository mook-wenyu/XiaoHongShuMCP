using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using XiaoHongShuMCP.Services;

namespace XiaoHongShuMCP.Internal;

internal static class SelectorMaintenanceService
{
    private static readonly JsonSerializerOptions SelectorReadOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly JsonSerializerOptions SelectorWriteOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static async Task<object> CompareProbeAndBuildPlan(
        string currentProbePath,
        string baselineProbePath,
        bool generatePlan = true,
        string? planOutputDir = null,
        IServiceProvider sp = null!)
    {
        if (!File.Exists(currentProbePath)) return new { status = "error", message = $"当前快照不存在: {currentProbePath}" };
        if (!File.Exists(baselineProbePath)) return new { status = "error", message = $"基线快照不存在: {baselineProbePath}" };

        var cur = System.Text.Json.JsonSerializer.Deserialize<XiaoHongShuMCP.Services.PageProbeResult>(await File.ReadAllTextAsync(currentProbePath));
        var baseRes = System.Text.Json.JsonSerializer.Deserialize<XiaoHongShuMCP.Services.PageProbeResult>(await File.ReadAllTextAsync(baselineProbePath));
        if (cur == null || baseRes == null) return new { status = "error", message = "快照解析失败" };

        var curMap = cur.Aliases.ToDictionary(a => a.Alias, StringComparer.Ordinal);
        var baseMap = baseRes.Aliases.ToDictionary(a => a.Alias, StringComparer.Ordinal);
        var allAliases = new HashSet<string>(curMap.Keys, StringComparer.Ordinal);
        allAliases.UnionWith(baseMap.Keys);

        var dom = sp?.GetService<XiaoHongShuMCP.Services.IDomElementManager>();
        var items = new List<HushOps.Core.Selectors.WeakSelectorPlanItem>();
        foreach (var alias in allAliases)
        {
            baseMap.TryGetValue(alias, out var b);
            curMap.TryGetValue(alias, out var c);
            var bFirst = b?.FirstMatchedSelector;
            var cFirst = c?.FirstMatchedSelector;
            if (!string.IsNullOrWhiteSpace(bFirst) && !string.IsNullOrWhiteSpace(cFirst) && !string.Equals(bFirst, cFirst, StringComparison.Ordinal))
            {
                var before = dom?.GetSelectors(alias) ?? new List<string>();
                if (before.Count == 0) continue;
                var after = new List<string> { cFirst! };
                after.AddRange(before.Where(s => !string.Equals(s, cFirst, StringComparison.Ordinal)));
                items.Add(new HushOps.Core.Selectors.WeakSelectorPlanItem(alias, before, after, new string[0]));
            }
        }

        string? planPath = null;
        if (generatePlan && items.Count > 0)
        {
            var dir = Path.GetFullPath(string.IsNullOrWhiteSpace(planOutputDir) ? "docs/selector-plans" : planOutputDir!);
            Directory.CreateDirectory(dir);
            planPath = Path.Combine(dir, $"plan-{DateTime.UtcNow:yyyyMMdd}.json");
            var plan = new HushOps.Core.Selectors.WeakSelectorPlan(items.ToArray());
            await File.WriteAllTextAsync(planPath, System.Text.Json.JsonSerializer.Serialize(plan, new System.Text.Json.JsonSerializerOptions{ WriteIndented = true }));
        }

        return new { status = items.Count>0?"ok":"noop", planPath, items = items.Count };
    }

    public static async Task<object> ExportWeakSelectorPlan(
        double successRateThreshold = 0.5,
        long minAttempts = 10,
        string? outputDir = null,
        IServiceProvider sp = null!)
    {
        var gov = sp.GetRequiredService<IWeakSelectorGovernor>();
        var plan = gov.BuildPlan(successRateThreshold, minAttempts);
        var dir = Path.GetFullPath(string.IsNullOrWhiteSpace(outputDir) ? "docs/selector-plans" : outputDir!);
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, $"plan-{DateTime.UtcNow:yyyyMMdd}.json");
        var json = JsonSerializer.Serialize(plan, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(file, json, Encoding.UTF8);
        return new { status = "ok", file, items = plan.Items.Count };
    }

    public static async Task<object> ApplyDomSelectorsPlanToSource(
        string planPath,
        string? sourcePath = null,
        string mode = "reorder")
    {
        if (string.IsNullOrWhiteSpace(planPath) || !File.Exists(planPath))
            return new { status = "error", message = "计划文件不存在" };
        var json = await File.ReadAllTextAsync(planPath, Encoding.UTF8);
        var plan = JsonSerializer.Deserialize<HushOps.Core.Selectors.WeakSelectorPlan>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (plan == null) return new { status = "error", message = "计划解析失败" };

        var src = Path.GetFullPath(string.IsNullOrWhiteSpace(sourcePath)
            ? Path.Combine("HushOps.Core", "Persistence", "Data", "locator-selectors.json")
            : sourcePath!);
        if (!File.Exists(src)) return new { status = "error", message = "源文件不存在" };

        var jsonText = await File.ReadAllTextAsync(src, Encoding.UTF8);
        var selectors = JsonSerializer.Deserialize<Dictionary<string, SelectorDocument>>(jsonText, SelectorReadOptions)
                        ?? new Dictionary<string, SelectorDocument>(StringComparer.Ordinal);

        int changes = 0;
        foreach (var item in plan.Items)
        {
            if (!selectors.TryGetValue(item.Alias, out var doc))
            {
                continue;
            }

            var newList = mode.Equals("prune", StringComparison.OrdinalIgnoreCase) && item.DemotedSelectors is { Count: > 0 }
                ? item.After.Except(item.DemotedSelectors).ToList()
                : item.After.ToList();
            if (newList.Count == 0)
            {
                continue;
            }

            doc.Selectors = newList;
            selectors[item.Alias] = doc;
            changes++;
        }

        if (changes > 0)
        {
            var updated = JsonSerializer.Serialize(selectors, SelectorWriteOptions);
            await File.WriteAllTextAsync(src, updated, Encoding.UTF8);
        }

        return new { status = changes > 0 ? "ok" : "noop", changes, source = src };
    }

    public static async Task<object> RollbackDomSelectorsPlanOnSource(
        string planPath,
        string? sourcePath = null)
    {
        if (string.IsNullOrWhiteSpace(planPath) || !File.Exists(planPath))
            return new { status = "error", message = "计划文件不存在" };
        var json = await File.ReadAllTextAsync(planPath, Encoding.UTF8);
        var plan = System.Text.Json.JsonSerializer.Deserialize<HushOps.Core.Selectors.WeakSelectorPlan>(json, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (plan == null) return new { status = "error", message = "计划解析失败" };

        var src = Path.GetFullPath(string.IsNullOrWhiteSpace(sourcePath)
            ? Path.Combine("HushOps.Core", "Persistence", "Data", "locator-selectors.json")
            : sourcePath!);
        if (!File.Exists(src)) return new { status = "error", message = "源文件不存在" };

        var jsonText = await File.ReadAllTextAsync(src, Encoding.UTF8);
        var selectors = JsonSerializer.Deserialize<Dictionary<string, SelectorDocument>>(jsonText, SelectorReadOptions)
                        ?? new Dictionary<string, SelectorDocument>(StringComparer.Ordinal);

        int changes = 0;
        foreach (var item in plan.Items)
        {
            if (!selectors.TryGetValue(item.Alias, out var doc))
            {
                continue;
            }

            var newList = item.Before.ToList();
            if (newList.Count == 0)
            {
                continue;
            }

            doc.Selectors = newList;
            selectors[item.Alias] = doc;
            changes++;
        }

        if (changes > 0)
        {
            var updated = JsonSerializer.Serialize(selectors, SelectorWriteOptions);
            await File.WriteAllTextAsync(src, updated, Encoding.UTF8);
        }

        return new { status = changes > 0 ? "ok" : "noop", changes, source = src };
    }

    private sealed class SelectorDocument
    {
        public List<string> Selectors { get; set; } = new();
        public Dictionary<string, List<string>>? States { get; set; }
    }
}
