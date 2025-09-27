using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HushOps.Servers.XiaoHongShu.Infrastructure.ToolExecution;
using HushOps.Servers.XiaoHongShu.Services.Browser;
using HushOps.Servers.XiaoHongShu.Services.Notes;
using HushOps.Servers.XiaoHongShu.Services.Humanization.Behavior;
using Microsoft.Extensions.Logging;

namespace HushOps.Servers.XiaoHongShu.Services.Humanization;

/// <summary>
/// 中文：拟人化行为实现，负责选择关键词、节奏控制与浏览器交互。
/// </summary>
public sealed class HumanizedActionService : IHumanizedActionService
{
    private readonly IKeywordResolver _keywordResolver;
    private readonly IHumanDelayProvider _delayProvider;
    private readonly IBrowserAutomationService _browserAutomation;
    private readonly INoteEngagementService _noteEngagement;
    private readonly IBehaviorController _behaviorController;
    private readonly ILogger<HumanizedActionService> _logger;

    public HumanizedActionService(
        IKeywordResolver keywordResolver,
        IHumanDelayProvider delayProvider,
        IBrowserAutomationService browserAutomation,
        INoteEngagementService noteEngagement,
        IBehaviorController behaviorController,
        ILogger<HumanizedActionService> logger)
    {
        _keywordResolver = keywordResolver;
        _delayProvider = delayProvider;
        _browserAutomation = browserAutomation;
        _noteEngagement = noteEngagement;
        _behaviorController = behaviorController;
        _logger = logger;
    }

