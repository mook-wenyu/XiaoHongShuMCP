using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HushOps.Servers.XiaoHongShu.Configuration;
using HushOps.Servers.XiaoHongShu.Infrastructure.ToolExecution;
using HushOps.Servers.XiaoHongShu.Services.Humanization;
using HushOps.Servers.XiaoHongShu.Services.Humanization.Interactions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace HushOps.Servers.XiaoHongShu.Tools;

[McpServerToolType]
public sealed class BehaviorFlowTool
{
    private readonly IHumanizedActionService _service;
    private readonly ILogger<BehaviorFlowTool> _logger;
    private readonly HumanBehaviorOptions _behaviorOptions;

    public BehaviorFlowTool(
        IHumanizedActionService service,
        ILogger<BehaviorFlowTool> logger,
        IOptions<HumanBehaviorOptions> behaviorOptions)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _behaviorOptions = behaviorOptions?.Value ?? throw new ArgumentNullException(nameof(behaviorOptions));
    }

    [McpServerTool(Name = "xhs_random_browse"),
     Description("根据用户画像或随机选择笔记，打开详情，几率点赞/收藏 | Browse random note based on portrait or randomly, open detail, probabilistic like/favorite")]
    public Task<OperationResult<BrowseFlowResult>> RandomBrowseAsync(
        [Description("拟人化浏览请求参数 | Browse action request payload")] BehaviorFlowRequest request,
        [Description("取消令牌 | Token that cancels the operation if triggered")] CancellationToken cancellationToken = default)
    {
        var keywords = Array.Empty<string>(); // 让 KeywordResolver 从画像或默认配置解析
        var portraitId = request?.PortraitId;
        var browserKey = string.IsNullOrWhiteSpace(request?.BrowserKey) ? "user" : request.BrowserKey.Trim();
        var behaviorProfile = string.IsNullOrWhiteSpace(request?.BehaviorProfile) ? "default" : request.BehaviorProfile.Trim();

        return ExecuteBrowseFlowAsync(keywords, portraitId, browserKey, behaviorProfile, cancellationToken);
    }

    [McpServerTool(Name = "xhs_keyword_browse"), Description("根据关键词数组选择笔记，打开详情，几率点赞/收藏 | Browse note by keyword array, open detail, probabilistic like/favorite")]
    public Task<OperationResult<BrowseFlowResult>> KeywordBrowseAsync(
        [Description("拟人化浏览请求参数 | Browse action request payload")] BehaviorFlowRequest request,
        [Description("取消令牌 | Token that cancels the operation if triggered")] CancellationToken cancellationToken = default)
    {
        var keywords = NormalizeKeywords(request?.Keywords);
        var portraitId = request?.PortraitId;
        var browserKey = string.IsNullOrWhiteSpace(request?.BrowserKey) ? "user" : request.BrowserKey.Trim();
        var behaviorProfile = string.IsNullOrWhiteSpace(request?.BehaviorProfile) ? "default" : request.BehaviorProfile.Trim();

        return ExecuteBrowseFlowAsync(keywords, portraitId, browserKey, behaviorProfile, cancellationToken);
    }

    /// <summary>
    /// 中文：执行完整的浏览流程（选择笔记→概率化点赞→概率化收藏）。
    /// English: Executes complete browse flow (select note → probabilistic like → probabilistic favorite).
    /// </summary>
    private async Task<OperationResult<BrowseFlowResult>> ExecuteBrowseFlowAsync(
        IReadOnlyList<string> keywords,
        string? portraitId,
        string browserKey,
        string behaviorProfile,
        CancellationToken cancellationToken)
    {
        var requestId = Guid.NewGuid().ToString("N");

        // 步骤1: 选择笔记
        var selectRequest = new HumanizedActionRequest(
            keywords,
            portraitId,
            null,
            browserKey,
            requestId,
            behaviorProfile);

        var selectPlan = await _service.PrepareAsync(selectRequest, HumanizedActionKind.SelectNote, cancellationToken).ConfigureAwait(false);
        var selectOutcome = await _service.ExecuteAsync(selectPlan, cancellationToken).ConfigureAwait(false);

        if (!selectOutcome.Success)
        {
            _logger.LogWarning("[BrowseFlow] 选择笔记失败: {Message}", selectOutcome.Message);
            return OperationResult<BrowseFlowResult>.Fail(
                selectOutcome.Status,
                selectOutcome.Message ?? "选择笔记失败",
                new Dictionary<string, string>(selectOutcome.Metadata, StringComparer.OrdinalIgnoreCase));
        }

        // 提取笔记信息和关键词信息
        var noteId = selectOutcome.Metadata.TryGetValue("detail.noteId", out var id) ? id : null;
        var noteTitle = selectOutcome.Metadata.TryGetValue("detail.title", out var title) ? title : null;
        var noteUrl = selectOutcome.Metadata.TryGetValue("detail.url", out var url) ? url : null;
        var selectedKeyword = selectOutcome.Metadata.TryGetValue("keywords.selected", out var kw) ? kw : selectPlan.ResolvedKeyword;
        var keywordSource = selectOutcome.Metadata.TryGetValue("keyword.source", out var source) ? source : null;

        // 步骤2: 获取行为配置
        var profile = GetBehaviorProfile(behaviorProfile);

        // 步骤3: 概率化点赞
        var likeResult = await TryExecuteInteractionAsync(
            HumanizedActionKind.LikeCurrentNote,
            profile.LikeProbability,
            browserKey,
            behaviorProfile,
            cancellationToken).ConfigureAwait(false);

        // 步骤4: 概率化收藏
        var favoriteResult = await TryExecuteInteractionAsync(
            HumanizedActionKind.FavoriteCurrentNote,
            profile.FavoriteProbability,
            browserKey,
            behaviorProfile,
            cancellationToken).ConfigureAwait(false);

        // 步骤5: 构建结果
        var interactions = new List<string>();
        var skipped = new List<string>();
        var failed = new List<string>();

        if (likeResult.Status == "success") interactions.Add("Like");
        else if (likeResult.Status == "skipped") skipped.Add("Like");
        else if (likeResult.Status == "failed") failed.Add("Like");

        if (favoriteResult.Status == "success") interactions.Add("Favorite");
        else if (favoriteResult.Status == "skipped") skipped.Add("Favorite");
        else if (favoriteResult.Status == "failed") failed.Add("Favorite");

        // TODO: AI 评论功能（等接入 AI 代理后实现）
        // 根据笔记标题和正文内容生成相关评论
        // if (enableAiComment && profile.CommentProbability > 0)
        // {
        //     var commentText = await _aiCommentService.GenerateCommentAsync(noteTitle, noteContent, cancellationToken);
        //     var commentRequest = new HumanizedActionRequest(Array.Empty<string>(), null, commentText, browserKey, null, behaviorProfile);
        //     await _service.ExecuteAsync(commentRequest, HumanizedActionKind.CommentCurrentNote, cancellationToken);
        // }

        var result = new BrowseFlowResult(
            keywords.Count > 0 ? "KeywordBrowse" : "RandomBrowse",
            selectedKeyword,
            keywordSource,
            noteId,
            noteTitle,
            noteUrl,
            interactions.ToArray(),
            skipped.ToArray(),
            failed.ToArray(),
            behaviorProfile,
            requestId);

        _logger.LogInformation(
            "[BrowseFlow] 完成: 关键词={Keyword} 来源={Source} 笔记={NoteId} 互动={Interactions}",
            selectedKeyword,
            keywordSource,
            noteId,
            string.Join(",", interactions));

        var metadata = new Dictionary<string, string>(selectOutcome.Metadata, StringComparer.OrdinalIgnoreCase)
        {
            ["browseType"] = result.BrowseType,
            ["interactions.executed"] = string.Join(",", interactions),
            ["interactions.skipped"] = string.Join(",", skipped),
            ["interactions.failed"] = string.Join(",", failed)
        };

        return OperationResult<BrowseFlowResult>.Ok(result, "ok", metadata);
    }

    private string? SelectCommentText(DiscoverFlowRequest request)
    {
        if (request.CommentTexts is not { Length: > 0 })
        {
            return null;
        }

        var pool = request.CommentTexts
            .Where(static text => !string.IsNullOrWhiteSpace(text))
            .Select(static text => text.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (pool.Length == 0)
        {
            return null;
        }

        return pool.Length == 1 ? pool[0] : pool[Random.Shared.Next(pool.Length)];
    }

    private async Task<InteractionExecutionResult> ExecuteInteractionAsync(
        HumanizedActionKind kind,
        IReadOnlyList<string> keywords,
        string? portraitId,
        string browserKey,
        string behaviorProfile,
        string parentRequestId,
        string? commentText,
        CancellationToken cancellationToken)
    {
        var interactionRequestId = $"{parentRequestId}-{kind.ToString().ToLowerInvariant()}";
        var actionRequest = new HumanizedActionRequest(
            keywords,
            portraitId,
            kind == HumanizedActionKind.CommentCurrentNote ? commentText : null,
            browserKey,
            interactionRequestId,
            behaviorProfile);

        HumanizedActionPlan? plan = null;

        try
        {
            plan = await _service.PrepareAsync(actionRequest, kind, cancellationToken).ConfigureAwait(false);
            var outcome = await _service.ExecuteAsync(plan, cancellationToken).ConfigureAwait(false);

            var metadata = new Dictionary<string, string>(plan.Metadata, StringComparer.OrdinalIgnoreCase)
            {
                ["kind"] = kind.ToString(),
                ["requestId"] = interactionRequestId,
                ["browserKey"] = plan.BrowserKey,
                ["behaviorProfile"] = plan.BehaviorProfile
            };

            foreach (var pair in outcome.Metadata)
            {
                metadata[pair.Key] = pair.Value;
            }

            var baseSummary = plan.Script.ToSummary();
            var telemetry = HumanizedActionMetadataReader.Read(
                metadata,
                baseSummary,
                outcome.Success ? baseSummary : HumanizedActionSummary.Empty);

            metadata["interaction.plan.count"] = telemetry.Planned.Count.ToString(CultureInfo.InvariantCulture);
            metadata["interaction.execute.count"] = telemetry.Executed.Count.ToString(CultureInfo.InvariantCulture);

            var snapshot = outcome.Success
                ? DiscoverInteractionResult.Success(outcome.Message)
                : DiscoverInteractionResult.Failed(outcome.Message);

            return new InteractionExecutionResult(
                outcome.Success,
                outcome.Status,
                snapshot,
                metadata,
                outcome.Message);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var status = ServerToolExecutor.MapExceptionCode(ex);
            var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["kind"] = kind.ToString(),
                ["status"] = status,
                ["error"] = ex.Message
            };

            if (plan is not null)
            {
                foreach (var pair in plan.Metadata)
                {
                    metadata[pair.Key] = pair.Value;
                }
            }

            return new InteractionExecutionResult(
                false,
                status,
                DiscoverInteractionResult.Failed(ex.Message),
                metadata,
                ex.Message);
        }
    }

    private static void MergeMetadata(IDictionary<string, string> destination, IReadOnlyDictionary<string, string> source, string prefix)
    {
        foreach (var pair in source)
        {
            destination[$"{prefix}{pair.Key}"] = pair.Value;
        }
    }

    private sealed record InteractionExecutionResult(
        bool Success,
        string Status,
        DiscoverInteractionResult Snapshot,
        Dictionary<string, string> Metadata,
        string? Message);
    private static IReadOnlyList<string> NormalizeKeywords(IReadOnlyList<string>? keywords)
    {
        if (keywords is null || keywords.Count == 0)
        {
            return Array.Empty<string>();
        }

        var normalized = keywords
            .Where(static k => !string.IsNullOrWhiteSpace(k))
            .Select(static k => k.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return normalized.Length == 0 ? Array.Empty<string>() : normalized;
    }

    /// <summary>
    /// 中文：获取指定的行为档案配置，未找到时返回默认档案。
    /// English: Retrieves the specified behavior profile; returns default profile if not found.
    /// </summary>
    private HumanBehaviorProfileOptions GetBehaviorProfile(string profileKey)
    {
        if (_behaviorOptions.Profiles.TryGetValue(profileKey, out var profile))
        {
            return profile;
        }

        _logger.LogWarning("[BehaviorFlowTool] 未找到行为档案 {ProfileKey}，使用默认档案 {DefaultProfile}", profileKey, _behaviorOptions.DefaultProfile);
        return _behaviorOptions.Profiles[_behaviorOptions.DefaultProfile];
    }

    /// <summary>
    /// 中文：根据概率执行互动动作（点赞、收藏等）。
    /// English: Executes interaction action (like, favorite, etc.) based on probability.
    /// </summary>
    private async Task<InteractionResult> TryExecuteInteractionAsync(
        HumanizedActionKind kind,
        double probability,
        string browserKey,
        string behaviorProfile,
        CancellationToken cancellationToken)
    {
        // 概率判断
        if (Random.Shared.NextDouble() >= probability)
        {
            return InteractionResult.Skipped();
        }

        // 执行互动
        var request = new HumanizedActionRequest(
            Array.Empty<string>(),
            null,
            null,
            browserKey,
            null,
            behaviorProfile);

        var outcome = await _service.ExecuteAsync(request, kind, cancellationToken).ConfigureAwait(false);

        if (outcome.Success)
        {
            return InteractionResult.Success(kind.ToString());
        }

        _logger.LogWarning("[BrowseFlow] 互动失败 {Kind}: {Message}", kind, outcome.Message);
        return InteractionResult.Failed(outcome.Message);
    }
}

