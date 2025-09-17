using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using HushOps.Core.Automation.Abstractions;
using HushOps.Core.Observability;
using HushOps.Core.Resumable;
using ResOpResultInteract = HushOps.Core.Resumable.OperationResult<XiaoHongShuMCP.Services.Resumable.InteractCheckpoint>;
using System.Diagnostics;

// 说明：命名空间迁移至 HushOps.Services.Resumable。
namespace XiaoHongShuMCP.Services.Resumable;

/// <summary>
/// 点赞/收藏 可恢复操作：Ensure → Locate → Bind → Click → AwaitAPI → Verify/Finalize。
/// </summary>
public sealed class ResumableInteractOperation : IResumableOperation<InteractCheckpoint>
{
    private readonly ILogger logger;
    private readonly IBrowserManager browser;
    private readonly IPageStateGuard guard;
    private readonly IHumanizedInteractionService human;
    private readonly IPageLoadWaitService pageWait;
    private readonly IUniversalApiMonitor uam;
    private readonly IMetrics? metrics;
    private readonly ILocatorPolicyStack locator;
    private readonly IHistogram? hEnsureMs;
    private readonly IHistogram? hLocateMs;
    private readonly IHistogram? hBindMs;
    private readonly IHistogram? hClickMs;
    private readonly IHistogram? hAwaitMs;
    private readonly IHistogram? hVerifyMs;
    private readonly ICounter? cStageFail;
    private static readonly ActivitySource Trace = new("XHS.Traces");

    public ResumableInteractOperation(
        ILogger<ResumableInteractOperation> logger,
        IBrowserManager browser,
        IPageStateGuard guard,
        IHumanizedInteractionService human,
        IPageLoadWaitService pageWait,
        IUniversalApiMonitor uam,
        ILocatorPolicyStack locator,
        IMetrics? metrics = null)
    {
        this.logger = logger;
        this.browser = browser ?? throw new ArgumentNullException(nameof(browser));
        this.guard = guard ?? throw new ArgumentNullException(nameof(guard));
        this.human = human ?? throw new ArgumentNullException(nameof(human));
        this.pageWait = pageWait ?? throw new ArgumentNullException(nameof(pageWait));
        this.uam = uam ?? throw new ArgumentNullException(nameof(uam));
        this.metrics = metrics;
        this.locator = locator ?? throw new ArgumentNullException(nameof(locator));
        if (metrics != null)
        {
            hEnsureMs = metrics.CreateHistogram("uam_stage_ensure_duration_ms", "Ensure 阶段耗时(ms)");
            hLocateMs = metrics.CreateHistogram("uam_stage_input_duration_ms", "Locate 阶段耗时(ms)");
            hBindMs = metrics.CreateHistogram("uam_stage_aggregate_duration_ms", "Bind 阶段耗时(ms)");
            hClickMs = metrics.CreateHistogram("uam_stage_scroll_duration_ms", "Click 阶段耗时(ms)");
            hAwaitMs = metrics.CreateHistogram("uam_stage_await_duration_ms", "AwaitAPI 阶段耗时(ms)");
            hVerifyMs = metrics.CreateHistogram("uam_stage_verify_duration_ms", "Verify 阶段耗时(ms)");
            cStageFail = metrics.CreateCounter("uam_stage_failures_total", "阶段失败计数");
        }
    }

