using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using HushOps.Core.Automation.Abstractions;
using HushOps.Core.Observability;
using HushOps.Core.Resumable;
using ResOpResultHome = HushOps.Core.Resumable.OperationResult<XiaoHongShuMCP.Services.Resumable.HomefeedCheckpoint>;
using System.Diagnostics;

// 说明：命名空间迁移至 HushOps.Services.Resumable。
namespace XiaoHongShuMCP.Services.Resumable;

/// <summary>
/// 推荐流（Homefeed）的可恢复采集操作：监听→滚动→聚合，直至达到目标数量或尝试上限。
/// </summary>
public sealed class ResumableHomefeedOperation : IResumableOperation<HomefeedCheckpoint>
{
    private readonly ILogger logger;
    private readonly int targetMax;
    private readonly int maxAttempts;
    private readonly IBrowserManager browser;
    private readonly IPageStateGuard pageGuard;
    private readonly IHumanizedInteractionService human;
    private readonly IPageLoadWaitService pageWait;
    private readonly IUniversalApiMonitor uam;
    private readonly IMetrics? metrics;
    private readonly ILocatorPolicyStack locator;
    private readonly IHistogram? hAwaitMs;
    private readonly IHistogram? hScrollMs;
    private static readonly ActivitySource Trace = new("XHS.Traces");
    private readonly IHistogram? hLocateMs;

    public ResumableHomefeedOperation(
        ILogger<ResumableHomefeedOperation> logger,
        int targetMax,
        int maxAttempts,
        IBrowserManager browser,
        IPageStateGuard pageGuard,
        IHumanizedInteractionService human,
        IPageLoadWaitService pageWait,
        IUniversalApiMonitor uam,
        ILocatorPolicyStack locator,
        IMetrics? metrics = null)
    {
        this.logger = logger;
        this.targetMax = Math.Max(1, targetMax);
        this.maxAttempts = Math.Max(1, maxAttempts);
        this.browser = browser;
        this.pageGuard = pageGuard;
        this.human = human;
        this.pageWait = pageWait;
        this.uam = uam;
        this.metrics = metrics;
        this.locator = locator ?? throw new ArgumentNullException(nameof(locator));
        if (metrics != null)
        {
            hAwaitMs = metrics.CreateHistogram("uam_stage_await_duration_ms", "AwaitAPI 阶段耗时(ms)");
            hScrollMs = metrics.CreateHistogram("uam_stage_scroll_duration_ms", "ScrollNext 阶段耗时(ms)");
            hLocateMs = metrics.CreateHistogram("locate_stage_duration_ms", "定位阶段耗时(ms)");
        }
    }

