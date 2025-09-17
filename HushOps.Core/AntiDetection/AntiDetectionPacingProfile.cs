namespace HushOps.Core.AntiDetection;

/// <summary>
/// 反检测节奏档位定义。
/// </summary>
public enum AntiDetectionPacingProfile
{
    /// <summary>激进模式：更快的交互节奏，仅在完全健康时启用。</summary>
    Aggressive = 0,

    /// <summary>标准模式：默认节奏，适用于大多数运行场景。</summary>
    Normal = 1,

    /// <summary>保守模式：降低交互频率、开启更多防护。</summary>
    Conservative = 2,

    /// <summary>暂停模式：需要人工介入时停机。</summary>
    Paused = 3
}
