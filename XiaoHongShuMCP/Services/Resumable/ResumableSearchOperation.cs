using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using HushOps.Core.Resumable;
using HushOps.Core.Automation.Abstractions;
using HushOps.Core.Observability;
using ResOpResult = HushOps.Core.Resumable.OperationResult<XiaoHongShuMCP.Services.Resumable.SearchNotesCheckpoint>;
using System.Diagnostics;

// 说明：命名空间迁移至 HushOps.Services.Resumable；统一品牌暴露。
namespace XiaoHongShuMCP.Services.Resumable;

/// <summary>
/// 搜索笔记的可恢复操作（多步状态机：监听+输入+滚动+聚合）。
/// - 破坏性变更：移除对服务层 SearchNotesAsync 的回退路径，统一以“UniversalApiMonitor 监听 + 拟人化输入/滚动 + 去重聚合”
///   作为唯一权威策略；缺失依赖将直接抛出配置异常，不再做服务调用兜底。
/// - 安全：不持久化敏感字段（如 Token/Cookie）；仅记录关键词与计数信息。
/// </summary>
public sealed class ResumableSearchOperation : IResumableOperation<SearchNotesCheckpoint>
{
    private readonly ILogger logger;
    private readonly string keyword;
    private readonly int targetMax;
    private readonly string sortBy;
    private readonly string noteType;
    private readonly string publishTime;
    private readonly int maxAttempts;
    // 多步化所需的强制依赖（统一监听策略）
    private readonly IBrowserManager browser;
    private readonly IPageStateGuard pageGuard;
    private readonly IHumanizedInteractionService human;
    private readonly IPageLoadWaitService pageWait;
    private readonly IUniversalApiMonitor universalApiMonitor;
    private readonly ILocatorPolicyStack locatorPolicy;
    private readonly IRateLimiter? rateLimiter;
    private readonly IMetrics? metrics;
    private readonly IHistogram? hEnsureMs;
    private readonly IHistogram? hInputMs;
    private readonly IHistogram? hAwaitMs;
    private readonly IHistogram? hAggregateMs;
    private readonly IHistogram? hScrollMs;
    private readonly ICounter? cStageFail;
    private static readonly ActivitySource Trace = new("XHS.Traces");

    public ResumableSearchOperation(
        ILogger<ResumableSearchOperation> logger,
        string keyword,
        int maxResults = 20,
        string sortBy = "comprehensive",
        string noteType = "all",
        string publishTime = "all",
        int maxAttempts = 5,
        IBrowserManager browser = null!,
        IPageStateGuard pageGuard = null!,
        IHumanizedInteractionService human = null!,
        IPageLoadWaitService pageWait = null!,
        IUniversalApiMonitor universalApiMonitor = null!,
        ILocatorPolicyStack locatorPolicy = null!,
        IRateLimiter? rateLimiter = null,
        IMetrics? metrics = null)
    {
        this.logger = logger;
        this.keyword = keyword;
        this.targetMax = Math.Max(1, maxResults);
        this.sortBy = sortBy;
        this.noteType = noteType;
        this.publishTime = publishTime;
        this.maxAttempts = Math.Max(1, maxAttempts);
        // 强制依赖（移除服务回退）：均不能为空
        this.browser = browser ?? throw new ArgumentNullException(nameof(browser));
        this.pageGuard = pageGuard ?? throw new ArgumentNullException(nameof(pageGuard));
        this.human = human ?? throw new ArgumentNullException(nameof(human));
        this.pageWait = pageWait ?? throw new ArgumentNullException(nameof(pageWait));
        this.universalApiMonitor = universalApiMonitor ?? throw new ArgumentNullException(nameof(universalApiMonitor));
        this.locatorPolicy = locatorPolicy ?? throw new ArgumentNullException(nameof(locatorPolicy));
        this.rateLimiter = rateLimiter;
        this.metrics = metrics;
        if (metrics != null)
        {
            // 低基数：仅携带 endpoint=SearchNotes；不包含 stage 作为标签，避免白名单外标签
            hEnsureMs = metrics.CreateHistogram("uam_stage_ensure_duration_ms", "EnsureContext 阶段耗时(ms)");
            hInputMs = metrics.CreateHistogram("uam_stage_input_duration_ms", "Input 阶段耗时(ms)");
            hAwaitMs = metrics.CreateHistogram("uam_stage_await_duration_ms", "AwaitAPI 阶段耗时(ms)");
            hAggregateMs = metrics.CreateHistogram("uam_stage_aggregate_duration_ms", "Aggregate 阶段耗时(ms)");
            hScrollMs = metrics.CreateHistogram("uam_stage_scroll_duration_ms", "ScrollNext 阶段耗时(ms)");
            cStageFail = metrics.CreateCounter("uam_stage_failures_total", "阶段失败计数");
        }
    }

