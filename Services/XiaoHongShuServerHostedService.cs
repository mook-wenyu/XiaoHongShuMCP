using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HushOps.Servers.XiaoHongShu.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HushOps.Servers.XiaoHongShu.Services;

/// <summary>
/// 中文：在服务器启动时校验配置并输出结构化日志，便于审计。
/// </summary>
public sealed class XiaoHongShuServerHostedService : IHostedService
{
    private readonly IOptions<XiaoHongShuOptions> _options;
    private readonly XiaoHongShuDiagnosticsService _diagnostics;
    private readonly ILogger<XiaoHongShuServerHostedService> _logger;

    public XiaoHongShuServerHostedService(
        IOptions<XiaoHongShuOptions> options,
        XiaoHongShuDiagnosticsService diagnostics,
        ILogger<XiaoHongShuServerHostedService> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var value = _options.Value ?? new XiaoHongShuOptions();
        if (!value.Accounts.Any())
        {
            _logger.LogWarning("XHS 服务器启动时未发现账号配置，将以只读模式运行。{@Payload}", new
            {
                @event = "xhs_server_missing_accounts"
            });
        }

        _diagnostics.LogConfiguration(value);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("XHS 服务器已停止 {@Payload}", new
        {
            @event = "xhs_server_stopped"
        });
        return Task.CompletedTask;
    }
}
