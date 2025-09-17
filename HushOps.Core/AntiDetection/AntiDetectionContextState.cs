namespace HushOps.Core.AntiDetection;

/// <summary>
/// 反检测上下文运行状态，内部保存滑动窗口与历史调整记录。
/// </summary>
public sealed class AntiDetectionContextState
{
    /// <summary>上下文标识。</summary>
    public string ContextId { get; set; } = string.Empty;

    /// <summary>关联工作流。</summary>
    public string Workflow { get; set; } = string.Empty;

    /// <summary>最近窗口内累计信号。</summary>
    public List<AntiDetectionSignal> Signals { get; set; } = new();

    /// <summary>当前生效的节奏档位。</summary>
    public AntiDetectionPacingProfile CurrentPacing { get; set; }
        = AntiDetectionPacingProfile.Normal;

    /// <summary>最近一次调整决策。</summary>
    public AntiDetectionAdjustment? LastAdjustment { get; set; }
        = null;

    /// <summary>历史决策列表（保留最近若干条）。</summary>
    public List<AntiDetectionAdjustment> History { get; set; } = new();

    /// <summary>最近一次决策时间。</summary>
    public DateTimeOffset? LastAdjustmentAtUtc { get; set; }
        = null;

    /// <summary>累计人类相似度均值，用于平滑趋势。</summary>
    public double SmoothedHumanLikeScore { get; set; }
        = 1.0;
}