    public async Task<ResOpResult> RunOrResumeAsync(OperationContext ctx)
    {
        var latest = await ctx.Repository.LoadLatestAsync(ctx.OperationId, ctx.CancellationToken);
        var last = latest == null
            ? SearchNotesCheckpoint.CreateInitial(keyword, sortBy, noteType, publishTime, targetMax, maxAttempts)
            : CheckpointSerializer.Unpack<SearchNotesCheckpoint>(latest);

        var attempt = last.Attempt + 1;
        var step = last.Step + 1;
        var kwHash = Hash8(keyword);
        logger.LogInformation("[ResumableSearch] attempt={Attempt}/{Max} step={Step} stage={Stage} keyword={Keyword} aggregated={Agg}/{Target}",
            attempt, last.MaxAttempts, step, last.Stage, keyword, last.Aggregated, last.TargetMax);

        // 阶段演进骨架：ensure_context → input → await_api
        long seq = (latest?.Seq ?? 0);
        var ensure = last with { Step = step, Attempt = attempt, Stage = "ensure_context" };
        await ctx.Repository.SaveAsync(CheckpointSerializer.Pack(ctx.OperationId, ++seq, ensure), ctx.CancellationToken);

        var input = ensure with { Stage = "input" };
        await ctx.Repository.SaveAsync(CheckpointSerializer.Pack(ctx.OperationId, ++seq, input), ctx.CancellationToken);

        var preAwait = input with { Stage = "await_api" };
        await ctx.Repository.SaveAsync(CheckpointSerializer.Pack(ctx.OperationId, ++seq, preAwait), ctx.CancellationToken);

        List<XiaoHongShuMCP.Services.NoteInfo> notes;
        // 统一监听+滚动分页（唯一权威策略）
        bool handledByLoop = false;
        {
            try
            {
                var auto = await browser.GetAutoPageAsync();
                var t0 = System.Diagnostics.Stopwatch.StartNew();
                var ok = await pageGuard.EnsureOnDiscoverOrSearchAsync(auto);
                t0.Stop();
                var lsEnsure = LabelSet.From(("endpoint", "SearchNotes"));
                hEnsureMs?.Record(t0.Elapsed.TotalMilliseconds, in lsEnsure);
                if (!ok)
                {
                    var lsFailEnsure = LabelSet.From(("endpoint", "SearchNotes"));
                    cStageFail?.Add(1, in lsFailEnsure);
                    throw new Exception("EnsureOnDiscoverOrSearch 失败");
                }

                // 初始化循环状态
                var processed = new HashSet<string>(last.ProcessedDigest);
                notes = new List<XiaoHongShuMCP.Services.NoteInfo>();

                while (true)
                {
                    // 每轮尝试视作一次 Attempt（外部 Attempt 仅用于回退路径）
                    step++;
                    attempt++;
                    // 阶段：ensure_context → input → await_api
                    long loopSeqBase = seq;
                    var ensureCtx = last with { Step = step, Attempt = attempt, Stage = "ensure_context" };
                    await ctx.Repository.SaveAsync(CheckpointSerializer.Pack(ctx.OperationId, ++loopSeqBase, ensureCtx), ctx.CancellationToken);

                    // 绑定监听
                    universalApiMonitor.ClearMonitoredData(ApiEndpointType.SearchNotes);
                    var endpoints = new HashSet<ApiEndpointType> { ApiEndpointType.SearchNotes };
                    var setup = universalApiMonitor.SetupMonitor(auto, endpoints);
                    if (!setup)
                        throw new Exception("SetupMonitor(SearchNotes) 失败");

                    // 仅在首次轮次执行输入（后续分页仅滚动触发）
                    if (attempt == 1 && last.Aggregated == 0)
                    {
                        try
                        {
                            var tInput = System.Diagnostics.Stopwatch.StartNew();
                            using var spanInput = Trace.StartActivity("input", ActivityKind.Internal);
                            spanInput?.SetTag("endpoint", "SearchNotes");
                            await pageWait.WaitForPageLoadAsync(auto);
                            await human.HumanWaitAsync(HushOps.Core.Humanization.HumanWaitType.ThinkingPause, ctx.CancellationToken);
                            var hint = new LocatorHint
                            {
                                Aliases = new[] { "SearchInput" },
                                Role = "textbox",
                                NameOrText = "搜" // 覆盖“搜索”“搜一搜”等常见占位/名称片段
                            };
                            var acquired = await locatorPolicy.AcquireAsync(auto, hint, ctx.CancellationToken);
                            var searchInput = acquired.Element ?? await human.FindElementAsync(auto, "SearchInput", retries: 2, timeout: 2000);
                            if (searchInput != null)
                            {
                                await human.HumanClickAsync(searchInput);
                                try { await auto.Keyboard.PressAsync("Control+A"); } catch { }
                                try { await auto.Keyboard.PressAsync("Meta+A"); } catch { }
                                try { await auto.Keyboard.PressAsync("Backspace"); } catch { }
                                await Task.Delay(Random.Shared.Next(300, 800), ctx.CancellationToken);
                                await human.HumanTypeAsync(auto, "SearchInput", keyword);
                                await auto.Keyboard.PressAsync("Enter");
                            }
                            tInput.Stop();
                            var lsInput = LabelSet.From(("endpoint", "SearchNotes"));
                            hInputMs?.Record(tInput.Elapsed.TotalMilliseconds, in lsInput);
                        }
                        catch
                        {
                            var lsFailInput = LabelSet.From(("endpoint", "SearchNotes"));
                            cStageFail?.Add(1, in lsFailInput);
                            // 输入失败不阻断
                        }
                    }

                    var preAwaitStage = ensureCtx with { Stage = "await_api" };
                    await ctx.Repository.SaveAsync(CheckpointSerializer.Pack(ctx.OperationId, ++loopSeqBase, preAwaitStage), ctx.CancellationToken);

                    // 滚动触发 & 等待
                    using var spanScroll = Trace.StartActivity("scroll", ActivityKind.Internal);
                    spanScroll?.SetTag("endpoint", "SearchNotes");
                    spanScroll?.SetTag("attempt", attempt);
                    spanScroll?.SetTag("step", step);
                    spanScroll?.SetTag("keyword_hash", kwHash);
                    spanScroll?.SetTag("op_id", ctx.OperationId);
                    var tScroll = System.Diagnostics.Stopwatch.StartNew();
                    await human.HumanScrollAsync(auto, targetDistance: 0, waitForLoad: true, cancellationToken: ctx.CancellationToken);
                    tScroll.Stop();
                    var lsScroll = LabelSet.From(("endpoint", "SearchNotes"));
                    hScrollMs?.Record(tScroll.Elapsed.TotalMilliseconds, in lsScroll);

                    using var spanAwait = Trace.StartActivity("await_api", ActivityKind.Internal);
                    spanAwait?.SetTag("endpoint", "SearchNotes");
                    spanAwait?.SetTag("attempt", attempt);
                    spanAwait?.SetTag("step", step);
                    spanAwait?.SetTag("keyword_hash", kwHash);
                    spanAwait?.SetTag("op_id", ctx.OperationId);
                    var tAwait = System.Diagnostics.Stopwatch.StartNew();
                    var got = await universalApiMonitor.WaitForResponsesAsync(ApiEndpointType.SearchNotes, TimeSpan.FromSeconds(60), 1);
                    tAwait.Stop();
                    var lsAwait = LabelSet.From(("endpoint", "SearchNotes"));
                    hAwaitMs?.Record(tAwait.Elapsed.TotalMilliseconds, in lsAwait);
                    if (!got)
                    {
                        var lsFailAwait = LabelSet.From(("endpoint", "SearchNotes"));
                        cStageFail?.Add(1, in lsFailAwait);
                        throw new TimeoutException("WaitForResponses(SearchNotes) 超时");
                    }

                    // 聚合本轮结果
                    using var spanAgg = Trace.StartActivity("aggregate", ActivityKind.Internal);
                    spanAgg?.SetTag("endpoint", "SearchNotes");
                    spanAgg?.SetTag("attempt", attempt);
                    spanAgg?.SetTag("step", step);
                    spanAgg?.SetTag("keyword_hash", kwHash);
                    spanAgg?.SetTag("op_id", ctx.OperationId);
                    var tAgg = System.Diagnostics.Stopwatch.StartNew();
                    var details = universalApiMonitor.GetMonitoredNoteDetails(ApiEndpointType.SearchNotes);
                    int newOnes = 0;
                    string? loopCursor = null;
                    foreach (var d in details)
                    {
                        var id = d.Id ?? string.Empty;
                        if (string.IsNullOrEmpty(id)) continue;
                        var dg = Digest(id);
                        if (processed.Add(dg))
                        {
                            newOnes++;
                            notes.Add(new XiaoHongShuMCP.Services.NoteInfo
                            {
                                Id = d.Id ?? string.Empty,
                                Title = d.Title ?? string.Empty,
                                Author = d.Author ?? string.Empty,
                                AuthorId = d.AuthorId ?? string.Empty,
                                AuthorAvatar = d.AuthorAvatar ?? string.Empty,
                                Url = d.Url ?? string.Empty,
                                CoverImage = d.CoverImage ?? string.Empty,
                                LikeCount = d.LikeCount,
                                CommentCount = d.CommentCount,
                                FavoriteCount = d.FavoriteCount,
                                ExtractedAt = d.ExtractedAt,
                                Quality = d.Quality,
                                MissingFields = d.MissingFields ?? [],
                                PageToken = d.PageToken,
                                SearchId = d.SearchId,
                                VideoUrl = d.VideoUrl ?? string.Empty,
                                VideoDuration = d.VideoDuration,
                                IsLiked = d.IsLiked,
                                IsCollected = d.IsCollected,
                                InteractInfo = d.InteractInfo,
                                CoverInfo = d.CoverInfo,
                                Images = d.Images ?? new List<string>()
                            });
                        }
                        loopCursor ??= d.SearchId ?? d.PageToken;
                    }

                    if (processed.Count > 1000)
                        processed = new HashSet<string>(processed.Take(1000));

                    var aggregatedNow = Math.Min((last.Aggregated) + newOnes, last.TargetMax);
                    var completedNow = aggregatedNow >= last.TargetMax || attempt >= last.MaxAttempts;

                    var loopCkpt = last with
                    {
                        Step = step,
                        Attempt = attempt,
                        Stage = completedNow ? "finalize" : "aggregate",
                        Aggregated = aggregatedNow,
                        LastBatch = newOnes,
                        ProcessedDigest = processed.ToArray(),
                        Cursor = loopCursor ?? last.Cursor,
                        ScrollOffset = last.ScrollOffset + Math.Max(0, newOnes),
                        Completed = completedNow,
                        LastError = null
                    };
                    await ctx.Repository.SaveAsync(CheckpointSerializer.Pack(ctx.OperationId, ++loopSeqBase, loopCkpt), ctx.CancellationToken);
                    tAgg.Stop();
                    var lsAgg = LabelSet.From(("endpoint", "SearchNotes"));
                    hAggregateMs?.Record(tAgg.Elapsed.TotalMilliseconds, in lsAgg);

                    await universalApiMonitor.StopMonitoringAsync();
                    universalApiMonitor.ClearMonitoredData(ApiEndpointType.SearchNotes);

                    // 更新 last 与 seq，准备下一轮
                    last = loopCkpt;
                    seq = loopSeqBase;

                    if (completedNow) { handledByLoop = true; break; }

                    // 写入 scroll_next，随后继续循环
                    var scrollCkpt = last with { Stage = "scroll_next" };
                    await ctx.Repository.SaveAsync(CheckpointSerializer.Pack(ctx.OperationId, ++seq, scrollCkpt), ctx.CancellationToken);
                }
            }
            catch (Exception ex)
            {
                // 统一策略：监听路径失败直接失败，不再使用服务调用回退。
                logger.LogError(ex, "[ResumableSearch] 监听+滚动分页失败（已移除回退路径）");
                var failed = last with { Step = step, Attempt = attempt, Stage = "aggregate", LastBatch = 0, LastError = ex.Message, Completed = attempt >= last.MaxAttempts };
                var envFail = CheckpointSerializer.Pack(ctx.OperationId, ++seq, failed);
                await ctx.Repository.SaveAsync(envFail, ctx.CancellationToken);
                return new ResOpResult { Completed = failed.Completed, LastCheckpoint = failed, Seq = envFail.Seq };
            }
        }
        if (handledByLoop)
        {
            // 循环已写入最终 checkpoint（last/seq 已更新），直接返回。
            return new ResOpResult { Completed = last.Completed, LastCheckpoint = last, Seq = seq };
        }
        var processed2 = new HashSet<string>(last.ProcessedDigest);
        int newOnes2 = 0;
        string? cursor2 = null;
        foreach (var n in notes)
        {
            var id = n?.Id ?? string.Empty;
            if (string.IsNullOrEmpty(id)) continue;
            var d = Digest(id);
            if (processed2.Add(d)) newOnes2++;
            cursor2 ??= n?.SearchId ?? n?.PageToken; // 记录一个游标（若存在）
        }
        // 限制摘要大小，防止无限增长（最多1000）
        if (processed2.Count > 1000)
        {
            processed2 = new HashSet<string>(processed2.Take(1000));
        }
        var aggregated = Math.Min(last.Aggregated + newOnes2, last.TargetMax);
        var completed = aggregated >= last.TargetMax || attempt >= last.MaxAttempts;
        var ckpt2 = last with
        {
            Step = step,
            Attempt = attempt,
            Stage = completed ? "finalize" : "aggregate",
            Aggregated = aggregated,
            LastBatch = newOnes2,
            ProcessedDigest = processed2.ToArray(),
            Cursor = cursor2 ?? last.Cursor,
            ScrollOffset = last.ScrollOffset + Math.Max(0, newOnes2),
            Completed = completed,
            LastError = null
        };
        var env = CheckpointSerializer.Pack(ctx.OperationId, ++seq, ckpt2);
        await ctx.Repository.SaveAsync(env, ctx.CancellationToken);

        // 若尚未完成，尝试执行一次滚动推进（记录阶段但不强依赖成功）。
        if (!completed)
        {
            try
            {
                var auto = await browser.GetAutoPageAsync();
                var ok = await pageGuard.EnsureOnDiscoverOrSearchAsync(auto);
                if (ok)
                {
                    var scrollCkpt = ckpt2 with { Stage = "scroll_next" };
                    await human.HumanScrollAsync(auto, targetDistance: 0, waitForLoad: true, cancellationToken: ctx.CancellationToken);
                    await ctx.Repository.SaveAsync(CheckpointSerializer.Pack(ctx.OperationId, ++seq, scrollCkpt), ctx.CancellationToken);
                }
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "[ResumableSearch] 滚动推进失败（忽略，等待下次运行继续）");
            }
        }

