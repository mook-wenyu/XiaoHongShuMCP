using System;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace HushOps.Servers.XiaoHongShu.Infrastructure.Telemetry;

/// <summary>
/// 中文：遥测扩展方法，用于配置 OpenTelemetry 跟踪和指标。
/// English: Telemetry extension methods for configuring OpenTelemetry tracing and metrics.
/// </summary>
public static class TelemetryExtensions
{
    /// <summary>
    /// 中文：添加 OpenTelemetry 遥测功能（包括跟踪和指标）。
    /// English: Adds OpenTelemetry telemetry capabilities (including tracing and metrics).
    /// </summary>
    /// <param name="services">服务集合 | Service collection</param>
    /// <param name="serviceName">服务名称（可选，默认为 "HushOps.Servers.XiaoHongShu"）| Service name (optional, defaults to "HushOps.Servers.XiaoHongShu")</param>
    /// <param name="serviceVersion">服务版本（可选，默认为 "1.0.0"）| Service version (optional, defaults to "1.0.0")</param>
    /// <returns>服务集合 | Service collection</returns>
    public static IServiceCollection AddTelemetry(
        this IServiceCollection services,
        string serviceName = "HushOps.Servers.XiaoHongShu",
        string serviceVersion = "1.0.0")
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        // 中文：注册 ActivitySource 单例
        // English: Register ActivitySource singleton
        services.AddSingleton(TelemetryActivitySource.Instance);

        // 中文：配置 OpenTelemetry 跟踪
        // English: Configure OpenTelemetry tracing
        services.AddOpenTelemetry()
            .ConfigureResource(resourceBuilder =>
            {
                resourceBuilder
                    .AddService(
                        serviceName: serviceName,
                        serviceVersion: serviceVersion,
                        serviceInstanceId: Environment.MachineName)
                    .AddAttributes(new[]
                    {
                        new System.Collections.Generic.KeyValuePair<string, object>("deployment.environment", "production"),
                        new System.Collections.Generic.KeyValuePair<string, object>("host.name", Environment.MachineName)
                    });
            })
            .WithTracing(tracerProviderBuilder =>
            {
                tracerProviderBuilder
                    .AddSource(TelemetryActivitySource.SourceName)
                    .AddHttpClientInstrumentation(options =>
                    {
                        // 中文：记录 HTTP 请求详细信息
                        // English: Record HTTP request details
                        options.EnrichWithHttpRequestMessage = (activity, httpRequestMessage) =>
                        {
                            activity.SetTag("http.request.method", httpRequestMessage.Method.Method);
                            activity.SetTag("http.request.uri", httpRequestMessage.RequestUri?.ToString());
                        };
                        options.EnrichWithHttpResponseMessage = (activity, httpResponseMessage) =>
                        {
                            activity.SetTag("http.response.status_code", (int)httpResponseMessage.StatusCode);
                        };
                        options.RecordException = true;
                    })
                    .SetSampler(new TraceIdRatioBasedSampler(0.1)); // 中文：10% 采样率 | English: 10% sampling rate
            })
            .WithMetrics(meterProviderBuilder =>
            {
                meterProviderBuilder
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddMeter(TelemetryActivitySource.MeterName)
                    .AddPrometheusExporter(); // 中文：添加 Prometheus 导出器 | English: Add Prometheus exporter
            });

        return services;
    }
}
