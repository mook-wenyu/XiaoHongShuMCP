using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using HushOps.Servers.XiaoHongShu.Infrastructure.ToolExecution;
using HushOps.Servers.XiaoHongShu.Services.Browser;
using HushOps.Servers.XiaoHongShu.Services.Humanization.Interactions;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace HushOps.Servers.XiaoHongShu.Tools;

/// <summary>
/// 中文: 低级交互工具,提供直接的浏览器动作执行能力。
/// English: Low-level interaction tool for direct browser action execution.
/// </summary>
[McpServerToolType]
public sealed class LowLevelInteractionTool
{
    private readonly IBrowserAutomationService _browserAutomation;
    private readonly IHumanizedInteractionExecutor _executor;
    private readonly ILogger<LowLevelInteractionTool> _logger;

    public LowLevelInteractionTool(
        IBrowserAutomationService browserAutomation,
        IHumanizedInteractionExecutor executor,
        ILogger<LowLevelInteractionTool> logger)
    {
        _browserAutomation = browserAutomation ?? throw new ArgumentNullException(nameof(browserAutomation));
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [McpServerTool(Name = "ll_execute"), Description("执行单个低级拟人化动作 | Execute a single low-level humanized interaction action")]
    public async Task<OperationResult<InteractionStepResult>> ExecuteAsync(
        [Description("动作请求参数 | Action execution payload")] LowLevelActionRequest request,
        [Description("取消执行的令牌 | Cancellation token")] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var browserKey = string.IsNullOrWhiteSpace(request.BrowserKey) ? "user" : request.BrowserKey!.Trim();
        var behaviorProfile = string.IsNullOrWhiteSpace(request.BehaviorProfile) ? "default" : request.BehaviorProfile!.Trim();

        var pageContext = await _browserAutomation.EnsurePageContextAsync(browserKey, cancellationToken).ConfigureAwait(false);

        var action = HumanizedAction.Create(
            request.ActionType,
            request.Target ?? ActionLocator.Empty,
            request.Timing ?? HumanizedActionTiming.Default,
            request.Parameters ?? HumanizedActionParameters.Empty,
            behaviorProfile);

        await _executor.ExecuteAsync(pageContext.Page, action, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "[LowLevelInteractionTool] executed action={Action} browserKey={Key} profile={Profile}",
            request.ActionType,
            browserKey,
            behaviorProfile);

        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["browserKey"] = browserKey,
            ["behaviorProfile"] = behaviorProfile,
            ["actionType"] = request.ActionType.ToString(),
            ["fingerprintUserAgent"] = pageContext.Fingerprint.UserAgent
        };

        return OperationResult<InteractionStepResult>.Ok(
            new InteractionStepResult(request.ActionType.ToString(), null),
            "ok",
            metadata);
    }
}

/// <summary>
/// 中文: 低级动作请求参数。
/// English: Low-level action request parameters.
/// </summary>
public sealed record LowLevelActionRequest(
    [property: Description("浏览器键,user 表示用户配置 | Browser key")] string? BrowserKey,
    [property: Description("行为档案键,默认 default | Behavior profile key")] string? BehaviorProfile,
    [property: Description("动作类型 | Action type")] HumanizedActionType ActionType,
    [property: Description("目标定位线索 | Element locator hints")] ActionLocator? Target,
    [property: Description("附加参数,例如文本、滚动距离等 | Additional action parameters")] HumanizedActionParameters? Parameters,
    [property: Description("动作时间控制 | Timing configuration")] HumanizedActionTiming? Timing);