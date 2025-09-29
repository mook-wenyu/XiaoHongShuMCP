namespace HushOps.Servers.XiaoHongShu.Services.Humanization.Interactions;

/// <summary>
/// 中文：描述单个拟人化动作。
/// English: Represents a single humanized interaction action.
/// </summary>
public sealed record HumanizedAction(
    HumanizedActionType Type,
    ActionLocator Target,
    HumanizedActionTiming Timing,
    HumanizedActionParameters Parameters,
    string BehaviorProfile = "default",
    string? Description = null)
{
    public static HumanizedAction Create(
        HumanizedActionType type,
        ActionLocator? target = null,
        HumanizedActionTiming? timing = null,
        HumanizedActionParameters? parameters = null,
        string? behaviorProfile = null,
        string? description = null)
        => new(
            type,
            target ?? ActionLocator.Empty,
            timing ?? HumanizedActionTiming.Default,
            parameters ?? HumanizedActionParameters.Empty,
            string.IsNullOrWhiteSpace(behaviorProfile) ? "default" : behaviorProfile!,
            description);
}
