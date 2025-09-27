using System;
using System.Collections.Generic;

namespace HushOps.Servers.XiaoHongShu.Configuration;

/// <summary>
/// 中文：拟人化行为全局配置，用于定义不同 profile 的延迟与动作参数。
/// English: Global configuration describing human behavior profiles for interaction simulation.
/// </summary>
public sealed class HumanBehaviorOptions
{
    public const string SectionName = "HumanBehavior";

    /// <summary>
    /// 中文：默认使用的行为模板键。
    /// English: Default behavior profile key to fall back to.
    /// </summary>
    public string DefaultProfile { get; set; } = "default";

    /// <summary>
    /// 中文：全部行为模板配置。
    /// English: All configured behavior profiles.
    /// </summary>
    public IDictionary<string, HumanBehaviorProfileOptions> Profiles { get; set; }
        = CreateDefaultProfiles();

    private static IDictionary<string, HumanBehaviorProfileOptions> CreateDefaultProfiles()
        => new Dictionary<string, HumanBehaviorProfileOptions>(StringComparer.OrdinalIgnoreCase)
        {
            ["default"] = HumanBehaviorProfileOptions.CreateDefault(),
            ["cautious"] = HumanBehaviorProfileOptions.CreateCautious(),
            ["aggressive"] = HumanBehaviorProfileOptions.CreateAggressive()
        };
}

/// <summary>
/// 中文：单个行为模板的参数配置。
/// English: Parameter set describing a single behavior profile.
/// </summary>
public sealed class HumanBehaviorProfileOptions
{
    public DelayRangeOptions PreActionDelay { get; set; } = new(250, 600);
    public DelayRangeOptions PostActionDelay { get; set; } = new(220, 520);
    public DelayRangeOptions TypingInterval { get; set; } = new(80, 200);
    public DelayRangeOptions ScrollDelay { get; set; } = new(260, 720);
    public int MaxScrollSegments { get; set; } = 2;
    public double HesitationProbability { get; set; } = 0.12;

    public static HumanBehaviorProfileOptions CreateDefault() => new();

    public static HumanBehaviorProfileOptions CreateCautious() => new()
    {
        PreActionDelay = new DelayRangeOptions(420, 820),
        PostActionDelay = new DelayRangeOptions(360, 780),
        TypingInterval = new DelayRangeOptions(120, 260),
        ScrollDelay = new DelayRangeOptions(320, 880),
        MaxScrollSegments = 3,
        HesitationProbability = 0.22
    };

    public static HumanBehaviorProfileOptions CreateAggressive() => new()
    {
        PreActionDelay = new DelayRangeOptions(120, 280),
        PostActionDelay = new DelayRangeOptions(140, 320),
        TypingInterval = new DelayRangeOptions(60, 140),
        ScrollDelay = new DelayRangeOptions(180, 420),
        MaxScrollSegments = 1,
        HesitationProbability = 0.05
    };
}

/// <summary>
/// 中文：延迟区间配置，单位毫秒。
/// English: Delay range configuration expressed in milliseconds.
/// </summary>
public sealed class DelayRangeOptions
{
    public DelayRangeOptions()
    {
    }

    public DelayRangeOptions(int minMs, int maxMs)
    {
        MinMs = minMs;
        MaxMs = maxMs;
    }

    public int MinMs { get; set; } = 200;

    public int MaxMs { get; set; } = 600;

    public (int Min, int Max) Normalize()
    {
        var min = Math.Max(0, MinMs);
        var max = Math.Max(min + 1, MaxMs);
        return (min, max);
    }
}
