using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using HushOps.Core.Core.Selectors;
using HushOps.Core.Observability;
using HushOps.Core.Selectors;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace XiaoHongShuMCP.Services;

/// <summary>
/// 启动期应用选择器注册表中的最新计划，保持工作流选择器一致性。
/// </summary>
internal sealed class SelectorPlanHostedService : IHostedService
{
    private readonly IConfiguration configuration;
    private readonly ISelectorRegistry registry;
    private readonly IWeakSelectorGovernor governor;
    private readonly ILogger<SelectorPlanHostedService>? logger;
    private readonly IMetrics? metrics;

    public SelectorPlanHostedService(
        IConfiguration configuration,
        ISelectorRegistry registry,
        IWeakSelectorGovernor governor,
        ILogger<SelectorPlanHostedService>? logger = null,
        IMetrics? metrics = null)
    {
        this.configuration = configuration;
        this.registry = registry;
        this.governor = governor;
        this.logger = logger;
        this.metrics = metrics;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var applyFlag = configuration["XHS:Selectors:ApplyOnStartup"];
            if (string.Equals(applyFlag, "false", StringComparison.OrdinalIgnoreCase))
            {
                logger?.LogInformation("[Selectors] 启动时未应用选择器计划：ApplyOnStartup=false");
                return;
            }

            var workflow = configuration["XHS:Selectors:Workflow"] ?? string.Empty;
            var dryRun = string.Equals(configuration["XHS:Selectors:DryRun"], "true", StringComparison.OrdinalIgnoreCase);
            var plan = await registry.BuildPlanAsync(workflow, cancellationToken).ConfigureAwait(false);
            if (plan.Items.Count == 0)
            {
                logger?.LogWarning("[Selectors] 未找到任何选择器计划，workflow={Workflow}", workflow);
                return;
            }

            RecordPlanMetrics(plan, dryRun);

            if (dryRun)
            {
                logger?.LogInformation("[Selectors] DryRun 模式应用计划，workflow={Workflow}，项目数={Count}", workflow, plan.Items.Count);
                return;
            }

            var ok = governor.ApplyPlan(plan);
            logger?.LogInformation("[Selectors] 已加载选择器计划，workflow={Workflow}，项目数={Count}，结果={Result}", workflow, plan.Items.Count, ok);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "[Selectors] 应用选择器计划失败，将在运行期依赖实时调节");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private void RecordPlanMetrics(WeakSelectorPlan plan, bool dryRun)
    {
        if (metrics is null)
        {
            return;
        }

        var mode = dryRun ? "dryrun" : "apply";
        var items = plan.Items.Count;
        var firstChanges = plan.Items.Count(i => i.Before.Count > 0 && i.After.Count > 0 && !string.Equals(i.Before[0], i.After[0], StringComparison.Ordinal));
        var demotions = plan.Items.Sum(i => i.DemotedSelectors?.Count ?? 0);
        try
        {
            metrics.CreateCounter("selectors_plan_items_total", "计划项目数量").Add(items, LabelSet.From(("mode", mode)));
            metrics.CreateCounter("selectors_plan_first_changes_total", "首项变化数量").Add(firstChanges, LabelSet.From(("mode", mode)));
            metrics.CreateCounter("selectors_plan_demotions_total", "降权选择器总数").Add(demotions, LabelSet.From(("mode", mode)));
        }
        catch (Exception ex)
        {
            logger?.LogDebug(ex, "[Selectors] 记录指标失败");
        }
    }
}
