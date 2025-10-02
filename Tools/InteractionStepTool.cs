using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HushOps.Servers.XiaoHongShu.Infrastructure.ToolExecution;
using HushOps.Servers.XiaoHongShu.Services.Browser;
using HushOps.Servers.XiaoHongShu.Services.Humanization;
using HushOps.Servers.XiaoHongShu.Services.Humanization.Interactions;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace HushOps.Servers.XiaoHongShu.Tools;

[McpServerToolType]
public sealed class InteractionStepTool
{
    private readonly IBrowserAutomationService _browserAutomation;
    private readonly IHumanizedActionService _humanizedActionService;
    private readonly IHumanizedInteractionExecutor _executor;
    private readonly ILogger<InteractionStepTool> _logger;

    public InteractionStepTool(
        IBrowserAutomationService browserAutomation,
        IHumanizedActionService humanizedActionService,
        IHumanizedInteractionExecutor executor,
        ILogger<InteractionStepTool> logger)
    {
        _browserAutomation = browserAutomation ?? throw new ArgumentNullException(nameof(browserAutomation));
        _humanizedActionService = humanizedActionService ?? throw new ArgumentNullException(nameof(humanizedActionService));
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [McpServerTool(Name = "xhs_navigate_explore"), Description("导航到发现页（点击导航栏发现按钮） | Navigate to explore page (discover feed)")]
    public async Task<OperationResult<InteractionStepResult>> NavigateExploreAsync(
        [Description("浏览器键，user 表示用户配置 | Browser key: 'user' for user profile")] string browserKey = "",
        [Description("行为档案键，默认 default | Behavior profile key")] string behaviorProfile = "",
        [Description("取消执行的令牌 | Cancellation token")] CancellationToken cancellationToken = default)
    {
        var request = new HumanizedActionRequest(
            Keywords: Array.Empty<string>(),
            PortraitId: null,
            CommentText: null,
            BrowserKey: browserKey ?? "user",
            RequestId: null,
            BehaviorProfile: behaviorProfile ?? "default");

        var outcome = await _humanizedActionService.ExecuteAsync(request, HumanizedActionKind.NavigateExplore, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("[InteractionStepTool] navigate_explore success={Success} status={Status}", outcome.Success, outcome.Status);

        var metadata = outcome.Metadata is Dictionary<string, string> dict
            ? dict
            : new Dictionary<string, string>(outcome.Metadata, StringComparer.OrdinalIgnoreCase);

        return outcome.Success
            ? OperationResult<InteractionStepResult>.Ok(new InteractionStepResult("NavigateExplore", "已导航到发现页"), outcome.Status, metadata)
            : OperationResult<InteractionStepResult>.Fail(outcome.Status, outcome.Message ?? "导航失败", metadata);
    }

    [McpServerTool(Name = "xhs_search_keyword"), Description("在搜索框输入关键词并搜索 | Search for keyword in search box")]
    public async Task<OperationResult<InteractionStepResult>> SearchKeywordAsync(
        [Description("搜索关键词 | Search keyword")] string keyword,
        [Description("浏览器键，user 表示用户配置 | Browser key: 'user' for user profile")] string browserKey = "",
        [Description("行为档案键，默认 default | Behavior profile key")] string behaviorProfile = "",
        [Description("取消执行的令牌 | Cancellation token")] CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyword);

        var request = new HumanizedActionRequest(
            Keywords: new[] { keyword },
            PortraitId: null,
            CommentText: null,
            BrowserKey: browserKey ?? "user",
            RequestId: null,
            BehaviorProfile: behaviorProfile ?? "default");

        var outcome = await _humanizedActionService.ExecuteAsync(request, HumanizedActionKind.SearchKeyword, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("[InteractionStepTool] search_keyword keyword={Keyword} success={Success} status={Status}", keyword, outcome.Success, outcome.Status);

        var metadata = outcome.Metadata is Dictionary<string, string> dict
            ? dict
            : new Dictionary<string, string>(outcome.Metadata, StringComparer.OrdinalIgnoreCase);

        return outcome.Success
            ? OperationResult<InteractionStepResult>.Ok(new InteractionStepResult("SearchKeyword", $"已搜索关键词: {keyword}"), outcome.Status, metadata)
            : OperationResult<InteractionStepResult>.Fail(outcome.Status, outcome.Message ?? "搜索失败", metadata);
    }

    [McpServerTool(Name = "xhs_select_note"), Description("根据关键词数组选择笔记（命中任意关键词即成功） | Select note by keyword array matching")]
    public async Task<OperationResult<InteractionStepResult>> SelectNoteAsync(
        [Description("关键词数组，命中任意关键词即选中 | Keyword array for note matching")] string[] keywords,
        [Description("浏览器键，user 表示用户配置 | Browser key: 'user' for user profile")] string browserKey = "",
        [Description("行为档案键，默认 default | Behavior profile key")] string behaviorProfile = "",
        [Description("取消执行的令牌 | Cancellation token")] CancellationToken cancellationToken = default)
    {
        if (keywords == null || keywords.Length == 0)
        {
            return OperationResult<InteractionStepResult>.Fail("ERR_INVALID_PARAMS", "关键词数组不能为空");
        }

        var request = new HumanizedActionRequest(
            Keywords: keywords,
            PortraitId: null,
            CommentText: null,
            BrowserKey: browserKey ?? "user",
            RequestId: null,
            BehaviorProfile: behaviorProfile ?? "default");

        var outcome = await _humanizedActionService.ExecuteAsync(request, HumanizedActionKind.SelectNote, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("[InteractionStepTool] select_note keywords={Keywords} success={Success} status={Status}",
            string.Join(",", keywords), outcome.Success, outcome.Status);

        var metadata = outcome.Metadata is Dictionary<string, string> dict
            ? dict
            : new Dictionary<string, string>(outcome.Metadata, StringComparer.OrdinalIgnoreCase);

        return outcome.Success
            ? OperationResult<InteractionStepResult>.Ok(new InteractionStepResult("SelectNote", $"已根据关键词选择笔记: {string.Join(",", keywords)}"), outcome.Status, metadata)
            : OperationResult<InteractionStepResult>.Fail(outcome.Status, outcome.Message ?? "选择笔记失败", metadata);
    }

    [McpServerTool(Name = "xhs_like_current"), Description("点赞当前打开的笔记 | Like the currently open note")]
    public async Task<OperationResult<InteractionStepResult>> LikeCurrentNoteAsync(
        [Description("浏览器键，user 表示用户配置 | Browser key: 'user' for user profile")] string browserKey = "",
        [Description("行为档案键，默认 default | Behavior profile key")] string behaviorProfile = "",
        [Description("取消执行的令牌 | Cancellation token")] CancellationToken cancellationToken = default)
    {
        var request = new HumanizedActionRequest(
            Keywords: Array.Empty<string>(),
            PortraitId: null,
            CommentText: null,
            BrowserKey: browserKey ?? "user",
            RequestId: null,
            BehaviorProfile: behaviorProfile ?? "default");

        var outcome = await _humanizedActionService.ExecuteAsync(request, HumanizedActionKind.LikeCurrentNote, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("[InteractionStepTool] like_current success={Success} status={Status}", outcome.Success, outcome.Status);

        var metadata = outcome.Metadata is Dictionary<string, string> dict
            ? dict
            : new Dictionary<string, string>(outcome.Metadata, StringComparer.OrdinalIgnoreCase);

        return outcome.Success
            ? OperationResult<InteractionStepResult>.Ok(new InteractionStepResult("LikeCurrentNote", "已点赞当前笔记"), outcome.Status, metadata)
            : OperationResult<InteractionStepResult>.Fail(outcome.Status, outcome.Message ?? "点赞失败", metadata);
    }

    [McpServerTool(Name = "xhs_favorite_current"), Description("收藏当前打开的笔记 | Favorite the currently open note")]
    public async Task<OperationResult<InteractionStepResult>> FavoriteCurrentNoteAsync(
        [Description("浏览器键，user 表示用户配置 | Browser key: 'user' for user profile")] string browserKey = "",
        [Description("行为档案键，默认 default | Behavior profile key")] string behaviorProfile = "",
        [Description("取消执行的令牌 | Cancellation token")] CancellationToken cancellationToken = default)
    {
        var request = new HumanizedActionRequest(
            Keywords: Array.Empty<string>(),
            PortraitId: null,
            CommentText: null,
            BrowserKey: browserKey ?? "user",
            RequestId: null,
            BehaviorProfile: behaviorProfile ?? "default");

        var outcome = await _humanizedActionService.ExecuteAsync(request, HumanizedActionKind.FavoriteCurrentNote, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("[InteractionStepTool] favorite_current success={Success} status={Status}", outcome.Success, outcome.Status);

        var metadata = outcome.Metadata is Dictionary<string, string> dict
            ? dict
            : new Dictionary<string, string>(outcome.Metadata, StringComparer.OrdinalIgnoreCase);

        return outcome.Success
            ? OperationResult<InteractionStepResult>.Ok(new InteractionStepResult("FavoriteCurrentNote", "已收藏当前笔记"), outcome.Status, metadata)
            : OperationResult<InteractionStepResult>.Fail(outcome.Status, outcome.Message ?? "收藏失败", metadata);
    }

    [McpServerTool(Name = "xhs_comment_current"), Description("评论当前打开的笔记 | Comment on the currently open note")]
    public async Task<OperationResult<InteractionStepResult>> CommentCurrentNoteAsync(
        [Description("评论文本 | Comment text")] string commentText,
        [Description("浏览器键，user 表示用户配置 | Browser key: 'user' for user profile")] string browserKey = "",
        [Description("行为档案键，默认 default | Behavior profile key")] string behaviorProfile = "",
        [Description("取消执行的令牌 | Cancellation token")] CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commentText);

        var request = new HumanizedActionRequest(
            Keywords: Array.Empty<string>(),
            PortraitId: null,
            CommentText: commentText,
            BrowserKey: browserKey ?? "user",
            RequestId: null,
            BehaviorProfile: behaviorProfile ?? "default");

        var outcome = await _humanizedActionService.ExecuteAsync(request, HumanizedActionKind.CommentCurrentNote, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("[InteractionStepTool] comment_current text={Text} success={Success} status={Status}",
            commentText, outcome.Success, outcome.Status);

        var metadata = outcome.Metadata is Dictionary<string, string> dict
            ? dict
            : new Dictionary<string, string>(outcome.Metadata, StringComparer.OrdinalIgnoreCase);

        return outcome.Success
            ? OperationResult<InteractionStepResult>.Ok(new InteractionStepResult("CommentCurrentNote", $"已评论: {commentText}"), outcome.Status, metadata)
            : OperationResult<InteractionStepResult>.Fail(outcome.Status, outcome.Message ?? "评论失败", metadata);
    }

    [McpServerTool(Name = "xhs_scroll_browse"), Description("拟人化滚动浏览当前页面 | Humanized scroll browsing on current page")]
    public async Task<OperationResult<InteractionStepResult>> ScrollBrowseAsync(
        [Description("浏览器键，user 表示用户配置 | Browser key: 'user' for user profile")] string browserKey = "",
        [Description("行为档案键，默认 default | Behavior profile key")] string behaviorProfile = "",
        [Description("取消执行的令牌 | Cancellation token")] CancellationToken cancellationToken = default)
    {
        var request = new HumanizedActionRequest(
            Keywords: Array.Empty<string>(),
            PortraitId: null,
            CommentText: null,
            BrowserKey: browserKey ?? "user",
            RequestId: null,
            BehaviorProfile: behaviorProfile ?? "default");

        var outcome = await _humanizedActionService.ExecuteAsync(request, HumanizedActionKind.ScrollBrowse, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("[InteractionStepTool] scroll_browse success={Success} status={Status}", outcome.Success, outcome.Status);

        var metadata = outcome.Metadata is Dictionary<string, string> dict
            ? dict
            : new Dictionary<string, string>(outcome.Metadata, StringComparer.OrdinalIgnoreCase);

        return outcome.Success
            ? OperationResult<InteractionStepResult>.Ok(new InteractionStepResult("ScrollBrowse", "已拟人化滚动浏览当前页面"), outcome.Status, metadata)
            : OperationResult<InteractionStepResult>.Fail(outcome.Status, outcome.Message ?? "滚动浏览失败", metadata);
    }
}

public sealed record InteractionStepResult(
    [property: Description("执行的动作类型 | Executed action type")] string ActionType,
    [property: Description("动作说明 | Action description")] string? Description);
