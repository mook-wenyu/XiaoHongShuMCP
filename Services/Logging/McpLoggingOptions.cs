using System;
using Microsoft.Extensions.Logging;

namespace HushOps.Servers.XiaoHongShu.Services.Logging;

/// <summary>
/// 中文：控制 MCP 日志桥接的缓冲策略与结构化输出。
/// </summary>
public sealed class McpLoggingOptions
{
    public const int DefaultQueueCapacity = 512;

    public int QueueCapacity { get; set; } = DefaultQueueCapacity;

    public TimeSpan FlushInterval { get; set; } = TimeSpan.FromMilliseconds(25);

    public LogLevel LocalFallbackLevel { get; set; } = LogLevel.Information;

    public bool IncludeScopes { get; set; } = true;

    public bool IncludeExceptionDetails { get; set; } = true;
}