        return new ResOpResult { Completed = completed, LastCheckpoint = ckpt2, Seq = seq };
    }

    private static string Hash8(string text)
    {
        try
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            var b = System.Text.Encoding.UTF8.GetBytes(text ?? string.Empty);
            var h = sha.ComputeHash(b);
            return Convert.ToHexString(h.AsSpan(0, 4));
        }
        catch { return ""; }
    }

    private static string Digest(string input)
    {
        // 简化的稳定摘要：FNV-1a 64 位 → 16 进制短串
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
/// 搜索检查点：记录参数、进度与摘要计数。
/// </summary>
public readonly record struct SearchNotesCheckpoint(
    int Step,
    string Stage,
    string Keyword,
    string SortBy,
    string NoteType,
    string PublishTime,
    int TargetMax,
    int Aggregated,
    int LastBatch,
    int Attempt,
    int MaxAttempts,
    string[] ProcessedDigest,
    string? Cursor,
    int ScrollOffset,
    bool Completed,
    string? LastError)
{
    public static SearchNotesCheckpoint CreateInitial(string keyword, string sortBy, string noteType, string publishTime, int targetMax, int maxAttempts)
        => new(0, "init", keyword, sortBy, noteType, publishTime, targetMax, 0, 0, 0, maxAttempts, Array.Empty<string>(), null, 0, false, null);
}



