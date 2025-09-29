using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HushOps.Servers.XiaoHongShu.Infrastructure.ToolExecution;
using HushOps.Servers.XiaoHongShu.Services.Humanization;
using HushOps.Servers.XiaoHongShu.Services.Humanization.Interactions;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace HushOps.Servers.XiaoHongShu.Tools;

[McpServerToolType]
public sealed class HumanizedActionTool
{
    private readonly IHumanizedActionService _service;
    private readonly ILogger<HumanizedActionTool> _logger;

    public HumanizedActionTool(IHumanizedActionService service, ILogger<HumanizedActionTool> logger)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [McpServerTool(Name = "xhs_random_browse"), Description("执行随机浏览动作 | Trigger a random browsing action with humanized pacing")]
    public Task<OperationResult<HumanizedActionToolResult>> RandomBrowseAsync(
        [Description("拟人化请求参数 | Humanized action request payload")] HumanizedActionToolRequest request,
        [Description("取消令牌 | Token that cancels the operation if triggered")] CancellationToken cancellationToken = default)
        => ExecuteAsync(HumanizedActionKind.RandomBrowse, request, cancellationToken);

    [McpServerTool(Name = "xhs_keyword_browse"), Description("按关键词执行浏览 | Browse notes using the provided keyword with pacing")]
    public Task<OperationResult<HumanizedActionToolResult>> KeywordBrowseAsync(
        [Description("拟人化请求参数 | Humanized action request payload")] HumanizedActionToolRequest request,
        [Description("取消令牌 | Token that cancels the operation if triggered")] CancellationToken cancellationToken = default)
        => ExecuteAsync(HumanizedActionKind.KeywordBrowse, request, cancellationToken);

    [McpServerTool(Name = "xhs_like"), Description("点赞笔记 | Like notes via the humanized workflow")]
    public Task<OperationResult<HumanizedActionToolResult>> LikeAsync(
        [Description("拟人化请求参数 | Humanized action request payload")] HumanizedActionToolRequest request,
        [Description("取消令牌 | Token that cancels the operation if triggered")] CancellationToken cancellationToken = default)
        => ExecuteAsync(HumanizedActionKind.Like, request, cancellationToken);

    [McpServerTool(Name = "xhs_favorite"), Description("收藏笔记 | Save notes to favorites with humanized pacing")]
    public Task<OperationResult<HumanizedActionToolResult>> FavoriteAsync(
        [Description("拟人化请求参数 | Humanized action request payload")] HumanizedActionToolRequest request,
        [Description("取消令牌 | Token that cancels the operation if triggered")] CancellationToken cancellationToken = default)
        => ExecuteAsync(HumanizedActionKind.Favorite, request, cancellationToken);

    [McpServerTool(Name = "xhs_comment"), Description("发表评论 | Post a comment with humanized pacing")]
    public Task<OperationResult<HumanizedActionToolResult>> CommentAsync(
        [Description("拟人化请求参数 | Humanized action request payload")] HumanizedActionToolRequest request,
        [Description("取消令牌 | Token that cancels the operation if triggered")] CancellationToken cancellationToken = default)
        => ExecuteAsync(HumanizedActionKind.Comment, request, cancellationToken);

