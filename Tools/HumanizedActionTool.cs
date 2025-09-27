using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using HushOps.Servers.XiaoHongShu.Infrastructure.ToolExecution;
using HushOps.Servers.XiaoHongShu.Services.Humanization;
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

    [McpServerTool(Name = "xhs_humanized_random_browse"), Description("执行随机浏览动作 | Trigger a random browsing action with humanized pacing")]
    public Task<OperationResult<HumanizedActionToolResult>> RandomBrowseAsync(
        [Description("拟人化请求参数 | Humanized action request payload")] HumanizedActionToolRequest request,
        [Description("取消执行的令牌 | Token that cancels the operation if triggered")] CancellationToken cancellationToken = default)
        => ExecuteAsync(HumanizedActionKind.RandomBrowse, request, cancellationToken);

    [McpServerTool(Name = "xhs_humanized_keyword_browse"), Description("按关键词执行浏览动作 | Browse notes using the provided keyword with pacing")] 
    public Task<OperationResult<HumanizedActionToolResult>> KeywordBrowseAsync(
        [Description("拟人化请求参数 | Humanized action request payload")] HumanizedActionToolRequest request,
        [Description("取消执行的令牌 | Token that cancels the operation if triggered")] CancellationToken cancellationToken = default)
        => ExecuteAsync(HumanizedActionKind.KeywordBrowse, request, cancellationToken);

    [McpServerTool(Name = "xhs_humanized_like"), Description("点赞笔记 | Like notes via the humanized workflow")]
    public Task<OperationResult<HumanizedActionToolResult>> LikeAsync(
        [Description("拟人化请求参数 | Humanized action request payload")] HumanizedActionToolRequest request,
        [Description("取消执行的令牌 | Token that cancels the operation if triggered")] CancellationToken cancellationToken = default)
        => ExecuteAsync(HumanizedActionKind.Like, request, cancellationToken);

    [McpServerTool(Name = "xhs_humanized_favorite"), Description("收藏笔记 | Save notes to favorites with humanized pacing")]
    public Task<OperationResult<HumanizedActionToolResult>> FavoriteAsync(
        [Description("拟人化请求参数 | Humanized action request payload")] HumanizedActionToolRequest request,
        [Description("取消执行的令牌 | Token that cancels the operation if triggered")] CancellationToken cancellationToken = default)
        => ExecuteAsync(HumanizedActionKind.Favorite, request, cancellationToken);

    [McpServerTool(Name = "xhs_humanized_comment"), Description("发表评论 | Post a comment with humanized pacing")]
    public Task<OperationResult<HumanizedActionToolResult>> CommentAsync(
        [Description("拟人化请求参数 | Humanized action request payload")] HumanizedActionToolRequest request,
        [Description("取消执行的令牌 | Token that cancels the operation if triggered")] CancellationToken cancellationToken = default)
        => ExecuteAsync(HumanizedActionKind.Comment, request, cancellationToken);

    private async Task<OperationResult<HumanizedActionToolResult>> ExecuteAsync(HumanizedActionKind kind, HumanizedActionToolRequest? request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var actionRequest = new HumanizedActionRequest(
            request.Keyword,
            request.PortraitId,
            request.CommentText,
            request.WaitForLoad,
            string.IsNullOrWhiteSpace(request.BrowserKey) ? "user" : request.BrowserKey.Trim(),
            request.RequestId);

        var outcome = await _service.ExecuteAsync(actionRequest, kind, cancellationToken).ConfigureAwait(false);

        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["kind"] = kind.ToString(),
            ["requestId"] = request.RequestId ?? string.Empty,
            ["browserKey"] = string.IsNullOrWhiteSpace(request.BrowserKey) ? "user" : request.BrowserKey.Trim()
        };

        foreach (var pair in outcome.Metadata)
        {
            metadata[pair.Key] = pair.Value;
        }

        var result = new HumanizedActionToolResult(kind.ToString(), outcome.Message, request.RequestId);

        if (outcome.Success)
        {
            _logger.LogInformation("[HumanizedActionTool] kind={Kind} status={Status}", kind, outcome.Status);
            return OperationResult<HumanizedActionToolResult>.Ok(result, outcome.Status, metadata);
        }

        _logger.LogWarning("[HumanizedActionTool] kind={Kind} status={Status} message={Message}", kind, outcome.Status, outcome.Message);
        return OperationResult<HumanizedActionToolResult>.Fail(outcome.Status, outcome.Message, metadata);
    }
}

public sealed record HumanizedActionToolRequest(
    [property: Description("优先使用的关键词，空值时回退画像或默认配置 | Preferred keyword; falls back to portrait or default settings when empty")] string? Keyword,
    [property: Description("画像标识，用于推导备用关键词 | Portrait identifier for resolving fallback keywords")] string? PortraitId,
    [property: Description("评论内容，仅在评论动作时生效 | Comment text used for comment actions")] string? CommentText,
    [property: Description("是否等待页面加载完成 | Whether to wait for page load completion")] bool WaitForLoad = true,
    [property: Description("浏览器键，user 表示用户配置，其他值对应独立配置 | Browser key: 'user' for the user profile, others map to isolated profiles")] string? BrowserKey = null,
    [property: Description("请求 ID，便于日志与幂等控制 | Request identifier for logging and idempotency")] string? RequestId = null);

public sealed record HumanizedActionToolResult(
    [property: Description("执行的动作类型 | Executed action type")] string Kind,
    [property: Description("额外说明或错误信息 | Additional message or error details")] string? Message,
    [property: Description("对应的请求 ID | Associated request identifier")] string? RequestId);
