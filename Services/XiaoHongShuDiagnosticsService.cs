using System;
using HushOps.Servers.XiaoHongShu.Configuration;
using Microsoft.Extensions.Logging;

namespace HushOps.Servers.XiaoHongShu.Services;

/// <summary>
/// 中文：负责在启动时输出配置摘要，帮助审计与调试。
/// </summary>
public sealed class XiaoHongShuDiagnosticsService
{
    private readonly ILogger<XiaoHongShuDiagnosticsService> _logger;

    public XiaoHongShuDiagnosticsService(ILogger<XiaoHongShuDiagnosticsService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void LogConfiguration(XiaoHongShuOptions options)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        _logger.LogInformation("XHS 服务器配置 {@Payload}", new
        {
            @event = "xhs_server_configuration",
            accountCount = options.Accounts.Count,
            options.Humanized.MinDelayMs,
            options.Humanized.MaxDelayMs,
            options.Humanized.Jitter
        });
    }
}
