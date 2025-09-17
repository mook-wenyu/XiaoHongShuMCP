namespace HushOps.Core.AntiDetection;

/// <summary>
/// 反检测调优器可配置参数。
/// </summary>
public sealed class AntiDetectionOrchestratorOptions
{
    /// <summary>信号滑动窗口最大条目。</summary>
    public int SlidingWindow { get; set; } = 24;

    /// <summary>连续保存的历史决策数量。</summary>
    public int HistoryDepth { get; set; } = 12;

    /// <summary>HTTP 429 命中率触发保守策略的阈值（0~1）。</summary>
    public double Http429High { get; set; } = 0.05;

    /// <summary>HTTP 403 命中率触发保守策略的阈值（0~1）。</summary>
    public double Http403High { get; set; } = 0.03;

    /// <summary>验证码触发率阈值（0~1）。</summary>
    public double CaptchaHigh { get; set; } = 0.02;

    /// <summary>恢复到标准节奏需要满足的 HTTP 429 命中率上限。</summary>
    public double Http429Recover { get; set; } = 0.02;

    /// <summary>恢复到标准节奏需要满足的 HTTP 403 命中率上限。</summary>
    public double Http403Recover { get; set; } = 0.01;

    /// <summary>恢复到激进节奏需要持续无异常的窗口数量。</summary>
    public int AggressiveWindowRequirement { get; set; } = 6;

    /// <summary>最小调整间隔，避免震荡。</summary>
    public TimeSpan MinimumAdjustmentInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>审计存储相对目录，默认 storage/antidetect 下。</summary>
    public string AdjustmentDirectory { get; set; } = "antidetect/adjustments";

    /// <summary>上下文状态存储相对目录，默认 storage/antidetect/state 下。</summary>
    public string StateDirectory { get; set; } = "antidetect/state";
}
