namespace HushOps.Core.Config;

/// <summary>
/// 中文：系统统一配置对象。仅通过白名单环境变量（前缀 XHS__）加载。
/// </summary>
public sealed class XhsSettings
{
    /// <summary>
    /// 浏览器运行选项（最小必要项）。
    /// </summary>
    public required BrowserOptions BrowserSettings { get; init; } = new();

    /// <summary>
    /// 反检测选项（严禁默认开启 JS 注入，除非明确用于反检测兜底）。
    /// </summary>
    public required AntiDetectionOptions AntiDetection { get; init; } = new();

    /// <summary>
    /// 观测与告警阈值配置。
    /// </summary>
    public required MetricsOptions Metrics { get; init; } = new();

    /// <summary>
    /// 交互策略（拟人化倍率、是否允许只读 Evaluate 等）。
    /// </summary>
    public required InteractionPolicyOptions InteractionPolicy { get; init; } = new();

    /// <summary>
    /// MCP 清单与工具策略（运行时注入）。
    /// </summary>
    public required McpOptions Mcp { get; init; } = new();

    /// <summary>
    /// 浏览器选项。
    /// </summary>
    public sealed class BrowserOptions
    {
        public bool Headless { get; init; } = true;
        public string? UserDataDir { get; init; }
        public string Locale { get; init; } = "zh-CN";
        public string TimezoneId { get; init; } = "Asia/Shanghai";
    }

    /// <summary>
    /// 反检测选项。
    /// </summary>
    public sealed class AntiDetectionOptions
    {
        public bool Enabled { get; init; } = true;
        public string AuditDirectory { get; init; } = ".audit";
        public bool EnableJsInjectionFallback { get; init; } = false;
        public bool EnableJsReadEval { get; init; } = false;
        public bool PatchNavigatorWebdriver { get; init; } = false;
    }

    /// <summary>
    /// 指标聚合设置（InProcess）。
    /// </summary>
    public sealed class MetricsOptions
    {
        public bool Enabled { get; init; } = true;
        public string MeterName { get; init; } = "XHS.Metrics";
        public string AllowedLabelsCsv { get; init; } = string.Join(',', HushOps.Observability.InProcessMetrics.DefaultAllowedLabels);
        public double UnknownRatioThreshold { get; init; } = 0.30;
    }

    /// <summary>
    /// 交互策略选项。
    /// </summary>
    public sealed class InteractionPolicyOptions
    {
        public bool EnableJsInjectionFallback { get; init; } = false;
        public bool EnableJsReadEval { get; init; } = false;
        public bool EnableHtmlSampleAudit { get; init; } = false;
        public string[] EvalAllowedPaths { get; init; } = Array.Empty<string>();
        public double PacingMultiplier { get; init; } = 1.0;
    }

    /// <summary>
    /// MCP 配置（策略 JSON）。
    /// </summary>
    public sealed class McpOptions
    {
        /// <summary>
        /// 工具策略 JSON（键：ToolName，如 "XiaoHongShuTools.ConnectToBrowser"），值：{ idempotency, timeoutMs, limits:{category,permitsPerSecond,burst} }
        /// </summary>
        public string? PolicyJson { get; init; }
    }
}
