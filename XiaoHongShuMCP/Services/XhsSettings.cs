namespace XiaoHongShuMCP.Services;

/// <summary>
/// 统一根配置（XHS）
/// - 设计目标：仅注册一个配置类 <see cref="XhsSettings"/>，集中承载所有子配置；
/// - 绑定路径：根节 <c>XHS</c>；环境变量使用双下划线映射冒号（例如：XHS__McpSettings__WaitTimeoutMs）。
/// - 约束：保持各子配置的语义与原有字段一致，删除分散的顶层配置类文件（破坏性变更，不向后兼容）。
/// </summary>
public class XhsSettings
{
    /// <summary>Serilog 日志相关设置（仅用于结构化展示；日志读取仍直接从 IConfiguration）</summary>
    public SerilogSection Serilog { get; set; } = new();

    /// <summary>通用 API 监听器开关等</summary>
    public UniversalApiMonitorSection UniversalApiMonitor { get; set; } = new();

    /// <summary>浏览器连接与超时设置</summary>
    public BrowserSettingsSection BrowserSettings { get; set; } = new();

    /// <summary>MCP 统一等待/批量参数</summary>
    public McpSettingsSection McpSettings { get; set; } = new();

    /// <summary>页面加载等待策略参数</summary>
    public PageLoadWaitSection PageLoadWaitConfig { get; set; } = new();

    /// <summary>搜索相关超时</summary>
    public SearchTimeoutsSection SearchTimeoutsConfig { get; set; } = new();

    /// <summary>端点等待与重试</summary>
    public EndpointRetrySection EndpointRetry { get; set; } = new();

    /// <summary>交互缓存设置</summary>
    public InteractionCacheSection InteractionCache { get; set; } = new();

    /// <summary>详情匹配权重/阈值/拼音</summary>
    public DetailMatchSection DetailMatchConfig { get; set; } = new();

    /// <summary>日志节</summary>
    public class SerilogSection
    {
        /// <summary>日志目录（相对项目根或绝对路径）</summary>
        public string LogDirectory { get; set; } = "logs";
        /// <summary>日志文件名模板</summary>
        public string FileNameTemplate { get; set; } = "xiaohongshu-mcp-.txt";
        /// <summary>最小日志级别（Information/Debug/Warning/Error）</summary>
        public string MinimumLevel { get; set; } = "Information";
    }

    /// <summary>通用 API 监听器附加设置</summary>
    public class UniversalApiMonitorSection
    {
        /// <summary>是否启用详细日志</summary>
        public bool EnableDetailedLogging { get; set; } = true;
    }

    /// <summary>浏览器连接与超时设置</summary>
    public class BrowserSettingsSection
    {
        /// <summary>是否以无头模式运行</summary>
        public bool Headless { get; set; } = false;
        /// <summary>远程调试端口</summary>
        public int RemoteDebuggingPort { get; set; } = 9222;
        /// <summary>连接超时时间（秒）</summary>
        public int ConnectionTimeoutSeconds { get; set; } = 30;
    }

    /// <summary>
    /// MCP 运行时配置（统一等待超时、批处理等）。
    /// - 配置键：XHS:McpSettings:WaitTimeoutMs
    /// - 环境变量：XHS__McpSettings__WaitTimeoutMs
    /// </summary>
    public class McpSettingsSection
    {
        /// <summary>长耗时操作的等待时长（毫秒，默认 600000）。</summary>
        public int WaitTimeoutMs { get; set; } = 600_000;
        /// <summary>是否启用进度上报</summary>
        public bool EnableProgressReporting { get; set; } = true;
        /// <summary>批次大小上限</summary>
        public int MaxBatchSize { get; set; } = 10;
        /// <summary>操作之间的延时（毫秒）</summary>
        public int DelayBetweenOperations { get; set; } = 1000;
    }

    /// <summary>
    /// 页面加载等待配置（从原 PageLoadWaitConfig 收敛而来）。
    /// </summary>
    public class PageLoadWaitSection
    {
        public int DOMContentLoadedTimeout { get; set; } = 15000;
        public int LoadTimeout { get; set; } = 30000;
        public int NetworkIdleTimeout { get; set; } = 60000;
        public int MaxRetries { get; set; } = 3;
        public int RetryDelayMs { get; set; } = 2000;
        public bool EnableDegradation { get; set; } = true;
        public int FastModeTimeout { get; set; } = 10000;
        public int CustomValidationTimeout { get; set; } = 5000;
    }

    /// <summary>
    /// 搜索相关超时（从原 SearchTimeoutsConfig 收敛而来）。
    /// </summary>
    public class SearchTimeoutsSection
    {
        public int UiWaitMs { get; set; } = 12000;
        public int ApiCollectionMaxWaitMs { get; set; } = 60000;
    }

    /// <summary>
    /// 端点等待与重试（从原 EndpointRetryConfig 收敛而来）。
    /// </summary>
    public class EndpointRetrySection
    {
        public int AttemptTimeoutMs { get; set; } = 120_000;
        public int MaxRetries { get; set; } = 3;
    }

    /// <summary>
    /// 交互缓存配置（从原 InteractionCacheConfig 收敛而来）。
    /// </summary>
    public class InteractionCacheSection
    {
        /// <summary>缓存有效期（分钟，默认 3 分钟；最大 1440）。</summary>
        public int TtlMinutes { get; set; } = 3;
    }

    /// <summary>
    /// 详情匹配配置（从原 DetailMatchConfig 收敛而来）。
    /// </summary>
    public class DetailMatchSection
    {
        public double WeightedThreshold { get; set; } = 0.5;
        public int TitleWeight { get; set; } = 4;
        public int AuthorWeight { get; set; } = 3;
        public int ContentWeight { get; set; } = 2;
        public int HashtagWeight { get; set; } = 2;
        public int ImageAltWeight { get; set; } = 1;
        public bool UseFuzzy { get; set; } = true;
        public int MaxDistanceCap { get; set; } = 3;
        public double TokenCoverageThreshold { get; set; } = 0.7;
        public bool IgnoreSpaces { get; set; } = true;
        public bool UsePinyin { get; set; } = true;
        public bool PinyinInitialsOnly { get; set; } = true;
    }
}
