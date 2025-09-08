namespace XiaoHongShuMCP.Services;

/// <summary>
/// MCP 运行时配置（中文文档注释）
/// 统一的“等待超时”配置键用于长耗时操作（如 API 监听、推荐收集等）。
/// 注意：
/// - 默认值为 10 分钟（600000ms）。
/// - 不再限制上限，若外部配置超出 10 分钟，将按配置值使用。
/// </summary>
public class McpSettings
{
    /// <summary>
    /// 长耗时操作的等待时长（毫秒）。
    /// 默认值：600000（10 分钟）。
    /// 可通过环境变量或命令行覆盖：XHS__McpSettings__WaitTimeoutMs。
    /// </summary>
    public int WaitTimeoutMs { get; set; } = 600_000;
}