    public async Task<HumanizedActionOutcome> ExecuteAsync(HumanizedActionRequest request, HumanizedActionKind kind, CancellationToken cancellationToken)
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["kind"] = kind.ToString()
        };

        var requestedKey = string.IsNullOrWhiteSpace(request.BrowserKey) ? "user" : request.BrowserKey.Trim();
        metadata["browserKey"] = requestedKey;

        if (!_browserAutomation.TryGetOpenProfile(requestedKey, out var profile))
        {
            if (!string.Equals(requestedKey, BrowserOpenRequest.UserProfileKey, StringComparison.OrdinalIgnoreCase))
            {
                return HumanizedActionOutcome.Fail("ERR_BROWSER_KEY_NOT_FOUND", $"浏览器键 {requestedKey} 未打开，请先调用 xhs_browser_open。", metadata);
            }

            profile = await _browserAutomation.EnsureProfileAsync(requestedKey, null, cancellationToken).ConfigureAwait(false);
        }

        metadata["browserKey"] = profile!.ProfileKey;
        metadata["browserPath"] = profile.ProfilePath;
        if (!string.IsNullOrWhiteSpace(profile.ProfileDirectoryName))
        {
            metadata["browserFolder"] = profile.ProfileDirectoryName!;
        }
        metadata["browserAlreadyOpen"] = profile.AlreadyOpen.ToString();
        metadata["browserAutoOpened"] = profile.AutoOpened.ToString();

        if (profile.SessionMetadata is not null)
        {
            metadata["fingerprintHash"] = profile.SessionMetadata.FingerprintHash ?? string.Empty;
            metadata["fingerprintUserAgent"] = profile.SessionMetadata.UserAgent ?? string.Empty;
            metadata["fingerprintTimezone"] = profile.SessionMetadata.Timezone ?? string.Empty;
            metadata["fingerprintLanguage"] = profile.SessionMetadata.Language ?? string.Empty;
            metadata["fingerprintViewportWidth"] = profile.SessionMetadata.ViewportWidth?.ToString() ?? string.Empty;
            metadata["fingerprintViewportHeight"] = profile.SessionMetadata.ViewportHeight?.ToString() ?? string.Empty;
            metadata["fingerprintDeviceScale"] = profile.SessionMetadata.DeviceScaleFactor?.ToString("F1") ?? string.Empty;
            metadata["fingerprintIsMobile"] = profile.SessionMetadata.IsMobile?.ToString() ?? string.Empty;
            metadata["fingerprintHasTouch"] = profile.SessionMetadata.HasTouch?.ToString() ?? string.Empty;
            metadata["networkProxyId"] = profile.SessionMetadata.ProxyId ?? string.Empty;
            metadata["networkProxyAddress"] = profile.SessionMetadata.ProxyAddress ?? string.Empty;
            metadata["networkExitIp"] = profile.SessionMetadata.ExitIpAddress ?? string.Empty;
            if (profile.SessionMetadata.NetworkDelayMinMs.HasValue)
            {
                metadata["networkDelayMinMs"] = profile.SessionMetadata.NetworkDelayMinMs.Value.ToString();
            }
            if (profile.SessionMetadata.NetworkDelayMaxMs.HasValue)
            {
                metadata["networkDelayMaxMs"] = profile.SessionMetadata.NetworkDelayMaxMs.Value.ToString();
            }
            if (profile.SessionMetadata.NetworkRetryBaseDelayMs.HasValue)
            {
                metadata["networkRetryBaseDelayMs"] = profile.SessionMetadata.NetworkRetryBaseDelayMs.Value.ToString();
            }
            if (profile.SessionMetadata.NetworkMaxRetryAttempts.HasValue)
            {
                metadata["networkMaxRetryAttempts"] = profile.SessionMetadata.NetworkMaxRetryAttempts.Value.ToString();
            }
            if (profile.SessionMetadata.NetworkMitigationCount.HasValue)
            {
                metadata["networkMitigationCount"] = profile.SessionMetadata.NetworkMitigationCount.Value.ToString();
            }
        }

        var behaviorContext = new BehaviorActionContext(
            profile.ProfileKey,
            MapBehaviorActionType(kind),
            metadata,
            cancellationToken);

        var preTrace = await _behaviorController.ExecuteBeforeActionAsync(behaviorContext).ConfigureAwait(false);
        AppendBehaviorTrace(metadata, preTrace, "pre");

        var effectiveKey = profile.ProfileKey;

        try
        {
            var keyword = await _keywordResolver.ResolveAsync(request.Keyword, request.PortraitId, metadata, cancellationToken).ConfigureAwait(false);
            switch (kind)
            {
                case HumanizedActionKind.RandomBrowse:
                    await _browserAutomation.NavigateRandomAsync(effectiveKey, keyword, request.WaitForLoad, cancellationToken).ConfigureAwait(false);
                    break;
                case HumanizedActionKind.KeywordBrowse:
                    await _browserAutomation.NavigateKeywordAsync(effectiveKey, keyword, request.WaitForLoad, cancellationToken).ConfigureAwait(false);
                    break;
                case HumanizedActionKind.Like:
                    await _noteEngagement.LikeAsync(keyword, cancellationToken).ConfigureAwait(false);
                    break;
                case HumanizedActionKind.Favorite:
                    await _noteEngagement.FavoriteAsync(keyword, cancellationToken).ConfigureAwait(false);
                    break;
                case HumanizedActionKind.Comment:
                    if (string.IsNullOrWhiteSpace(request.CommentText))
                    {
                        return HumanizedActionOutcome.Fail("ERR_INVALID_ARGUMENT", "commentText 不能为空", metadata);
                    }
                    metadata["comment"] = request.CommentText!.Trim();
                    await _noteEngagement.CommentAsync(keyword, request.CommentText!.Trim(), cancellationToken).ConfigureAwait(false);
                    break;
                default:
                    return HumanizedActionOutcome.Fail("ERR_UNSUPPORTED", $"不支持的动作类型：{kind}", metadata);
            }

            await _delayProvider.DelayBetweenActionsAsync(cancellationToken).ConfigureAwait(false);
            var postSuccessTrace = await _behaviorController
                .ExecuteAfterActionAsync(behaviorContext, new BehaviorResult(true, "ok"))
                .ConfigureAwait(false);
            AppendBehaviorTrace(metadata, postSuccessTrace, "post");
            return HumanizedActionOutcome.Ok(metadata);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[HumanizedAction] 执行失败 kind={Kind}", kind);
            metadata["error"] = ex.Message;
            var failureTrace = await _behaviorController
                .ExecuteAfterActionAsync(behaviorContext, new BehaviorResult(false, ServerToolExecutor.MapExceptionCode(ex)))
                .ConfigureAwait(false);
            AppendBehaviorTrace(metadata, failureTrace, "post");
            return HumanizedActionOutcome.Fail(ServerToolExecutor.MapExceptionCode(ex), ex.Message, metadata);
        }
    }

    private static void AppendBehaviorTrace(IDictionary<string, string> metadata, BehaviorTrace trace, string stage)
    {
        if (metadata is null)
        {
            return;
        }

        metadata[$"behavior.{stage}.durationMs"] = trace.DurationMs.ToString("F0");
        foreach (var pair in trace.Extras)
        {
            metadata[$"behavior.{stage}.{pair.Key}"] = pair.Value;
        }
    }

    private static BehaviorActionType MapBehaviorActionType(HumanizedActionKind kind)
        => kind switch
        {
            HumanizedActionKind.RandomBrowse => BehaviorActionType.NavigateRandom,
            HumanizedActionKind.KeywordBrowse => BehaviorActionType.NavigateKeyword,
            HumanizedActionKind.Like => BehaviorActionType.Like,
            HumanizedActionKind.Favorite => BehaviorActionType.Favorite,
            HumanizedActionKind.Comment => BehaviorActionType.Comment,
            _ => BehaviorActionType.Unknown
        };
}

public interface IKeywordResolver
{
    Task<string> ResolveAsync(string? keyword, string? portraitId, IDictionary<string, string> metadata, CancellationToken cancellationToken);
}

public interface IHumanDelayProvider
{
    Task DelayBetweenActionsAsync(CancellationToken cancellationToken);
}