    public async Task<ResOpResultInteract> RunOrResumeAsync(OperationContext ctx)
    {
        // Checkpoint 中包含 keyword/doLike/doFavorite/Attempt/MaxAttempts 等参数
        var latest = await ctx.Repository.LoadLatestAsync(ctx.OperationId, ctx.CancellationToken);
        if (latest == null)
        {
            throw new InvalidOperationException("InteractCheckpoint 缺少初始参数，请先通过工具构造初始检查点");
        }
        var last = CheckpointSerializer.Unpack<InteractCheckpoint>(latest);
        var attempt = last.Attempt + 1;
        var step = last.Step + 1;
        long seq = latest.Seq;

        var auto = await browser.GetAutoPageAsync();

        // Ensure
        using var spanEnsure = Trace.StartActivity("ensure_context", ActivityKind.Internal);
        spanEnsure?.SetTag("endpoint", "Interact");
        spanEnsure?.SetTag("attempt", attempt);
        spanEnsure?.SetTag("step", step);
        spanEnsure?.SetTag("op_id", ctx.OperationId);
        var tEnsure = System.Diagnostics.Stopwatch.StartNew();
        var ok = await guard.EnsureOnDiscoverOrSearchAsync(auto);
        tEnsure.Stop();
        var lsEnsure = LabelSet.From(("endpoint", "Interact"));
        hEnsureMs?.Record(tEnsure.Elapsed.TotalMilliseconds, in lsEnsure);
        if (!ok)
        {
            var fail = last with { Step = step, Attempt = attempt, Stage = "ensure", LastError = "EnsureOnDiscoverOrSearch 失败", Completed = attempt >= last.MaxAttempts };
            await ctx.Repository.SaveAsync(CheckpointSerializer.Pack(ctx.OperationId, ++seq, fail), ctx.CancellationToken);
            var lsFailEnsure = LabelSet.From(("endpoint", "Interact"));
            cStageFail?.Add(1, in lsFailEnsure);
            return new ResOpResultInteract { Completed = fail.Completed, LastCheckpoint = fail, Seq = seq };
        }

        // Locate（纯 UI 策略）：尝试输入关键词并点击首个结果卡片
        using var spanLocate = Trace.StartActivity("locate", ActivityKind.Internal);
        spanLocate?.SetTag("endpoint", "Interact");
        spanLocate?.SetTag("attempt", attempt);
        spanLocate?.SetTag("step", step);
        spanLocate?.SetTag("op_id", ctx.OperationId);
        var tLocate = System.Diagnostics.Stopwatch.StartNew();
        await pageWait.WaitForPageLoadAsync(auto);
        try
        {
            // 搜索输入（优先策略栈）
            var hintInput = new LocatorHint { Aliases = new[] { "SearchInput" }, Role = "textbox", NameOrText = "搜" };
            var acquiredInput = await locator.AcquireAsync(auto, hintInput, ctx.CancellationToken);
            var searchInput = acquiredInput.Element ?? await human.FindElementAsync(auto, "SearchInput", retries: 2, timeout: 2000);
            if (searchInput != null)
            {
                await human.HumanClickAsync(searchInput);
                await human.HumanTypeAsync(auto, "SearchInput", last.Keyword);
            }
            // 结果卡片（优先策略栈，按关键词文本靠近）
            var candidates = new[] { "FirstSearchResult", "SearchResultFirst", "SearchResultCard", "NoteCard0", "NoteCard" };
            IAutoElement? card = null;
            var hintCard = new LocatorHint { Aliases = candidates, NameOrText = last.Keyword };
            var acquiredCard = await locator.AcquireAsync(auto, hintCard, ctx.CancellationToken);
            card = acquiredCard.Element;
            if (card == null)
            {
                foreach (var alias in candidates)
                {
                    card = await human.FindElementAsync(auto, alias, retries: 2, timeout: 1500);
                    if (card != null) break;
                }
            }
            if (card == null)
            {
                tLocate.Stop();
                var lsLocateFail = LabelSet.From(("endpoint", "Interact"));
                hLocateMs?.Record(tLocate.Elapsed.TotalMilliseconds, in lsLocateFail);
                var fail = last with { Step = step, Attempt = attempt, Stage = "locate", LastError = "未找到搜索结果卡片", Completed = attempt >= last.MaxAttempts };
                await ctx.Repository.SaveAsync(CheckpointSerializer.Pack(ctx.OperationId, ++seq, fail), ctx.CancellationToken);
                cStageFail?.Add(1, in lsLocateFail);
                return new ResOpResultInteract { Completed = fail.Completed, LastCheckpoint = fail, Seq = seq };
            }
            using var spanClick = Trace.StartActivity("click", ActivityKind.Internal);
            spanClick?.SetTag("endpoint", "Interact");
            var tClick = System.Diagnostics.Stopwatch.StartNew();
            await human.HumanBetweenActionsDelayAsync();
            await human.HumanClickAsync(card);
            tClick.Stop();
            var lsClick2 = LabelSet.From(("endpoint", "Interact"));
            hClickMs?.Record(tClick.Elapsed.TotalMilliseconds, in lsClick2);
        }
        catch (Exception ex)
        {
            tLocate.Stop();
            var lsLocateEx = LabelSet.From(("endpoint", "Interact"));
            hLocateMs?.Record(tLocate.Elapsed.TotalMilliseconds, in lsLocateEx);
            var fail = last with { Step = step, Attempt = attempt, Stage = "locate", LastError = $"定位或打开详情失败: {ex.Message}", Completed = attempt >= last.MaxAttempts };
            await ctx.Repository.SaveAsync(CheckpointSerializer.Pack(ctx.OperationId, ++seq, fail), ctx.CancellationToken);
            cStageFail?.Add(1, in lsLocateEx);
            return new ResOpResultInteract { Completed = fail.Completed, LastCheckpoint = fail, Seq = seq };
        }
        tLocate.Stop();
        var lsLocateOk = LabelSet.From(("endpoint", "Interact"));
        hLocateMs?.Record(tLocate.Elapsed.TotalMilliseconds, in lsLocateOk);

        // Bind
        using var spanBind = Trace.StartActivity("bind", ActivityKind.Internal);
        spanBind?.SetTag("endpoint", "Interact");
        spanBind?.SetTag("attempt", attempt);
        spanBind?.SetTag("step", step);
        spanBind?.SetTag("op_id", ctx.OperationId);
        var tBind = System.Diagnostics.Stopwatch.StartNew();
        var endpoints = new HashSet<ApiEndpointType>();
        if (last.DoLike) endpoints.Add(ApiEndpointType.LikeNote);
        if (last.DoFavorite) endpoints.Add(ApiEndpointType.CollectNote);
        uam.ClearMonitoredData(null);
        var setup = uam.SetupMonitor(auto, endpoints);
        tBind.Stop();
        var lsBind = LabelSet.From(("endpoint", "Interact"));
        hBindMs?.Record(tBind.Elapsed.TotalMilliseconds, in lsBind);
        if (!setup)
        {
            var fail = last with { Step = step, Attempt = attempt, Stage = "bind", LastError = "SetupMonitor 失败", Completed = attempt >= last.MaxAttempts };
            await ctx.Repository.SaveAsync(CheckpointSerializer.Pack(ctx.OperationId, ++seq, fail), ctx.CancellationToken);
            var lsFailBind = LabelSet.From(("endpoint", "Interact"));
            cStageFail?.Add(1, in lsFailBind);
            return new ResOpResultInteract { Completed = fail.Completed, LastCheckpoint = fail, Seq = seq };
        }

        // Click + AwaitAPI
        InteractionResult? likeRes = null;
        InteractionResult? favRes = null;
        if (last.DoLike)
        {
            likeRes = await human.HumanLikeAsync();
        }
        if (last.DoFavorite)
        {
            favRes = await human.HumanFavoriteAsync(auto);
        }

        using var spanAwait = Trace.StartActivity("await_api", ActivityKind.Internal);
        spanAwait?.SetTag("endpoint", "Interact");
        spanAwait?.SetTag("attempt", attempt);
        spanAwait?.SetTag("step", step);
        spanAwait?.SetTag("op_id", ctx.OperationId);
        var tAwait = System.Diagnostics.Stopwatch.StartNew();
        var okLike = !last.DoLike || await uam.WaitForResponsesAsync(ApiEndpointType.LikeNote, TimeSpan.FromSeconds(30), 1);
        var okFav = !last.DoFavorite || await uam.WaitForResponsesAsync(ApiEndpointType.CollectNote, TimeSpan.FromSeconds(30), 1);
        tAwait.Stop();
        var lsAwait = LabelSet.From(("endpoint", "Interact"));
        hAwaitMs?.Record(tAwait.Elapsed.TotalMilliseconds, in lsAwait);

        // Verify：DOM 状态校验 + API 确认（策略栈优先）
        var tVerify = System.Diagnostics.Stopwatch.StartNew();
        bool vLike = !last.DoLike;
        bool vFav = !last.DoFavorite;
        try
        {
            if (last.DoLike)
            {
                var hint = new LocatorHint { Aliases = new[] { "likeButtonActive", "LikeWrapper" }, NameOrText = "已赞" };
                var acq = await locator.AcquireAsync(auto, hint, ctx.CancellationToken);
                var el = acq.Element ?? await human.FindElementAsync(auto, "likeButtonActive", retries: 1, timeout: 800);
                vLike = el != null || okLike;
            }
            if (last.DoFavorite)
            {
                var hint = new LocatorHint { Aliases = new[] { "favoriteButtonActive", "CollectWrapper" }, NameOrText = "已收藏" };
                var acq = await locator.AcquireAsync(auto, hint, ctx.CancellationToken);
                var el = acq.Element ?? await human.FindElementAsync(auto, "favoriteButtonActive", retries: 1, timeout: 800);
                vFav = el != null || okFav;
            }
        }
        catch { }
        finally
        {
            tVerify.Stop();
            var lsVerify = LabelSet.From(("endpoint", "Interact"));
            hVerifyMs?.Record(tVerify.Elapsed.TotalMilliseconds, in lsVerify);
        }
        var success = vLike && vFav;
        var completed = success || attempt >= last.MaxAttempts;
        var ckpt = last with
        {
            Step = step,
            Attempt = attempt,
            Stage = completed ? "finalize" : "await",
            Completed = completed,
            LastError = success ? null : "API未确认",
            LikeResult = likeRes,
            FavoriteResult = favRes
        };
        await ctx.Repository.SaveAsync(CheckpointSerializer.Pack(ctx.OperationId, ++seq, ckpt), ctx.CancellationToken);
        await uam.StopMonitoringAsync();
        uam.ClearMonitoredData(null);

        return new ResOpResultInteract { Completed = completed, LastCheckpoint = ckpt, Seq = seq };
    }
}

/// <summary>
/// 互动检查点。
/// </summary>
public readonly record struct InteractCheckpoint(
    int Step,
    int Attempt,
    string Stage,
    string Keyword,
    bool DoLike,
    bool DoFavorite,
    int MaxAttempts,
    InteractionResult? LikeResult,
    InteractionResult? FavoriteResult,
    bool Completed,
    string? LastError)
{
    public static InteractCheckpoint CreateInitial(string keyword, bool doLike, bool doFavorite, int maxAttempts)
        => new(0, 0, "init", keyword, doLike, doFavorite, maxAttempts, null, null, false, null);
}

