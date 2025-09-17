using System;
using System.Collections.Generic;
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
    /// <summary>审计设置（.audit 写盘控制）</summary>
    public AuditSection Audit { get; set; } = new();

    /// <summary>详情匹配权重/阈值/拼音</summary>
    public DetailMatchSection DetailMatchConfig { get; set; } = new();

    /// <summary>并发/速率/熔断综合治理配置（新）</summary>
    public ConcurrencySection Concurrency { get; set; } = new();

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
        /// <summary>每处理 N 条响应输出一次网络指标日志（0 表示不输出，默认 200）。</summary>
        public int MetricsLogEveryResponses { get; set; } = 200;
    }

    /// <summary>浏览器连接与超时设置</summary>
    public class BrowserSettingsSection
    {
        /// <summary>是否以无头模式运行（Playwright 管理）。</summary>
        public bool Headless { get; set; } = false;
        /// <summary>浏览器可执行文件路径（可选）。为空则使用 Playwright 自带 Chromium 或通过 Channel 指定。</summary>
        public string? ExecutablePath { get; set; }
        /// <summary>浏览器用户数据目录（推荐设置，默认项目根 profiles/xhs-automation）。</summary>
        public string? UserDataDir { get; set; }
        /// <summary>首选浏览器 Channel（如 chrome/msedge/chromium）。为空使用 Playwright 默认。</summary>
        public string? Channel { get; set; }

        /// <summary>连接与健康检查策略。</summary>
        public ConnectionSection Connection { get; set; } = new();

        public class ConnectionSection
        {
            /// <summary>服务启动后首次尝试连接前的延迟（秒）。</summary>
            public int InitialDelaySeconds { get; set; } = 5;

            /// <summary>初始失败重试间隔（秒），随后指数退避直至上限。</summary>
            public int RetryIntervalSeconds { get; set; } = 20;

            /// <summary>重试间隔上限（秒），避免退避过长。</summary>
            public int RetryIntervalMaxSeconds { get; set; } = 180;

            /// <summary>健康检查间隔（秒），用于定期校验登录态并视情况重连。</summary>
            public int HealthCheckIntervalSeconds { get; set; } = 300;

            /// <summary>Cookie 续期阈值（分钟），距过期不足该阈值时触发续期；0 表示禁用。</summary>
            public int CookieRenewalThresholdMinutes { get; set; } = 15;

            /// <summary>上下文轮换周期（分钟），超过后在空闲时重建浏览器上下文；0 表示禁用。</summary>
            public int ContextRecycleMinutes { get; set; } = 180;
        }
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

        /// <summary>
        /// 工具白名单：若列表不为空，仅暴露列出的工具（可用工具名或方法名）。
        /// </summary>
        public List<string> EnabledToolNames { get; set; } = [];

        /// <summary>
        /// 工具黑名单：列出的工具将从经纪列表中移除。
        /// </summary>
        public List<string> DisabledToolNames { get; set; } = [];

        /// <summary>
        /// 工具标题覆盖表（键为工具名或方法名，值为展示标题）。
        /// </summary>
        public Dictionary<string, string>? ToolTitleOverrides { get; set; }
            = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 工具描述覆盖表（键为工具名或方法名，值为中文说明）。
        /// </summary>
        public Dictionary<string, string>? ToolDescriptionOverrides { get; set; }
            = new(StringComparer.OrdinalIgnoreCase);
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
    /// 审计设置
    /// </summary>
    public class AuditSection
    {
        /// <summary>是否启用审计写盘（默认 true）</summary>
        public bool Enabled { get; set; } = true;
        /// <summary>写盘目录，默认 .audit （相对项目根）</summary>
        public string Directory { get; set; } = ".audit";
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

    /// <summary>
    /// 并发/速率/熔断综合治理配置
    /// - PerAccountReadConcurrency：每账户只读操作并发预算（默认2）
    /// - PerAccountWriteConcurrency：每账户写操作并发预算（默认1）
    /// - Rate：令牌桶速率配置（按端点类别）
    /// - Breaker：熔断配置
    /// </summary>
    public class ConcurrencySection
    {
        public int PerAccountReadConcurrency { get; set; } = 2;
        public int PerAccountWriteConcurrency { get; set; } = 1;

        public RateSection Rate { get; set; } = new();
        public BreakerSection Breaker { get; set; } = new();
        public PoolSection Pool { get; set; } = new();

        public class RateSection
        {
            // 写操作端点默认更低速率（更保守）
            public double LikeCapacity { get; set; } = 1;              // 允许一次突发
            public double LikeRefillPerSecond { get; set; } = 1.0 / 20; // 平均1次/20s

            public double CollectCapacity { get; set; } = 1;
            public double CollectRefillPerSecond { get; set; } = 1.0 / 30; // 1次/30s

            public double CommentCapacity { get; set; } = 1;
            public double CommentRefillPerSecond { get; set; } = 1.0 / 300; // 1次/5min

            // 只读端点可更宽松
            public double SearchCapacity { get; set; } = 2;
            public double SearchRefillPerSecond { get; set; } = 0.5; // 2次/秒容量，平均1次/2s

            public double FeedCapacity { get; set; } = 2;
            public double FeedRefillPerSecond { get; set; } = 0.5;
        }

        public class BreakerSection
        {
            /// <summary>滑动窗口内失败阈值（达到即熔断）。</summary>
            public int FailureThreshold { get; set; } = 3;
            /// <summary>滑动窗口时长（秒）。</summary>
            public int WindowSeconds { get; set; } = 120;
            /// <summary>熔断打开持续时长（秒）。</summary>
            public int OpenSeconds { get; set; } = 600; // 10 分钟
        }

        /// <summary>
        /// 页面池化配置
        /// </summary>
        public class PoolSection
        {
            /// <summary>池内页面最大数量（并发租约上限）。</summary>
            public int MaxPages { get; set; } = 3;
        }
    }

    /// <summary>
    /// 反检测配置（占位版，后续迁移到 Ops:AntiDetection）。
    /// </summary>
    public class AntiDetectionSection
    {
        /// <summary>是否启用反检测脚本注入。</summary>
        public bool Enabled { get; set; } = true;
        /// <summary>是否隐藏 navigator.webdriver。</summary>
        public bool PatchNavigatorWebdriver { get; set; } = true;
        /// <summary>是否启用 WebGL 指纹占位补丁（隐藏厂商/渲染器）。</summary>
        public bool PatchWebGL { get; set; } = true;
        /// <summary>是否启用 Canvas 占位补丁（保留接口一致性）。</summary>
        public bool PatchCanvas { get; set; } = false;
        /// <summary>是否写入审计快照。</summary>
        public bool AuditEnabled { get; set; } = true;
        /// <summary>审计输出目录。</summary>
        public string AuditDirectory { get; set; } = ".audit";
    }

    /// <summary>反检测配置根节。</summary>
    public AntiDetectionSection AntiDetection { get; set; } = new();

    /// <summary>
    /// 交互策略（禁注入门控等）— 破坏性新增，不向后兼容：默认严格禁用注入兜底。
    /// 配置键：XHS:InteractionPolicy:*
    /// </summary>
    public InteractionPolicySection InteractionPolicy { get; set; } = new();

    /// <summary>
    /// 人格化与自调优参数（节律倍率、半衰期等）。
    /// 配置键：XHS:Persona:*
    /// </summary>
    public PersonaSection Persona { get; set; } = new();

    public class InteractionPolicySection
    {
        /// <summary>
        /// 是否允许在极端兜底情况下使用 JS 注入（如 dispatchEvent）。默认 false。
        /// </summary>
        public bool EnableJsInjectionFallback { get; set; } = false;

        /// <summary>
        /// 是否允许滚动使用 JS 注入（window.scrollBy）。默认 false。
        /// </summary>
        public bool EnableJsScrollInjection { get; set; } = false;

        /// <summary>
        /// 是否允许“只读 Evaluate”（读取 location/outerHTML/属性等），默认 true。
        /// 说明：为实现“只读 Evaluate 清零”的过渡策略，保留该开关并对每次使用进行计量；
        /// 后续阶段可将默认改为 false 并逐步删除残余路径。
        /// </summary>
        public bool EnableJsReadEval { get; set; } = true;
    }

    /// <summary>
    /// 人格化与自调优参数。
    /// </summary>
    public class PersonaSection
    {
        /// <summary>当出现 HTTP 429 时的基础倍率（随后指数衰减回 1.0）。</summary>
        public double Http429BaseMultiplier { get; set; } = 2.5;
        /// <summary>当出现 HTTP 403 时的基础倍率（随后指数衰减回 1.0）。</summary>
        public double Http403BaseMultiplier { get; set; } = 2.0;
        /// <summary>倍率最大值上限（避免过度放大）。</summary>
        public double MaxDelayMultiplier { get; set; } = 3.0;
        /// <summary>倍率衰减半衰期（秒）。</summary>
        public int DegradeHalfLifeSeconds { get; set; } = 60;
    }
}











