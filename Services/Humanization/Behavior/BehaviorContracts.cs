using System.Collections.Generic;
using System.Threading;

namespace HushOps.Servers.XiaoHongShu.Services.Humanization.Behavior;

/// <summary>
/// 中文：描述拟人化行为阶段的枚举。
/// English: Represents different simulated behavior action types.
/// </summary>
public enum BehaviorActionType
{
    Unknown,
    NavigateRandom,
    NavigateKeyword,
    Like,
    Favorite,
    Comment,
    Capture
}

/// <summary>
/// 中文：执行行为前的上下文信息。
/// English: Context supplied to behavior controller before and after actions.
/// </summary>
public sealed record BehaviorActionContext(
    string ProfileKey,
    BehaviorActionType ActionType,
    IReadOnlyDictionary<string, string> Metadata,
    CancellationToken CancellationToken);

/// <summary>
/// 中文：行为动作执行后的结果摘要。
/// English: Result summary passed to the behavior controller after an action executes.
/// </summary>
public sealed record BehaviorResult(bool Success, string Status);

/// <summary>
/// 中文：行为控制器输出的轨迹与统计信息。
/// English: Trace information emitted by the behavior controller.
/// </summary>
public sealed record BehaviorTrace(
    BehaviorActionType ActionType,
    double DurationMs,
    double[] MousePathSamples,
    int TypoCount,
    int ScrollSegments,
    IReadOnlyDictionary<string, string> Extras);