    private async Task<OperationResult<HumanizedActionToolResult>> ExecuteAsync(HumanizedActionKind kind, HumanizedActionToolRequest? request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var browserKey = string.IsNullOrWhiteSpace(request.BrowserKey) ? "user" : request.BrowserKey.Trim();
        var behaviorProfile = string.IsNullOrWhiteSpace(request.BehaviorProfile) ? "default" : request.BehaviorProfile.Trim();

        var actionRequest = new HumanizedActionRequest(
            request.Keyword,
            request.PortraitId,
            request.CommentText,
            request.WaitForLoad,
            browserKey,
            request.RequestId,
            behaviorProfile);

        HumanizedActionPlan? plan = null;

        try
        {
            plan = await _service.PrepareAsync(actionRequest, kind, cancellationToken).ConfigureAwait(false);
            var outcome = await _service.ExecuteAsync(plan, cancellationToken).ConfigureAwait(false);

            var metadata = new Dictionary<string, string>(plan.Metadata, StringComparer.OrdinalIgnoreCase)
            {
                ["kind"] = kind.ToString(),
                ["requestId"] = request.RequestId ?? string.Empty,
                ["browserKey"] = plan.BrowserKey,
                ["behaviorProfile"] = plan.BehaviorProfile
            };

            foreach (var pair in outcome.Metadata)
            {
                metadata[pair.Key] = pair.Value;
            }

            var basePlannedSummary = plan.Script.ToSummary();
            var telemetry = HumanizedActionMetadataReader.Read(
                metadata,
                basePlannedSummary,
                outcome.Success ? basePlannedSummary : HumanizedActionSummary.Empty);
            var plannedSummary = telemetry.Planned;
            var executedSummary = telemetry.Executed;
            var actions = executedSummary.Actions.Count > 0 ? executedSummary.Actions : plannedSummary.Actions;
            var warnings = telemetry.Warnings;
            var result = new HumanizedActionToolResult(
                kind.ToString(),
                outcome.Message,
                request.RequestId,
                plan.ResolvedKeyword,
                plan.BehaviorProfile,
                actions,
                plannedSummary,
                executedSummary,
                warnings);

            if (outcome.Success)
            {
                _logger.LogInformation("[HumanizedActionTool] kind={Kind} status={Status}", kind, outcome.Status);
                return OperationResult<HumanizedActionToolResult>.Ok(result, outcome.Status, metadata);
            }

            _logger.LogWarning("[HumanizedActionTool] kind={Kind} status={Status} message={Message}", kind, outcome.Status, outcome.Message);
            return OperationResult<HumanizedActionToolResult>.Fail(outcome.Status, outcome.Message, metadata);
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
                ["requestId"] = request.RequestId ?? string.Empty,
                ["browserKey"] = browserKey,
                ["behaviorProfile"] = behaviorProfile,
                ["error"] = ex.Message
            };

            if (plan is not null)
            {
                foreach (var pair in plan.Metadata)
                {
                    metadata[pair.Key] = pair.Value;
                }
            }

            _logger.LogWarning(ex, "[HumanizedActionTool] kind={Kind} status={Status} message={Message}", kind, status, ex.Message);
            return OperationResult<HumanizedActionToolResult>.Fail(status, ex.Message, metadata);
        }
    }
}

public sealed record HumanizedActionToolRequest(
    [property: Description("优先使用的关键词 | Preferred keyword; falls back to portrait or defaults when empty")] string? Keyword,
    [property: Description("画像 ID，用于推荐关键词 | Portrait identifier for resolving fallback keywords")] string? PortraitId,
    [property: Description("评论内容，仅在评论动作时有效 | Comment text used for comment actions")] string? CommentText,
    [property: Description("是否等待页面加载完成 | Whether to wait for page load completion")] bool WaitForLoad = true,
    [property: Description("浏览器键，user 表示用户配置 | Browser key: 'user' for the user profile, others map to isolated profiles")] string? BrowserKey = null,
    [property: Description("请求 ID，便于审计与幂等 | Request identifier for logging and idempotency")] string? RequestId = null,
    [property: Description("行为档案键，覆盖默认拟人化配置 | Behavior profile key overriding the default humanization profile")] string? BehaviorProfile = null);

public sealed record HumanizedActionToolResult(
    [property: Description("执行的动作类型 | Executed action type")] string Kind,
    [property: Description("附加说明或错误信息 | Additional message or error details")] string? Message,
    [property: Description("关联的请求 ID | Associated request identifier")] string? RequestId,
    [property: Description("解析后的关键词 | Resolved keyword used during execution")] string ResolvedKeyword,
    [property: Description("使用的行为档案 | Behavior profile applied for the run")] string BehaviorProfile,
    [property: Description("执行的动作序列 | Ordered list of executed actions")] IReadOnlyList<string> Actions,
    [property: Description("计划阶段的动作概览 | Summary of planned actions")] HumanizedActionSummary Planned,
    [property: Description("执行阶段的动作概览 | Summary of executed actions")] HumanizedActionSummary Executed,
    [property: Description("一致性告警列表 | Consistency warnings emitted during execution")] IReadOnlyList<string> Warnings);