    public async Task<ResOpResultHome> RunOrResumeAsync(OperationContext ctx)
    {
        var latest = await ctx.Repository.LoadLatestAsync(ctx.OperationId, ctx.CancellationToken);
        var last = latest == null ? HomefeedCheckpoint.CreateInitial(targetMax, maxAttempts)
                                  : CheckpointSerializer.Unpack<HomefeedCheckpoint>(latest);

        var processed = new HashSet<string>(last.ProcessedDigest);
        var attempt = last.Attempt;
        var step = last.Step;
        long seq = latest?.Seq ?? 0;

        while (true)
        {
            attempt++;
            step++;
            var auto = await browser.GetAutoPageAsync();
            var ok = await pageGuard.EnsureOnDiscoverOrSearchAsync(auto);
            if (!ok)
            {
                var fail = last with { Step = step, Attempt = attempt, Stage = "ensure", LastError = "EnsureOnDiscoverOrSearch 失败", Completed = attempt >= last.MaxAttempts };
                await ctx.Repository.SaveAsync(CheckpointSerializer.Pack(ctx.OperationId, ++seq, fail), ctx.CancellationToken);
                return new ResOpResultHome { Completed = fail.Completed, LastCheckpoint = fail, Seq = seq };
            }

            // 定位主滚动容器（可见即可，不强制）
            using var spanLocate = Trace.StartActivity("locate", ActivityKind.Internal);
            spanLocate?.SetTag("endpoint", "Homefeed");
            spanLocate?.SetTag("attempt", attempt);
            spanLocate?.SetTag("step", step);
            spanLocate?.SetTag("op_id", ctx.OperationId);
            var tLocate = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var hint = new LocatorHint { Aliases = new[] { "MainScrollContainer" } };
                var acquired = await locator.AcquireAsync(auto, hint, ctx.CancellationToken);
                if (acquired.Element != null)
                {
                    try { await acquired.Element.ScrollIntoViewIfNeededAsync(); } catch { }
                }
            }
            catch { }
            finally
            {
                var labels = LabelSet.From(("endpoint", "Homefeed"));
                tLocate.Stop();
                hLocateMs?.Record(tLocate.Elapsed.TotalMilliseconds, in labels);
            }

            // 绑定监听并滚动触发
            uam.ClearMonitoredData(ApiEndpointType.Homefeed);
            var endpoints = new HashSet<ApiEndpointType> { ApiEndpointType.Homefeed };
            var setup = uam.SetupMonitor(auto, endpoints);
            if (!setup)
            {
                var fail = last with { Step = step, Attempt = attempt, Stage = "bind", LastError = "SetupMonitor(Homefeed) 失败", Completed = attempt >= last.MaxAttempts };
                await ctx.Repository.SaveAsync(CheckpointSerializer.Pack(ctx.OperationId, ++seq, fail), ctx.CancellationToken);
                return new ResOpResultHome { Completed = fail.Completed, LastCheckpoint = fail, Seq = seq };
            }

            using var spanScroll = Trace.StartActivity("scroll", ActivityKind.Internal);
            spanScroll?.SetTag("endpoint", "Homefeed");
            spanScroll?.SetTag("attempt", attempt);
            spanScroll?.SetTag("step", step);
            spanScroll?.SetTag("op_id", ctx.OperationId);
            var tScroll = System.Diagnostics.Stopwatch.StartNew();
            await human.HumanScrollAsync(auto, targetDistance: 0, waitForLoad: true, cancellationToken: ctx.CancellationToken);
            tScroll.Stop();
            var lsScroll = LabelSet.From(("endpoint", "Homefeed"));
            hScrollMs?.Record(tScroll.Elapsed.TotalMilliseconds, in lsScroll);

            using var spanAwait = Trace.StartActivity("await_api", ActivityKind.Internal);
            spanAwait?.SetTag("endpoint", "Homefeed");
            spanAwait?.SetTag("attempt", attempt);
            spanAwait?.SetTag("step", step);
            spanAwait?.SetTag("op_id", ctx.OperationId);
            var tAwait = System.Diagnostics.Stopwatch.StartNew();
            var got = await uam.WaitForResponsesAsync(ApiEndpointType.Homefeed, TimeSpan.FromSeconds(60), 1);
            tAwait.Stop();
            var lsAwait = LabelSet.From(("endpoint", "Homefeed"));
            hAwaitMs?.Record(tAwait.Elapsed.TotalMilliseconds, in lsAwait);
            if (!got)
            {
                var fail = last with { Step = step, Attempt = attempt, Stage = "await", LastError = "WaitForResponses(Homefeed) 超时", Completed = attempt >= last.MaxAttempts };
                await ctx.Repository.SaveAsync(CheckpointSerializer.Pack(ctx.OperationId, ++seq, fail), ctx.CancellationToken);
                return new ResOpResultHome { Completed = fail.Completed, LastCheckpoint = fail, Seq = seq };
            }

            var details = uam.GetMonitoredNoteDetails(ApiEndpointType.Homefeed);
            int newOnes = 0;
            foreach (var d in details)
            {
                var id = d.Id ?? string.Empty;
                if (string.IsNullOrEmpty(id)) continue;
                var dg = Digest(id);
                if (processed.Add(dg)) newOnes++;
            }
            if (processed.Count > 2000)
                processed = new HashSet<string>(processed.Take(2000));

            var aggregated = Math.Min(last.Aggregated + newOnes, last.TargetMax);
            var completed = aggregated >= last.TargetMax || attempt >= last.MaxAttempts;
            var ckpt = last with
            {
                Step = step,
                Attempt = attempt,
                Stage = completed ? "finalize" : "aggregate",
                Aggregated = aggregated,
                LastBatch = newOnes,
                ProcessedDigest = processed.ToArray(),
                Completed = completed,
                LastError = null
            };
            await ctx.Repository.SaveAsync(CheckpointSerializer.Pack(ctx.OperationId, ++seq, ckpt), ctx.CancellationToken);
            await uam.StopMonitoringAsync();
            uam.ClearMonitoredData(ApiEndpointType.Homefeed);

            if (completed)
                return new ResOpResultHome { Completed = true, LastCheckpoint = ckpt, Seq = seq };

            var scrollNext = ckpt with { Stage = "scroll_next" };
            await ctx.Repository.SaveAsync(CheckpointSerializer.Pack(ctx.OperationId, ++seq, scrollNext), ctx.CancellationToken);
            last = ckpt;
        }
    }

    private static string Digest(string input)
    {
        unchecked
        {
            const ulong offset = 1469598103934665603UL;
            const ulong prime = 1099511628211UL;
            ulong hash = offset;
            foreach (var c in input)
            {
                hash ^= c;
                hash *= prime;
            }
            return hash.ToString("x16");
        }
    }
}

/// <summary>
/// Homefeed 检查点。
/// </summary>
public readonly record struct HomefeedCheckpoint(
    int Step,
    int Attempt,
    string Stage,
    int TargetMax,
    int Aggregated,
    int LastBatch,
    string[] ProcessedDigest,
    bool Completed,
    string? LastError)
{
    public static HomefeedCheckpoint CreateInitial(int targetMax, int maxAttempts)
        => new(0, 0, "init", targetMax, 0, 0, Array.Empty<string>(), false, null) { MaxAttempts = maxAttempts };

    public int MaxAttempts { get; init; }
}

