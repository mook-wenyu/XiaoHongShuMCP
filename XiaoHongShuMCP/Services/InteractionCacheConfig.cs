namespace XiaoHongShuMCP.Services;

/// <summary>
/// 临时交互缓存配置（支持环境变量/命令行覆盖）。
/// 配置键：InteractionCache:TtlMinutes（环境变量前缀 XHS__）。
/// </summary>
public class InteractionCacheConfig
{
    /// <summary>
    /// 缓存有效期（分钟）。默认 3 分钟。
    /// </summary>
    public int TtlMinutes { get; set; } = 3;
}

