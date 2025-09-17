namespace HushOps.Core.Selectors;

/// <summary>
/// 弱选择器治理计划条目。
/// </summary>
public sealed record WeakSelectorPlanItem(string Alias, IReadOnlyList<string> Before, IReadOnlyList<string> After, IReadOnlyList<string> DemotedSelectors);

/// <summary>
/// 弱选择器治理计划：按别名给出“应用前后顺序”和被降权的选择器集合。
/// </summary>
public sealed record WeakSelectorPlan(IReadOnlyList<WeakSelectorPlanItem> Items);