public sealed record BehaviorFlowRequest(
    [property: Description("候选关键词列表 | Candidate keywords; falls back to portrait/default when为空")] string[]? Keywords,
    [property: Description("画像 ID，用于推荐关键词 | Portrait identifier for resolving fallback keywords")] string PortraitId = "",
    [property: Description("浏览器键，user 表示用户配置 | Browser key: 'user' for the user profile, others map to isolated profiles")] string BrowserKey = "",
    [property: Description("行为档案键，覆盖默认拟人化配置 | Behavior profile key overriding the default humanization profile")] string BehaviorProfile = "");

/// <summary>
/// 中文：浏览流程返回结果（用于 RandomBrowse 和 KeywordBrowse）。
/// English: Browse flow result (for RandomBrowse and KeywordBrowse).
/// </summary>
public sealed record BrowseFlowResult(
    [property: Description("浏览类型 | Browse type")] string BrowseType,
    [property: Description("选中的关键词 | Selected keyword")] string? SelectedKeyword,
    [property: Description("关键词来源 | Keyword source (request/portrait/default)")] string? KeywordSource,
    [property: Description("笔记 ID | Note identifier")] string? NoteId,
    [property: Description("笔记标题 | Note title")] string? NoteTitle,
    [property: Description("笔记链接 | Note URL")] string? NoteUrl,
    [property: Description("执行的互动列表 | Executed interactions")] string[] Interactions,
    [property: Description("跳过的互动列表 | Skipped interactions")] string[] SkippedInteractions,
    [property: Description("失败的互动列表 | Failed interactions")] string[] FailedInteractions,
    [property: Description("行为档案 | Behavior profile")] string BehaviorProfile,
    [property: Description("请求 ID | Request identifier")] string RequestId);

