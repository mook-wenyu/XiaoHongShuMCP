using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HushOps.Servers.XiaoHongShu.Configuration;
using HushOps.Servers.XiaoHongShu.Infrastructure.ToolExecution;
using HushOps.Servers.XiaoHongShu.Services.Browser;
using HushOps.Servers.XiaoHongShu.Services.Humanization.Behavior;
using HushOps.Servers.XiaoHongShu.Services.Humanization.Interactions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HushOps.Servers.XiaoHongShu.Services.Humanization;

/// <summary>
/// 中文：拟人化动作服务，负责生成计划、执行脚本并记录一致性指标。
/// English: Humanized action service that prepares plans, executes scripts, and captures consistency metrics.
/// </summary>
public sealed class HumanizedActionService : IHumanizedActionService
{
    private readonly IKeywordResolver _keywordResolver;
    private readonly IHumanDelayProvider _delayProvider;
    private readonly IBrowserAutomationService _browserAutomation;
    private readonly IHumanizedActionScriptBuilder _scriptBuilder;
    private readonly IHumanizedInteractionExecutor _executor;
    private readonly IBehaviorController _behaviorController;
    private readonly ISessionConsistencyInspector _consistencyInspector;
    private readonly HumanBehaviorOptions _behaviorOptions;
    private readonly ILogger<HumanizedActionService> _logger;

