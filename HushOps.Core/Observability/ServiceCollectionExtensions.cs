using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using HushOps.Core.Observability;

namespace HushOps.Observability;

/// <summary>
/// DI 扩展：在不依赖 Prometheus/Otel 的前提下装配指标能力。
/// - 默认注册 InProcessMetrics，使用本地 Meter 聚合并维持标签白名单。
/// - 支持通过 XHS:Metrics:Enabled 显式关闭；关闭时退化为 NoopMetrics，确保调用方无需判空。
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 注册指标实现与适配器引导服务，保持 API 一致性同时移除外部采集依赖。
    /// </summary>
    public static IServiceCollection AddObservability(this IServiceCollection services, IConfiguration configuration)
    {
        var enabled = !string.Equals(configuration["XHS:Metrics:Enabled"], "false", StringComparison.OrdinalIgnoreCase);
        var meterName = configuration["XHS:Metrics:MeterName"] ?? "XHS.Metrics";
        var allowedLabelsCsv = configuration["XHS:Metrics:AllowedLabels"];
        var allowed = string.IsNullOrWhiteSpace(allowedLabelsCsv)
            ? InProcessMetrics.DefaultAllowedLabels
            : allowedLabelsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (!enabled)
        {
            services.AddSingleton<IMetrics, NoopMetrics>();
            services.AddHostedService<AdapterTelemetryBootstrapperHostedService>();
            return services;
        }

        services.AddSingleton<IMetrics>(_ => new InProcessMetrics(meterName, "1.0.0", allowed));
        services.AddHostedService<AdapterTelemetryBootstrapperHostedService>();
        return services;
    }
}
