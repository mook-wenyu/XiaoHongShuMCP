using System;

namespace HushOps.Servers.XiaoHongShu.Services.Humanization.Interactions;

/// <summary>
/// 中文：描述单个动作的时间控制参数。
/// English: Describes timing configuration for a single action.
/// </summary>
public sealed record HumanizedActionTiming
{
    public HumanizedActionTiming(
        TimeSpan? delayBefore = null,
        TimeSpan? delayAfter = null,
        TimeSpan? timeout = null,
        TimeSpan? idlePause = null)
    {
        DelayBefore = Normalize(delayBefore, TimeSpan.Zero);
        DelayAfter = Normalize(delayAfter, TimeSpan.Zero);
        Timeout = Normalize(timeout, TimeSpan.FromSeconds(10));
        IdlePause = Normalize(idlePause, TimeSpan.Zero);
    }

    /// <summary>
    /// 中文：动作前等待时间。
    /// English: Delay before executing the action.
    /// </summary>
    public TimeSpan DelayBefore { get; }

    /// <summary>
    /// 中文：动作后等待时间。
    /// English: Delay after executing the action.
    /// </summary>
    public TimeSpan DelayAfter { get; }

    /// <summary>
    /// 中文：动作超时时间。
    /// English: Timeout applied to the action execution.
    /// </summary>
    public TimeSpan Timeout { get; }

    /// <summary>
    /// 中文：模拟“停顿观察”的额外时间。
    /// English: Additional idle pause to simulate user observation.
    /// </summary>
    public TimeSpan IdlePause { get; }

    public static HumanizedActionTiming Default { get; } = new();

    private static TimeSpan Normalize(TimeSpan? value, TimeSpan fallback)
    {
        if (!value.HasValue)
        {
            return fallback;
        }

        var actual = value.Value;
        if (actual < TimeSpan.Zero)
        {
            return fallback;
        }

        return actual;
    }
}
