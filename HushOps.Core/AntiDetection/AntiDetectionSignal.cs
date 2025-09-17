namespace HushOps.Core.AntiDetection;

/// <summary>
/// 反检测监控信号，来源于网络监控、拟人化执行与工作流统计。
/// </summary>
public sealed class AntiDetectionSignal
{
    /// <summary>上下文标识（账号/会话/浏览器池），用于区分独立调度单元。</summary>
    public string ContextId { get; init; } = string.Empty;

    /// <summary>所属工作流名称（例如 Comment、Engagement、Discovery）。</summary>
    public string Workflow { get; init; } = string.Empty;

    /// <summary>信号采集时间（UTC）。</summary>
    public DateTimeOffset ObservedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>窗口内总交互次数（动作 + API 调用），用于计算异常率。</summary>
    public long TotalInteractions { get; init; }
        = 0;

    /// <summary>HTTP 429 次数。</summary>
    public long Http429 { get; init; }
        = 0;

    /// <summary>HTTP 403 次数。</summary>
    public long Http403 { get; init; }
        = 0;

    /// <summary>验证码或滑块触发次数。</summary>
    public long CaptchaChallenges { get; init; }
        = 0;

    /// <summary>拟人化 API 确认的 P95 延迟（毫秒）。</summary>
    public double P95LatencyMs { get; init; }
        = 0d;

    /// <summary>拟人化 API 确认的 P99 延迟（毫秒）。</summary>
    public double P99LatencyMs { get; init; }
        = 0d;

    /// <summary>人类相似度得分（0~1），1 越接近真实用户行为。</summary>
    public double HumanLikeScore { get; init; }
        = 1d;

    /// <summary>是否触发过 JS 应急注入兜底。</summary>
    public bool JsInjectionFallbackUsed { get; init; }
        = false;

    /// <summary>附加标记（如设备、批次、环境）。</summary>
    public IReadOnlyList<string> Tags { get; init; }
        = Array.Empty<string>();

    /// <summary>扩展指标（键值对），用于灰度或自动化分析。</summary>
    public IReadOnlyDictionary<string, double> Metrics { get; init; }
        = new Dictionary<string, double>();
}