    public HumanizedActionService(
        IKeywordResolver keywordResolver,
        IHumanDelayProvider delayProvider,
        IBrowserAutomationService browserAutomation,
        IHumanizedActionScriptBuilder scriptBuilder,
        IHumanizedInteractionExecutor executor,
        IBehaviorController behaviorController,
        ISessionConsistencyInspector consistencyInspector,
        IOptions<HumanBehaviorOptions> behaviorOptions,
        ILogger<HumanizedActionService> logger)
    {
        _keywordResolver = keywordResolver ?? throw new ArgumentNullException(nameof(keywordResolver));
        _delayProvider = delayProvider ?? throw new ArgumentNullException(nameof(delayProvider));
        _browserAutomation = browserAutomation ?? throw new ArgumentNullException(nameof(browserAutomation));
        _scriptBuilder = scriptBuilder ?? throw new ArgumentNullException(nameof(scriptBuilder));
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _behaviorController = behaviorController ?? throw new ArgumentNullException(nameof(behaviorController));
        _consistencyInspector = consistencyInspector ?? throw new ArgumentNullException(nameof(consistencyInspector));
        if (behaviorOptions is null)
        {
            throw new ArgumentNullException(nameof(behaviorOptions));
        }

        _behaviorOptions = behaviorOptions.Value ?? new HumanBehaviorOptions();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<HumanizedActionPlan> PrepareAsync(HumanizedActionRequest request, HumanizedActionKind kind, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["kind"] = kind.ToString(),
            ["requestId"] = request.RequestId ?? string.Empty
        };

        var requestedKey = string.IsNullOrWhiteSpace(request.BrowserKey)
            ? BrowserOpenRequest.UserProfileKey
            : request.BrowserKey.Trim();
        metadata["browserKey"] = requestedKey;

        var behaviorProfileKey = string.IsNullOrWhiteSpace(request.BehaviorProfile)
            ? _behaviorOptions.DefaultProfile
            : request.BehaviorProfile.Trim();
        metadata["behaviorProfile"] = behaviorProfileKey;

        if (!_browserAutomation.TryGetOpenProfile(requestedKey, out var profile))
        {
            if (!string.Equals(requestedKey, BrowserOpenRequest.UserProfileKey, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"浏览器键 {requestedKey} 未打开，请先调用 xhs_browser_open。");
            }

            profile = await _browserAutomation.EnsureProfileAsync(requestedKey, null, cancellationToken).ConfigureAwait(false);
        }

        profile = profile!.EnsureValid();

        metadata["browserKey"] = profile.ProfileKey;
        metadata["browserPath"] = profile.ProfilePath;
        metadata["browserAlreadyOpen"] = profile.AlreadyOpen.ToString();
        metadata["browserAutoOpened"] = profile.AutoOpened.ToString();
        metadata["browserIsNew"] = profile.IsNewProfile.ToString();
        metadata["browserFallback"] = profile.UsedFallbackPath.ToString();
        if (!string.IsNullOrWhiteSpace(profile.ProfileDirectoryName))
        {
            metadata["browserFolder"] = profile.ProfileDirectoryName!;
        }

        if (profile.SessionMetadata is { } session)
        {
            metadata["fingerprintHash"] = session.FingerprintHash ?? string.Empty;
            metadata["fingerprintUserAgent"] = session.UserAgent ?? string.Empty;
            metadata["fingerprintTimezone"] = session.Timezone ?? string.Empty;
            metadata["fingerprintLanguage"] = session.Language ?? string.Empty;
            metadata["fingerprintViewportWidth"] = session.ViewportWidth?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            metadata["fingerprintViewportHeight"] = session.ViewportHeight?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            metadata["fingerprintDeviceScale"] = session.DeviceScaleFactor?.ToString("F1", CultureInfo.InvariantCulture) ?? string.Empty;
            metadata["fingerprintIsMobile"] = session.IsMobile?.ToString() ?? string.Empty;
            metadata["fingerprintHasTouch"] = session.HasTouch?.ToString() ?? string.Empty;
            metadata["networkProxyId"] = session.ProxyId ?? string.Empty;
            metadata["networkProxyAddress"] = session.ProxyAddress ?? string.Empty;
            metadata["networkExitIp"] = session.ExitIpAddress ?? string.Empty;
            if (session.NetworkDelayMinMs.HasValue)
            {
                metadata["networkDelayMinMs"] = session.NetworkDelayMinMs.Value.ToString(CultureInfo.InvariantCulture);
            }

            if (session.NetworkDelayMaxMs.HasValue)
            {
                metadata["networkDelayMaxMs"] = session.NetworkDelayMaxMs.Value.ToString(CultureInfo.InvariantCulture);
            }

            if (session.NetworkRetryBaseDelayMs.HasValue)
            {
                metadata["networkRetryBaseDelayMs"] = session.NetworkRetryBaseDelayMs.Value.ToString(CultureInfo.InvariantCulture);
            }

            if (session.NetworkMaxRetryAttempts.HasValue)
            {
                metadata["networkMaxRetryAttempts"] = session.NetworkMaxRetryAttempts.Value.ToString(CultureInfo.InvariantCulture);
            }

            if (session.NetworkMitigationCount.HasValue)
            {
                metadata["networkMitigationCount"] = session.NetworkMitigationCount.Value.ToString(CultureInfo.InvariantCulture);
            }
        }

        var normalizedKeywords = NormalizeKeywords(request.Keywords);
        if (normalizedKeywords.Count > 0)
        {
            metadata["keywords.candidates"] = string.Join(",", normalizedKeywords);
        }

        var normalizedRequest = new HumanizedActionRequest(
            normalizedKeywords,
            request.PortraitId,
            request.CommentText,
            profile.ProfileKey,
            request.RequestId,
            behaviorProfileKey);

        // 判断操作是否需要关键词解析
        // NavigateExplore、LikeCurrentNote、FavoriteCurrentNote、CommentCurrentNote、ScrollBrowse、KeywordBrowse 不需要关键词
        var requiresKeyword = kind is HumanizedActionKind.SearchKeyword
            or HumanizedActionKind.SelectNote
            or HumanizedActionKind.RandomBrowse;

        string resolvedKeyword;
        if (requiresKeyword)
        {
            // 需要关键词的操作：执行解析（可能抛出异常）
            resolvedKeyword = await _keywordResolver.ResolveAsync(normalizedKeywords, request.PortraitId, metadata, cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            // 不需要关键词的操作：使用空字符串
            resolvedKeyword = string.Empty;
            metadata["keyword.source"] = "not_required";
        }

        metadata["resolvedKeyword"] = resolvedKeyword;
        metadata["selectedKeyword"] = resolvedKeyword;
        metadata["keywords.selected"] = resolvedKeyword;

        var script = _scriptBuilder.Build(normalizedRequest, kind, resolvedKeyword);
        metadata["script.actionCount"] = script.Actions.Count.ToString(CultureInfo.InvariantCulture);
        metadata["script.actions"] = string.Join(",", script.Actions.Select(action => action.Type.ToString()));
        var plannedSummary = script.ToSummary();
        metadata["humanized.plan.count"] = plannedSummary.Count.ToString(CultureInfo.InvariantCulture);
        metadata["humanized.plan.actions"] = string.Join(",", plannedSummary.Actions);
        metadata["plan.actionCount"] = plannedSummary.Count.ToString(CultureInfo.InvariantCulture);
        metadata["plan.actions"] = metadata["humanized.plan.actions"];
        for (var i = 0; i < script.Actions.Count; i++)
        {
            metadata[$"script.actions.{i}"] = script.Actions[i].Type.ToString();
        }

        for (var i = 0; i < plannedSummary.Actions.Count; i++)
        {
            metadata[$"humanized.plan.actions.{i}"] = plannedSummary.Actions[i];
            metadata[$"plan.actions.{i}"] = plannedSummary.Actions[i];
        }

        var behaviorProfileOptions = ResolveBehaviorProfile(behaviorProfileKey);

        return HumanizedActionPlan.Create(kind, normalizedRequest, resolvedKeyword, profile, behaviorProfileOptions, script, metadata);
    }

    public async Task<HumanizedActionOutcome> ExecuteAsync(HumanizedActionPlan plan, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var metadata = new Dictionary<string, string>(plan.Metadata, StringComparer.OrdinalIgnoreCase)
        {
            ["kind"] = plan.Kind.ToString(),
            ["browserKey"] = plan.BrowserKey,
            ["behaviorProfile"] = plan.BehaviorProfile
        };

        if (!metadata.ContainsKey("requestId"))
        {
            metadata["requestId"] = plan.Request.RequestId ?? string.Empty;
        }

        var plannedSummary = plan.Script.ToSummary();
        metadata["humanized.plan.count"] = plannedSummary.Count.ToString(CultureInfo.InvariantCulture);
        metadata["humanized.plan.actions"] = string.Join(",", plannedSummary.Actions);
        for (var i = 0; i < plannedSummary.Actions.Count; i++)
        {
            metadata[$"humanized.plan.actions.{i}"] = plannedSummary.Actions[i];
        }

        var behaviorContext = new BehaviorActionContext(plan.Profile.ProfileKey, MapBehaviorActionType(plan.Kind), metadata, cancellationToken);
        var preTrace = await _behaviorController.ExecuteBeforeActionAsync(behaviorContext).ConfigureAwait(false);
        AppendBehaviorTrace(metadata, preTrace, "pre");

        try
        {
            metadata["resolvedKeyword"] = plan.ResolvedKeyword;
            metadata["selectedKeyword"] = plan.ResolvedKeyword;
            metadata["keywords.selected"] = plan.ResolvedKeyword;
            metadata["script.actionCount"] = plannedSummary.Count.ToString(CultureInfo.InvariantCulture);
            metadata["script.actions"] = string.Join(",", plannedSummary.Actions);
            metadata["plan.actionCount"] = plannedSummary.Count.ToString(CultureInfo.InvariantCulture);
            metadata["plan.actions"] = metadata["script.actions"];
            for (var i = 0; i < plannedSummary.Actions.Count; i++)
            {
                metadata[$"script.actions.{i}"] = plannedSummary.Actions[i];
                metadata[$"plan.actions.{i}"] = plannedSummary.Actions[i];
            }

            metadata["execution.status"] = "pending";
            metadata["execution.actionCount"] = "0";
            metadata["execution.actions"] = string.Empty;
            metadata["humanized.execute.status"] = "pending";
            metadata["humanized.execute.count"] = "0";
            metadata["humanized.execute.actions"] = string.Empty;

            var pageContext = await _browserAutomation.EnsurePageContextAsync(plan.Profile.ProfileKey, cancellationToken)
                .ConfigureAwait(false);

            metadata["fingerprintUserAgent"] = pageContext.Fingerprint.UserAgent;
            metadata["fingerprintTimezone"] = pageContext.Fingerprint.Timezone;
            metadata["fingerprintLanguage"] = pageContext.Fingerprint.Language;
            metadata["fingerprintViewportWidth"] = pageContext.Fingerprint.ViewportWidth.ToString(CultureInfo.InvariantCulture);
            metadata["fingerprintViewportHeight"] = pageContext.Fingerprint.ViewportHeight.ToString(CultureInfo.InvariantCulture);
            metadata["fingerprintIsMobile"] = pageContext.Fingerprint.IsMobile.ToString();
            metadata["fingerprintHasTouch"] = pageContext.Fingerprint.HasTouch.ToString();
            metadata["networkProxyAddress"] = pageContext.Network.ProxyAddress ?? string.Empty;
            metadata["networkDelayMinMs"] = pageContext.Network.DelayMinMs.ToString(CultureInfo.InvariantCulture);
            metadata["networkDelayMaxMs"] = pageContext.Network.DelayMaxMs.ToString(CultureInfo.InvariantCulture);
            metadata["networkMitigationCount"] = pageContext.Network.MitigationCount.ToString(CultureInfo.InvariantCulture);

            var report = await _consistencyInspector.InspectAsync(pageContext, plan.BehaviorProfileOptions, cancellationToken)
                .ConfigureAwait(false);
            metadata["consistency.uaMatch"] = report.UserAgentMatch.ToString();
            metadata["consistency.languageMatch"] = report.LanguageMatch.ToString();
            metadata["consistency.timezoneMatch"] = report.TimezoneMatch.ToString();
            metadata["consistency.viewportMatch"] = report.ViewportMatch.ToString();
            metadata["consistency.isMobileMatch"] = report.IsMobileMatch.ToString();
            metadata["consistency.proxyConfigured"] = report.ProxyConfigured.ToString();
            metadata["consistency.proxyRequirementSatisfied"] = report.ProxyRequirementSatisfied.ToString();
            metadata["consistency.gpuInfoAvailable"] = report.GpuInfoAvailable.ToString();
            metadata["consistency.gpuRequirementSatisfied"] = report.GpuRequirementSatisfied.ToString();
            metadata["consistency.gpuSuspicious"] = report.GpuSuspicious.ToString();
            metadata["consistency.automationDetected"] = report.AutomationIndicatorsDetected.ToString();
            metadata["consistency.pageUserAgent"] = report.PageUserAgent ?? string.Empty;
            metadata["consistency.pageLanguage"] = report.PageLanguage ?? string.Empty;
            metadata["consistency.pageTimezone"] = report.PageTimezone ?? string.Empty;
            metadata["consistency.pageViewportWidth"] = report.PageViewportWidth.ToString(CultureInfo.InvariantCulture);
            metadata["consistency.pageViewportHeight"] = report.PageViewportHeight.ToString(CultureInfo.InvariantCulture);
            metadata["consistency.hardwareConcurrency"] = report.HardwareConcurrency.ToString(CultureInfo.InvariantCulture);
            metadata["consistency.deviceMemoryGb"] = report.DeviceMemoryGb?.ToString("F1", CultureInfo.InvariantCulture) ?? string.Empty;
            metadata["consistency.platform"] = report.Platform ?? string.Empty;
            metadata["consistency.vendor"] = report.Vendor ?? string.Empty;
            metadata["consistency.gpuVendor"] = report.GpuVendor ?? string.Empty;
            metadata["consistency.gpuRenderer"] = report.GpuRenderer ?? string.Empty;
            metadata["consistency.connection.effectiveType"] = report.ConnectionEffectiveType ?? string.Empty;
            metadata["consistency.connection.downlinkMbps"] = report.ConnectionDownlinkMbps?.ToString("F2", CultureInfo.InvariantCulture) ?? string.Empty;
            metadata["consistency.connection.rttMs"] = report.ConnectionRttMs?.ToString("F0", CultureInfo.InvariantCulture) ?? string.Empty;
            metadata["consistency.connection.saveData"] = report.ConnectionSaveDataEnabled.ToString();
            for (var i = 0; i < report.Warnings.Count; i++)
            {
                metadata[$"consistency.warning.{i}"] = report.Warnings[i];
            }

            await _executor.ExecuteAsync(pageContext.Page, plan.Script, cancellationToken).ConfigureAwait(false);

            if (plan.Kind == HumanizedActionKind.SelectNote)
            {
                await CaptureNoteDetailAsync(pageContext, metadata, cancellationToken).ConfigureAwait(false);
            }

            var executedSummary = plan.Script.ToSummary();
            metadata["execution.actionCount"] = executedSummary.Count.ToString(CultureInfo.InvariantCulture);
            metadata["execution.actions"] = string.Join(",", executedSummary.Actions);
            metadata["execution.status"] = "success";
            metadata["humanized.execute.count"] = executedSummary.Count.ToString(CultureInfo.InvariantCulture);
            metadata["humanized.execute.actions"] = string.Join(",", executedSummary.Actions);
            metadata["humanized.execute.status"] = "success";
            for (var i = 0; i < executedSummary.Actions.Count; i++)
            {
                metadata[$"execution.actions.{i}"] = executedSummary.Actions[i];
                metadata[$"humanized.execute.actions.{i}"] = executedSummary.Actions[i];
            }

            await _delayProvider.DelayBetweenActionsAsync(cancellationToken).ConfigureAwait(false);

            var postTrace = await _behaviorController.ExecuteAfterActionAsync(behaviorContext, new BehaviorResult(true, "ok"))
                .ConfigureAwait(false);
            AppendBehaviorTrace(metadata, postTrace, "post");

            return HumanizedActionOutcome.Ok(metadata);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var status = ServerToolExecutor.MapExceptionCode(ex);
            _logger.LogWarning(ex, "[HumanizedAction] 执行失败 kind={Kind} status={Status}", plan.Kind, status);
            metadata["error"] = ex.Message;
            metadata["execution.status"] = "failed";
            metadata["execution.actionCount"] = "0";
            metadata["execution.actions"] = string.Empty;
            metadata["humanized.execute.status"] = "failed";
            metadata["humanized.execute.count"] = "0";
            metadata["humanized.execute.actions"] = string.Empty;

            var failureTrace = await _behaviorController.ExecuteAfterActionAsync(behaviorContext, new BehaviorResult(false, status))
                .ConfigureAwait(false);
            AppendBehaviorTrace(metadata, failureTrace, "post");

            return HumanizedActionOutcome.Fail(status, ex.Message, metadata);
        }
    }

    public async Task<HumanizedActionOutcome> ExecuteAsync(HumanizedActionRequest request, HumanizedActionKind kind, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var plan = await PrepareAsync(request, kind, cancellationToken).ConfigureAwait(false);
            return await ExecuteAsync(plan, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (InvalidOperationException ex)
        {
            var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["kind"] = kind.ToString(),
                ["browserKey"] = string.IsNullOrWhiteSpace(request.BrowserKey) ? BrowserOpenRequest.UserProfileKey : request.BrowserKey.Trim(),
                ["behaviorProfile"] = string.IsNullOrWhiteSpace(request.BehaviorProfile) ? _behaviorOptions.DefaultProfile : request.BehaviorProfile.Trim(),
                ["requestId"] = request.RequestId ?? string.Empty,
            };

            metadata["error"] = ex.Message;

            return HumanizedActionOutcome.Fail("ERR_BROWSER_KEY_NOT_FOUND", ex.Message, metadata);
        }
        catch (Exception ex)
        {
            var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["kind"] = kind.ToString(),
                ["browserKey"] = string.IsNullOrWhiteSpace(request.BrowserKey) ? BrowserOpenRequest.UserProfileKey : request.BrowserKey.Trim(),
                ["behaviorProfile"] = string.IsNullOrWhiteSpace(request.BehaviorProfile) ? _behaviorOptions.DefaultProfile : request.BehaviorProfile.Trim(),
                ["requestId"] = request.RequestId ?? string.Empty,
                ["error"] = ex.Message
            };

            var status = ServerToolExecutor.MapExceptionCode(ex);
            _logger.LogWarning(ex, "[HumanizedAction] 准备阶段失败 kind={Kind} status={Status}", kind, status);
            return HumanizedActionOutcome.Fail(status, ex.Message, metadata);
        }
    }

