namespace XiaoHongShuMCP.Services;

/// <summary>
/// 统一的端点等待重试配置
/// - AttemptTimeoutMs: 单次等待命中端点的超时（毫秒），默认 120000（2 分钟）
/// - MaxRetries: 超时后的最大重试次数（不含首次尝试），默认 3 次
/// </summary>
public class EndpointRetryConfig
{
    public int AttemptTimeoutMs { get; set; } = 120_000;
    public int MaxRetries { get; set; } = 3;
}

