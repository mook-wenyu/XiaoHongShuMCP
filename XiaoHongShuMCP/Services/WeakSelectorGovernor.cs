using System.Collections.Immutable;

namespace XiaoHongShuMCP.Services;

/// <summary>
/// 弱选择器治理器：基于遥测统计对“弱选择器”进行降权（移动到末尾），支持预览与应用。
/// - 规则：成功率 < 阈值 且 尝试次数 ≥ 最小样本 的候选，认定为弱选择器；
/// - 顺序：保持强选择器的原相对顺序，再接上弱选择器原相对顺序；
/// - 只影响通用别名集合；不覆盖页面状态特定集合（如 alias@state）。
/// </summary>
public sealed class WeakSelectorGovernor : IWeakSelectorGovernor
{
    private readonly IDomElementManager _dom;
    private readonly HushOps.Core.Selectors.ISelectorTelemetry _telemetry;

    public WeakSelectorGovernor(IDomElementManager dom, HushOps.Core.Selectors.ISelectorTelemetry telemetry)
    {
        _dom = dom;
        _telemetry = telemetry;
    }

    public HushOps.Core.Selectors.WeakSelectorPlan BuildPlan(double successRateThreshold, long minAttempts)
    {
        if (successRateThreshold <= 0) successRateThreshold = 0.01;
        if (successRateThreshold >= 1) successRateThreshold = 0.99;
        if (minAttempts < 1) minAttempts = 1;

        var stats = _telemetry.GetStats();
        var all = _dom.GetAllSelectors();
        var items = new List<HushOps.Core.Selectors.WeakSelectorPlanItem>();
        foreach (var (alias, before) in all)
        {
            if (before.Count <= 1)
            {
                continue; // 单一候选无需治理
            }
            var selectorStats = stats.TryGetValue(alias, out var m) ? m : new Dictionary<string, HushOps.Core.Selectors.SelectorStat>();
            var strong = new List<string>();
            var weak = new List<string>();
            foreach (var s in before)
            {
                var st = selectorStats.TryGetValue(s, out var snap) ? snap : null;
                if (st == null || st.Attempts < minAttempts)
                {
                    strong.Add(s); // 样本不足，不处理，保持原位
                }
                else if (st.SuccessRate < successRateThreshold)
                {
                    weak.Add(s);
                }
                else
                {
                    strong.Add(s);
                }
            }
            if (weak.Count == 0) continue;
            var after = strong.Concat(weak).ToList();
            if (after.SequenceEqual(before)) continue;
            items.Add(new HushOps.Core.Selectors.WeakSelectorPlanItem(alias, before.ToImmutableArray(), after.ToImmutableArray(), weak.ToImmutableArray()));
        }
        return new HushOps.Core.Selectors.WeakSelectorPlan(items.ToImmutableArray());
    }

    public bool ApplyPlan(HushOps.Core.Selectors.WeakSelectorPlan plan)
    {
        var ok = true;
        foreach (var item in plan.Items)
        {
            ok &= _dom.TryReorderSelectors(item.Alias, item.After);
        }
        return ok;
    }
}