    private static void AppendBehaviorTrace(IDictionary<string, string> metadata, BehaviorTrace trace, string stage)
    {
        if (metadata is null)
        {
            return;
        }

        metadata[$"behavior.{stage}.durationMs"] = trace.DurationMs.ToString("F0", CultureInfo.InvariantCulture);
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
            HumanizedActionKind.NavigateExplore => BehaviorActionType.NavigateExplore,
            HumanizedActionKind.SearchKeyword => BehaviorActionType.SearchKeyword,
            HumanizedActionKind.SelectNote => BehaviorActionType.SelectNote,
            HumanizedActionKind.LikeCurrentNote => BehaviorActionType.LikeCurrentNote,
            HumanizedActionKind.FavoriteCurrentNote => BehaviorActionType.FavoriteCurrentNote,
            HumanizedActionKind.CommentCurrentNote => BehaviorActionType.CommentCurrentNote,
            _ => BehaviorActionType.Unknown
        };

    private static async Task CaptureNoteDetailAsync(
        BrowserPageContext pageContext,
        IDictionary<string, string> metadata,
        CancellationToken cancellationToken)
    {
        if (pageContext is null || metadata is null)
        {
            return;
        }

        const string script = """
() => {
    const url = window.location && typeof window.location.href === 'string' ? window.location.href : '';
    const titleElement = document.querySelector('[data-note-title]')
        || document.querySelector('h1')
        || document.querySelector('h2');
    const title = titleElement ? (titleElement.textContent || '').trim() : '';
    const noteIdAttr = document.querySelector('[data-note-id]')?.getAttribute('data-note-id') || '';
    return { url, title, noteId: noteIdAttr };
}
""";

        try
        {
            var snapshot = await pageContext.Page.EvaluateAsync<NoteDetailSnapshot>(script).ConfigureAwait(false);

            metadata["detail.captureStatus"] = "ok";

            if (snapshot is null)
            {
                metadata["detail.captureStatus"] = "empty";
                return;
            }

            if (!string.IsNullOrWhiteSpace(snapshot.Url))
            {
                metadata["detail.url"] = snapshot.Url;
            }

            var noteId = snapshot.NoteId;
            if (string.IsNullOrWhiteSpace(noteId) && !string.IsNullOrWhiteSpace(snapshot.Url))
            {
                noteId = ExtractNoteIdFromUrl(snapshot.Url);
            }

            if (!string.IsNullOrWhiteSpace(noteId))
            {
                metadata["detail.noteId"] = noteId.Trim();
            }

            if (!string.IsNullOrWhiteSpace(snapshot.Title))
            {
                metadata["detail.title"] = snapshot.Title.Trim();
            }
        }
        catch (Exception ex)
        {
            metadata["detail.captureStatus"] = "failed";
            metadata["detail.captureError"] = ex.Message;
        }
    }

