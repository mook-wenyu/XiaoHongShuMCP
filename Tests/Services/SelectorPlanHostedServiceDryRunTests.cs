using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using HushOps.Core.Core.Selectors;
using HushOps.Core.Selectors;
using HushOps.Core.Observability;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using XiaoHongShuMCP.Services;

namespace Tests.Services;

/// <summary>
/// SelectorPlanHostedService 的 DryRun 与指标测试。
/// </summary>
public class SelectorPlanHostedServiceDryRunTests
{
    private sealed class CaptureMetrics : IMetrics
    {
        public long Items;
        public ICounter CreateCounter(string name, string? description = null) => new CounterImpl(this, name);
        public IHistogram CreateHistogram(string name, string? description = null) => new HistogramImpl();

        private sealed class CounterImpl : ICounter
        {
            private readonly CaptureMetrics metrics;
            private readonly string name;
            public CounterImpl(CaptureMetrics metrics, string name) { this.metrics = metrics; this.name = name; }
            public void Add(long value, in LabelSet labels)
            {
                if (name == "selectors_plan_items_total")
                {
                    metrics.Items += value;
                }
            }
        }

        private sealed class HistogramImpl : IHistogram
        {
            public void Record(double value, in LabelSet labels) { }
        }
    }

    private sealed class SpyGovernor : IWeakSelectorGovernor
    {
        public bool Applied { get; private set; }
        public HushOps.Core.Selectors.WeakSelectorPlan BuildPlan(double s, long m) => throw new NotSupportedException();
        public bool ApplyPlan(HushOps.Core.Selectors.WeakSelectorPlan plan) { Applied = true; return true; }
    }

    private sealed class StubSelectorRegistry : ISelectorRegistry
    {
        private readonly WeakSelectorPlan plan;
        public StubSelectorRegistry(WeakSelectorPlan plan) { this.plan = plan; }
        public Task<SelectorRegistryItem> PublishAsync(SelectorRevision revision, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<SelectorRegistryItem> RollbackAsync(string alias, string version, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<SelectorRegistrySnapshot> GetSnapshotAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<SelectorRegistryItem?> GetActiveAsync(string alias, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<WeakSelectorPlan> BuildPlanAsync(string workflow, CancellationToken ct = default) => Task.FromResult(plan);
    }

    [Test]
    public async Task DryRun_Should_Record_Metrics_And_Not_Apply()
    {
        var selectorPlan = new WeakSelectorPlan(new[]
        {
            new WeakSelectorPlanItem("LoginButton", new[] { "#login-btn", ".login-btn" }, new[] { ".login-btn", "#login-btn" }, new[] { "#login-btn" })
        });

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["XHS:Selectors:ApplyOnStartup"] = "true",
                ["XHS:Selectors:DryRun"] = "true",
                ["XHS:Selectors:Workflow"] = ""
            })
            .Build();

        var gov = new SpyGovernor();
        var metrics = new CaptureMetrics();
        var registry = new StubSelectorRegistry(selectorPlan);
        var svc = new SelectorPlanHostedService(config, registry, gov, NullLogger<SelectorPlanHostedService>.Instance, metrics);

        await svc.StartAsync(CancellationToken.None);

        Assert.That(gov.Applied, Is.False);
        Assert.That(metrics.Items, Is.EqualTo(1));
    }
}