public sealed record BehaviorFlowToolResult(
    [property: Description("执行的动作类型 | Executed action type")] string Kind,
    [property: Description("附加说明或错误信息 | Additional message or error details")] string? Message,
    [property: Description("关联的请求 ID | Associated request identifier")] string? RequestId,
    [property: Description("解析后的关键词 | Resolved keyword used during execution")] string ResolvedKeyword,
    [property: Description("使用的行为档案 | Behavior profile applied for the run")] string BehaviorProfile,
    [property: Description("执行的动作序列 | Ordered list of executed actions")] string[] Actions,
    [property: Description("计划阶段的动作概览 | Summary of planned actions")] HumanizedActionSummary Planned,
    [property: Description("执行阶段的动作概览 | Summary of executed actions")] HumanizedActionSummary Executed,
    [property: Description("一致性告警列表 | Consistency warnings emitted during execution")] string[] Warnings,
    [property: Description("命中的关键词（同 ResolvedKeyword，提供更直观字段）| Selected keyword echoed for clients")] string? SelectedKeyword = null);
/// <summary>
/// 中文：互动执行结果（内部使用）。
/// English: Interaction execution result (internal use).
/// </summary>
internal sealed record InteractionResult(string Status, string? ActionType = null, string? Message = null)
{
    public static InteractionResult Success(string actionType) => new("success", actionType);
    public static InteractionResult Skipped() => new("skipped");
    public static InteractionResult Failed(string? message) => new("failed", Message: message);
}
public sealed record DiscoverFlowRequest(
    [property: Description("候选关键词列表 | Candidate keywords for discover flow")] string[]? Keywords = null,
    [property: Description("画像 ID，用于关键词兜底 | Portrait identifier for fallback keywords")] string PortraitId = "",
    [property: Description("搜索结果选择策略 | Note selection strategy")] DiscoverNoteSelectionStrategy NoteSelection = DiscoverNoteSelectionStrategy.First,
    [property: Description("是否执行点赞 | Whether to perform like interaction")] bool PerformLike = true,
    [property: Description("是否执行收藏 | Whether to perform favorite interaction")] bool PerformFavorite = true,
    [property: Description("是否执行评论 | Whether to perform comment interaction")] bool PerformComment = true,
    [property: Description("评论候选文本 | Candidate comment texts")] string[]? CommentTexts = null,
    [property: Description("浏览器键 | Browser profile key")] string BrowserKey = "",
    [property: Description("行为档案键 | Behavior profile key")] string BehaviorProfile = "");

