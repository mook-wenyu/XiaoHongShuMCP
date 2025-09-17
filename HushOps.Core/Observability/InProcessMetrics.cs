using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using HushOps.Core.Observability;

namespace HushOps.Observability;

/// <summary>
/// 基于系统 Meter 的本地指标实现，维持统一标签白名单并避免外部 Prometheus/Otel 依赖。
/// - 默认启用低基数标签白名单，防止意外高基数冲击。
/// - 仅在进程内聚合度量数据，配合审计 JSON 落盘即可满足可追溯需求。
/// </summary>
public sealed class InProcessMetrics : IMetrics
{
    private readonly string meterName;
    private readonly string? meterVersion;
    private readonly HashSet<string> allowedLabels;
    private readonly Meter meter;

    /// <summary>
    /// 允许的标签键名（小写）。默认：endpoint/status/hint/accounttier/region/stage/type/path/role/name/personaid/strategy。
    /// </summary>
    public static readonly string[] DefaultAllowedLabels = new[]
    {
        "endpoint", "status", "hint", "accounttier", "region",
        "stage", "type", "path", "role", "name", "personaid", "strategy"
    };

    public InProcessMetrics(string meterName = "XHS.Metrics", string? meterVersion = "1.0.0",
        IEnumerable<string>? allowedLabelKeys = null)
    {
        this.meterName = string.IsNullOrWhiteSpace(meterName) ? "XHS.Metrics" : meterName;
        this.meterVersion = meterVersion;
        allowedLabels = new HashSet<string>((allowedLabelKeys ?? DefaultAllowedLabels), StringComparer.OrdinalIgnoreCase);
        meter = new Meter(this.meterName, this.meterVersion);
    }

    public ICounter CreateCounter(string name, string? description = null)
    {
        var counter = meter.CreateCounter<long>(name, unit: null, description: description);
        return new InProcessCounter(counter, allowedLabels);
    }

    public IHistogram CreateHistogram(string name, string? description = null)
    {
        var histogram = meter.CreateHistogram<double>(name, unit: null, description: description);
        return new InProcessHistogram(histogram, allowedLabels);
    }

    private static KeyValuePair<string, object?>[] BuildTags(in LabelSet labels, HashSet<string> allowed)
    {
        var list = new List<KeyValuePair<string, object?>>();
        if (labels.Labels is { Count: > 0 })
        {
            foreach (var kvp in labels.Labels)
            {
                var key = kvp.Key?.Trim().ToLowerInvariant();
                if (string.IsNullOrEmpty(key)) continue;
                if (!allowed.Contains(key)) continue; // 白名单外直接丢弃，避免指标爆炸
                object? value = kvp.Value;
                list.Add(new KeyValuePair<string, object?>(key, value is null ? "null" : value.ToString()));
            }
        }
        return list.ToArray();
    }

    private sealed class InProcessCounter : ICounter
    {
        private readonly Counter<long> inner;
        private readonly HashSet<string> allowed;

        public InProcessCounter(Counter<long> inner, HashSet<string> allowed)
        {
            this.inner = inner;
            this.allowed = allowed;
        }

        public void Add(long value, in LabelSet labels)
        {
            var tags = BuildTags(in labels, allowed);
            inner.Add(value, tags);
        }
    }

    private sealed class InProcessHistogram : IHistogram
    {
        private readonly Histogram<double> inner;
        private readonly HashSet<string> allowed;

        public InProcessHistogram(Histogram<double> inner, HashSet<string> allowed)
        {
            this.inner = inner;
            this.allowed = allowed;
        }

        public void Record(double value, in LabelSet labels)
        {
            var tags = BuildTags(in labels, allowed);
            inner.Record(value, tags);
        }
    }
}
