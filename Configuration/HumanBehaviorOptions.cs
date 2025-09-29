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
    public PixelRangeOptions ClickJitter { get; set; } = new(1, 4);
    public double EdgeClickProbability { get; set; } = 0.1;
    public IntRangeOptions MouseMoveSteps { get; set; } = new(12, 28);
    public DoubleRangeOptions MouseVelocity { get; set; } = new(280, 820);
    public DoubleRangeOptions ScrollDelta { get; set; } = new(320, 720);
    public DelayRangeOptions IdlePause { get; set; } = new(220, 680);
    public double ErrorCorrectionProbability { get; set; } = 0.08;
    public DelayRangeOptions ErrorCorrectionDelay { get; set; } = new(150, 320);
    public DelayRangeOptions HotkeyInterval { get; set; } = new(90, 180);
    public DoubleRangeOptions WheelDelta { get; set; } = new(260, 540);
    public double ReverseScrollProbability { get; set; } = 0.08;
    public double RandomMoveProbability { get; set; } = 0.08;
    public bool UseCurvedPaths { get; set; } = true;
    public DelayRangeOptions RandomIdleDuration { get; set; } = new(420, 960);
    public double RandomIdleProbability { get; set; } = 0.1;
    public string? RandomSeed { get; set; }
        = null;
    public int ViewportTolerancePx { get; set; } = 2;
    public bool RequireProxy { get; set; } = false;
    public string[] AllowedProxyPrefixes { get; set; } = Array.Empty<string>();
    public bool RequireGpuInfo { get; set; } = false;
    public bool AllowAutomationIndicators { get; set; } = false;

    public static HumanBehaviorProfileOptions CreateDefault() => new();

    public static HumanBehaviorProfileOptions CreateCautious() => new()
    {
        PreActionDelay = new DelayRangeOptions(420, 820),
        PostActionDelay = new DelayRangeOptions(360, 780),
        TypingInterval = new DelayRangeOptions(120, 260),
        ScrollDelay = new DelayRangeOptions(320, 880),
        MaxScrollSegments = 3,
        HesitationProbability = 0.22,
        ClickJitter = new PixelRangeOptions(2, 6),
        EdgeClickProbability = 0.18,
        MouseMoveSteps = new IntRangeOptions(18, 36),
        MouseVelocity = new DoubleRangeOptions(220, 640),
        ScrollDelta = new DoubleRangeOptions(240, 560),
        IdlePause = new DelayRangeOptions(380, 960),
        ErrorCorrectionProbability = 0.18,
        ErrorCorrectionDelay = new DelayRangeOptions(220, 420),
        HotkeyInterval = new DelayRangeOptions(140, 260),
        WheelDelta = new DoubleRangeOptions(220, 460),
        ReverseScrollProbability = 0.16,
        RandomMoveProbability = 0.18,
        UseCurvedPaths = true,
        RandomIdleDuration = new DelayRangeOptions(620, 1320),
        RandomIdleProbability = 0.2
    };

    public static HumanBehaviorProfileOptions CreateAggressive() => new()
    {
        PreActionDelay = new DelayRangeOptions(120, 280),
        PostActionDelay = new DelayRangeOptions(140, 320),
        TypingInterval = new DelayRangeOptions(60, 140),
        ScrollDelay = new DelayRangeOptions(180, 420),
        MaxScrollSegments = 1,
        HesitationProbability = 0.05,
        ClickJitter = new PixelRangeOptions(0, 2),
        EdgeClickProbability = 0.05,
        MouseMoveSteps = new IntRangeOptions(8, 18),
        MouseVelocity = new DoubleRangeOptions(420, 1020),
        ScrollDelta = new DoubleRangeOptions(360, 880),
        IdlePause = new DelayRangeOptions(120, 360),
        ErrorCorrectionProbability = 0.02,
        ErrorCorrectionDelay = new DelayRangeOptions(80, 180),
        HotkeyInterval = new DelayRangeOptions(70, 150),
        WheelDelta = new DoubleRangeOptions(320, 640),
        ReverseScrollProbability = 0.04,
        RandomMoveProbability = 0.05,
        UseCurvedPaths = true,
        RandomIdleDuration = new DelayRangeOptions(240, 520),
        RandomIdleProbability = 0.05
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

/// <summary>
/// 中文：整数区间配置。
/// English: Integer range configuration.
/// </summary>
public sealed class IntRangeOptions
{
    public IntRangeOptions()
    {
    }

    public IntRangeOptions(int min, int max)
    {
        Min = min;
        Max = max;
    }

    public int Min { get; set; } = 1;

    public int Max { get; set; } = 10;

    public (int Min, int Max) Normalize()
    {
        var min = Math.Max(0, Min);
        var max = Math.Max(min + 1, Max);
        return (min, max);
    }
}

/// <summary>
/// 中文：像素偏移区间配置。
/// English: Pixel offset range configuration.
/// </summary>
public sealed class PixelRangeOptions
{
    public PixelRangeOptions()
    {
    }

    public PixelRangeOptions(int minPx, int maxPx)
    {
        MinPx = minPx;
        MaxPx = maxPx;
    }

    public int MinPx { get; set; } = 0;

    public int MaxPx { get; set; } = 4;

    public (int Min, int Max) Normalize()
    {
        var min = Math.Max(0, MinPx);
        var max = Math.Max(min, MaxPx);
        return (min, max);
    }
}

/// <summary>
/// 中文：浮点区间配置（如速度、距离）。
/// English: Floating point range configuration (e.g., speed, distance).
/// </summary>
public sealed class DoubleRangeOptions
{
    public DoubleRangeOptions()
    {
    }

    public DoubleRangeOptions(double min, double max)
    {
        Min = min;
        Max = max;
    }

    public double Min { get; set; } = 0d;

    public double Max { get; set; } = 1d;

    public (double Min, double Max) Normalize()
    {
        var min = Math.Max(0d, Min);
        var max = Math.Max(min, Max);
        return (min, max);
    }
}
