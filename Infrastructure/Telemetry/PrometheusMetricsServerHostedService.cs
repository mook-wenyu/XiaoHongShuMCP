using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter;

namespace HushOps.Servers.XiaoHongShu.Infrastructure.Telemetry;

/// <summary>
/// 中文：Prometheus 指标服务器托管服务，在后台启动 HTTP 监听器暴露 /metrics 端点。
/// English: Prometheus metrics server hosted service that starts an HTTP listener in the background to expose /metrics endpoint.
/// </summary>
public sealed class PrometheusMetricsServerHostedService : IHostedService
{
    private readonly ILogger<PrometheusMetricsServerHostedService> _logger;
    private WebApplication? _metricsApp;
    private readonly int _port;

    public PrometheusMetricsServerHostedService(
        ILogger<PrometheusMetricsServerHostedService> logger,
        int port = 9464)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _port = port;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            // 中文：创建独立的 WebApplication 用于暴露 /metrics 端点
            // English: Create standalone WebApplication for exposing /metrics endpoint
            var builder = WebApplication.CreateBuilder();

            // 中文：配置 Kestrel 监听指定端口
            // English: Configure Kestrel to listen on specified port
            builder.WebHost.UseUrls($"http://localhost:{_port}");

            // 中文：禁用不必要的日志以减少噪音
            // English: Disable unnecessary logging to reduce noise
            builder.Logging.ClearProviders();
            builder.Logging.AddSimpleConsole(options =>
            {
                options.SingleLine = true;
                options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffK ";
            });
            builder.Logging.SetMinimumLevel(LogLevel.Warning);

            _metricsApp = builder.Build();

            // 中文：映射 Prometheus scraping 端点
            // English: Map Prometheus scraping endpoint
            _metricsApp.MapPrometheusScrapingEndpoint();

            // 中文：在后台线程启动 metrics server
            // English: Start metrics server in background thread
            _ = Task.Run(async () =>
            {
                try
                {
                    _logger.LogInformation(
                        "Prometheus 指标服务器已启动 | Prometheus metrics server started: http://localhost:{Port}/metrics",
                        _port);

                    await _metricsApp.RunAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex,
                        "Prometheus 指标服务器异常 | Prometheus metrics server error");
                }
            }, cancellationToken);

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "启动 Prometheus 指标服务器失败 | Failed to start Prometheus metrics server");
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_metricsApp != null)
        {
            _logger.LogInformation(
                "正在停止 Prometheus 指标服务器 | Stopping Prometheus metrics server");

            await _metricsApp.StopAsync(cancellationToken).ConfigureAwait(false);
            await _metricsApp.DisposeAsync().ConfigureAwait(false);

            _logger.LogInformation(
                "Prometheus 指标服务器已停止 | Prometheus metrics server stopped");
        }
    }
}