public enum DiscoverNoteSelectionStrategy
{
    First,
    Random
}

public sealed record DiscoverFlowResult(
    [property: Description("请求 ID | Request identifier")] string RequestId,
    [property: Description("命中的关键词 | Selected keyword")] string SelectedKeyword,
    [property: Description("使用的行为档案 | Behavior profile applied")] string BehaviorProfile,
    [property: Description("浏览器键 | Browser profile key")] string BrowserKey,
    [property: Description("笔记 ID | Selected note identifier")] string? NoteId,
    [property: Description("笔记标题 | Selected note title")] string? NoteTitle,
    [property: Description("笔记链接 | Selected note URL")] string? NoteUrl,
    [property: Description("导航计划概要 | Navigation planned summary")] HumanizedActionSummary NavigationPlanned,
    [property: Description("导航执行概要 | Navigation executed summary")] HumanizedActionSummary NavigationExecuted,
    [property: Description("导航告警 | Navigation warnings")] string[] NavigationWarnings,
    [property: Description("互动结果 | Interaction results")] DiscoverFlowInteractions Interactions);

public sealed record DiscoverFlowInteractions(
    DiscoverInteractionResult Like,
    DiscoverInteractionResult Favorite,
    DiscoverInteractionResult Comment);

public sealed record DiscoverInteractionResult(string Status, string? Message = null)
{
    public static DiscoverInteractionResult Success(string? message = null) => new("success", message);

    public static DiscoverInteractionResult Skipped(string? message = null) => new("skipped", message);

    public static DiscoverInteractionResult Failed(string? message = null) => new("failed", message);
}




