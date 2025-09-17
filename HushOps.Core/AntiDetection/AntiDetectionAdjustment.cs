namespace HushOps.Core.AntiDetection;

/// <summary>
/// 反检测调优决策，用于驱动节奏、指纹与 Cookie 管理。
/// </summary>
public sealed class AntiDetectionAdjustment
{
    /// <summary>调优针对的上下文标识。</summary>
    public string ContextId { get; init; } = string.Empty;

    /// <summary>关联工作流名称。</summary>
    public string Workflow { get; init; } = string.Empty;

    /// <summary>决策生成时间（UTC）。</summary>
    public DateTimeOffset IssuedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>节奏档位。</summary>
    public AntiDetectionPacingProfile PacingProfile { get; init; }
        = AntiDetectionPacingProfile.Normal;

    /// <summary>是否强制轮换浏览器指纹。</summary>
    public bool RotateFingerprint { get; init; }
        = false;

    /// <summary>是否强制轮换 Cookie 或清理会话。</summary>
    public bool RefreshCookies { get; init; }
        = false;

    /// <summary>是否需要暂停交互并等待人工复核。</summary>
    public bool PauseInteractions { get; init; }
        = false;

    /// <summary>是否启用 navigator.webdriver 补丁。</summary>
    public bool EnableNavigatorPatch { get; init; }
        = false;

    /// <summary>是否启用 UA/语言清洗。</summary>
    public bool EnableUaLanguageScrub { get; init; }
        = false;

    /// <summary>信心评分（0~1），越高表示更确定。</summary>
    public double Confidence { get; init; }
        = 1.0;

    /// <summary>生成该决策的主要原因与依据。</summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>附加标记，方便审计与灰度过滤。</summary>
    public IReadOnlyList<string> Tags { get; init; }
        = Array.Empty<string>();
}