    private static string ExtractNoteIdFromUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }

        try
        {
            var uri = Uri.TryCreate(url, UriKind.Absolute, out var absolute)
                ? absolute
                : Uri.TryCreate(url, UriKind.Relative, out var relative)
                    ? relative
                    : null;

            var path = uri?.IsAbsoluteUri == true ? uri.AbsolutePath : uri?.OriginalString ?? url;
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
            {
                return string.Empty;
            }

            var lastSegment = segments[^1];
            var trimmed = lastSegment.Split('?', '#')[0];
            return trimmed;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// 中文：笔记详情页快照。
    /// English: Note detail page snapshot.
    /// </summary>
    private sealed record NoteDetailSnapshot(string? Url, string? NoteId, string? Title);

    private HumanBehaviorProfileOptions ResolveBehaviorProfile(string profileKey)
    {
        if (!string.IsNullOrWhiteSpace(profileKey) && _behaviorOptions.Profiles.TryGetValue(profileKey, out var configured))
        {
            return configured;
        }

        if (_behaviorOptions.Profiles.TryGetValue(_behaviorOptions.DefaultProfile, out var fallback))
        {
            return fallback;
        }

        return HumanBehaviorProfileOptions.CreateDefault();
    }

    private static IReadOnlyList<string> NormalizeKeywords(IReadOnlyList<string> keywords)
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
}

public interface IKeywordResolver
{
    Task<string> ResolveAsync(IReadOnlyList<string> keywords, string? portraitId, IDictionary<string, string> metadata, CancellationToken cancellationToken);
}

public interface IHumanDelayProvider
{
    Task DelayBetweenActionsAsync(CancellationToken cancellationToken);
}





