using System.Globalization;
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
using HushOps.Servers.XiaoHongShu.Services.Notes;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace HushOps.Servers.XiaoHongShu.Tools;

[McpServerToolType]
public sealed class NoteCaptureTool
{
    private readonly INoteCaptureService _captureService;
    private readonly IAccountPortraitStore _portraitStore;
    private readonly IDefaultKeywordProvider _defaultKeywordProvider;
    private readonly IBrowserAutomationService _browserService;
    private readonly IHumanizedActionService _humanizedActionService;
    private readonly ILogger<NoteCaptureTool> _logger;

    public NoteCaptureTool(
        INoteCaptureService captureService,
        IAccountPortraitStore portraitStore,
        IDefaultKeywordProvider defaultKeywordProvider,
        IBrowserAutomationService browserService,
        IHumanizedActionService humanizedActionService,
        ILogger<NoteCaptureTool> logger)
    {
        _captureService = captureService ?? throw new ArgumentNullException(nameof(captureService));
        _portraitStore = portraitStore ?? throw new ArgumentNullException(nameof(portraitStore));
        _defaultKeywordProvider = defaultKeywordProvider ?? throw new ArgumentNullException(nameof(defaultKeywordProvider));
        _browserService = browserService ?? throw new ArgumentNullException(nameof(browserService));
        _humanizedActionService = humanizedActionService ?? throw new ArgumentNullException(nameof(humanizedActionService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [McpServerTool(Name = "xhs_note_capture"), Description("抓取笔记并导出记录 | Capture notes and export structured records")]
    public Task<OperationResult<NoteCaptureToolResult>> CaptureAsync(
        [Description("笔记抓取请求参数 | Request payload describing the capture operation")] NoteCaptureToolRequest request,
        [Description("取消执行的令牌 | Token that cancels the operation if triggered")] CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        return ServerToolExecutor.TryAsync(
            _logger,
            nameof(NoteCaptureTool),
            nameof(CaptureAsync),
            request.RequestId,
            async () => await ExecuteAsync(request, cancellationToken).ConfigureAwait(false),
            (ex, rid) => OperationResult<NoteCaptureToolResult>.Fail(ServerToolExecutor.MapExceptionCode(ex), ex.Message, BuildErrorMetadata(request, rid, ex)));
    }

    private async Task<OperationResult<NoteCaptureToolResult>> ExecuteAsync(NoteCaptureToolRequest request, CancellationToken cancellationToken)
    {
        var browserKey = NormalizeBrowserKey(request.BrowserKey);
        if (!_browserService.TryGetOpenProfile(browserKey, out var profile))
        {
            if (!IsUserProfile(browserKey))
            {
                throw new InvalidOperationException($"浏览器键 {browserKey} 未打开，请先调用 xhs_browser_open。");
            }

            profile = await _browserService.EnsureProfileAsync(browserKey, null, cancellationToken).ConfigureAwait(false);
        }

        var behaviorProfile = string.IsNullOrWhiteSpace(request.BehaviorProfile) ? "default" : request.BehaviorProfile.Trim();

        var keyword = await ResolveKeywordAsync(request, cancellationToken).ConfigureAwait(false);

        HumanizedActionPlan? navigationPlan = null;
        HumanizedActionOutcome? navigationOutcome = null;
        IReadOnlyDictionary<string, string>? navigationMetadata = null;

        try
        {
            navigationPlan = await _humanizedActionService.PrepareAsync(
                    new HumanizedActionRequest(keyword, request.PortraitId, null, true, browserKey, request.RequestId, behaviorProfile),
                    HumanizedActionKind.KeywordBrowse,
                    cancellationToken)
                .ConfigureAwait(false);

            navigationMetadata = navigationPlan.Metadata;

            if (request.RunHumanizedNavigation)
            {
                navigationOutcome = await _humanizedActionService.ExecuteAsync(navigationPlan, cancellationToken).ConfigureAwait(false);
                navigationMetadata = navigationOutcome.Metadata;

                if (!navigationOutcome.Success)
                {
                    _logger.LogWarning("[NoteCaptureTool] humanized navigation failed keyword={Keyword} status={Status}", keyword, navigationOutcome.Status);
                    var failureMetadata = MergeMetadata(request, keyword, navigationOutcome.Metadata, profile!, navigationMetadata);
                    return OperationResult<NoteCaptureToolResult>.Fail(
                        navigationOutcome.Status,
                        navigationOutcome.Message ?? "humanized navigation failed",
                        failureMetadata);
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var status = ServerToolExecutor.MapExceptionCode(ex);
            _logger.LogWarning(ex, "[NoteCaptureTool] navigation preparation failed keyword={Keyword} status={Status}", keyword, status);
            var failureMetadata = MergeMetadata(request, keyword, new Dictionary<string, string>(), profile!, navigationMetadata);
            failureMetadata["error"] = ex.Message;
            return OperationResult<NoteCaptureToolResult>.Fail(status, ex.Message, failureMetadata);
        }

        var context = new NoteCaptureContext(
            keyword,
            NormalizeTargetCount(request.TargetCount),
            NormalizeString(request.SortBy, "comprehensive"),
            NormalizeString(request.NoteType, "all"),
            NormalizeString(request.PublishTime, "all"),
            request.IncludeAnalytics,
            request.IncludeRaw,
            request.OutputDirectory ?? string.Empty);

        var captureResult = await _captureService.CaptureAsync(context, cancellationToken).ConfigureAwait(false);
        var metadata = MergeMetadata(request, keyword, captureResult.Metadata, profile!, navigationMetadata);

        var filterSelections = new NoteCaptureFilterSelections(
            NormalizeString(request.SortBy, "comprehensive"),
            NormalizeString(request.NoteType, "all"),
            NormalizeString(request.PublishTime, "all"));

        var basePlannedSummary = navigationPlan?.Script.ToSummary() ?? HumanizedActionSummary.Empty;
        var telemetry = HumanizedActionMetadataReader.Read(
            metadata,
            basePlannedSummary,
            request.RunHumanizedNavigation && navigationOutcome is { Success: true }
                ? basePlannedSummary
                : HumanizedActionSummary.Empty);
        var plannedSummary = telemetry.Planned;
        var executedSummary = telemetry.Executed;
        var humanizedActions = executedSummary.Actions.Count > 0 ? executedSummary.Actions : plannedSummary.Actions;
        var consistencyWarnings = telemetry.Warnings;

        var result = new NoteCaptureToolResult(
            keyword,
            captureResult.CsvPath,
            captureResult.RawPath,
            captureResult.Notes.Count,
            captureResult.Duration,
            request.RequestId ?? string.Empty,
            behaviorProfile,
            filterSelections,
            humanizedActions,
            plannedSummary,
            executedSummary,
            consistencyWarnings);

        _logger.LogInformation("[NoteCaptureTool] success keyword={Keyword} collected={Count} csv={Csv}", keyword, captureResult.Notes.Count, captureResult.CsvPath);

        return OperationResult<NoteCaptureToolResult>.Ok(result, metadata: metadata);
    }

    private async Task<string> ResolveKeywordAsync(NoteCaptureToolRequest request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            return request.Keyword.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.PortraitId))
        {
            var portrait = await _portraitStore.GetAsync(request.PortraitId, cancellationToken).ConfigureAwait(false);
            if (portrait is not null && portrait.Tags.Count > 0)
            {
                var index = Random.Shared.Next(portrait.Tags.Count);
                return portrait.Tags[index];
            }
        }

        var fallback = await _defaultKeywordProvider.GetDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(fallback))
        {
            return fallback.Trim();
        }

        throw new InvalidOperationException("鏃犳硶瑙ｆ瀽鍏抽敭璇嶏細璇锋彁渚?keyword銆佺敾鍍忔垨閰嶇疆榛樿鍏抽敭璇嶃€?");
    }

    private static int NormalizeTargetCount(int value)
        => Math.Clamp(value <= 0 ? 20 : value, 1, 200);

    private static string NormalizeString(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim().ToLowerInvariant();

    private static Dictionary<string, string> MergeMetadata(
        NoteCaptureToolRequest request,
        string keyword,
        IReadOnlyDictionary<string, string> captureMetadata,
        BrowserOpenResult profile,
        IReadOnlyDictionary<string, string>? navigationMetadata)
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["keyword"] = keyword,
            ["portraitId"] = request.PortraitId ?? string.Empty,
            ["includeRaw"] = request.IncludeRaw.ToString(),
            ["includeAnalytics"] = request.IncludeAnalytics.ToString(),
            ["requestId"] = request.RequestId ?? string.Empty,
            ["browserKey"] = profile.ProfileKey,
            ["browserPath"] = profile.ProfilePath,
            ["browserIsNew"] = profile.IsNewProfile.ToString(),
            ["browserFallback"] = profile.UsedFallbackPath.ToString(),
            ["browserAlreadyOpen"] = profile.AlreadyOpen.ToString(),
            ["browserAutoOpened"] = profile.AutoOpened.ToString(),
            ["behaviorProfile"] = string.IsNullOrWhiteSpace(request.BehaviorProfile) ? "default" : request.BehaviorProfile.Trim(),
            ["runHumanizedNavigation"] = request.RunHumanizedNavigation.ToString()
        };

        if (!string.IsNullOrWhiteSpace(profile.ProfileDirectoryName))
        {
            metadata["browserFolder"] = profile.ProfileDirectoryName!;
        }

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

        if (navigationMetadata is not null)
        {
            foreach (var pair in navigationMetadata)
            {
                metadata[pair.Key] = pair.Value;
            }
        }

        foreach (var pair in captureMetadata)
        {
            metadata[pair.Key] = pair.Value;
        }

        return metadata;
    }

    private static IReadOnlyDictionary<string, string> BuildErrorMetadata(NoteCaptureToolRequest request, string? requestId, Exception ex)
    {
        var resolvedKey = NormalizeBrowserKey(request.BrowserKey);
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["requestId"] = requestId ?? string.Empty,
            ["keyword"] = request.Keyword ?? string.Empty,
            ["portraitId"] = request.PortraitId ?? string.Empty,
            ["browserKey"] = resolvedKey,
            ["behaviorProfile"] = string.IsNullOrWhiteSpace(request.BehaviorProfile) ? "default" : request.BehaviorProfile.Trim(),
            ["runHumanizedNavigation"] = request.RunHumanizedNavigation.ToString(),
            ["error"] = ex.Message
        };

        return metadata;
    }

    private static string NormalizeBrowserKey(string? browserKey)
        => string.IsNullOrWhiteSpace(browserKey) ? BrowserOpenRequest.UserProfileKey : browserKey.Trim();

    private static bool IsUserProfile(string browserKey)
        => string.Equals(browserKey, BrowserOpenRequest.UserProfileKey, StringComparison.OrdinalIgnoreCase);
}

public sealed record NoteCaptureToolRequest(
    [property: Description("优先使用的关键词 | Preferred keyword to capture against")] string? Keyword,
    [property: Description("画像 ID，用于推荐关键词 | Portrait identifier for keyword fallback")] string? PortraitId,
    [property: Description("目标笔记数量上限 | Maximum number of notes to collect")] int TargetCount = 20,
    [property: Description("排序方式，默认 comprehensive | Sort strategy, defaults to comprehensive")] string? SortBy = "comprehensive",
    [property: Description("笔记类型过滤条件 | Note type filter")] string? NoteType = "all",
    [property: Description("发布时间过滤条件 | Publish time filter")] string? PublishTime = "all",
    [property: Description("是否输出分析字段 | Whether to include analytics columns")] bool IncludeAnalytics = false,
    [property: Description("是否同时保存原始 JSON | Whether to save raw JSON data")] bool IncludeRaw = false,
    [property: Description("输出目录，空值使用默认路径 | Output directory; defaults when empty")] string? OutputDirectory = null,
    [property: Description("浏览器键：user 表示用户配置，其它值映射为独立配置 | Browser key: 'user' for user profile, others map to isolated profiles")] string? BrowserKey = null,
    [property: Description("行为档案键，用于覆盖默认拟人化配置 | Behavior profile key overriding the default humanization profile")] string? BehaviorProfile = null,
    [property: Description("是否执行拟人化导航 | Whether to execute humanized navigation before capture")] bool RunHumanizedNavigation = true,
    [property: Description("请求 ID，便于审计和重试 | Request identifier for auditing and retries")] string? RequestId = null);

public sealed record NoteCaptureToolResult(
    [property: Description("实际使用的关键词 | Keyword used during capture")] string Keyword,
    [property: Description("CSV 输出路径 | CSV export path")] string CsvPath,
    [property: Description("原始 JSON 文件路径 | Raw JSON output path")] string? RawPath,
    [property: Description("采集到的笔记数量 | Number of notes collected")] int CollectedCount,
    [property: Description("操作耗时 | Duration of the operation")] TimeSpan Duration,
    [property: Description("对应的请求 ID | Associated request identifier")] string RequestId,
    [property: Description("实际使用的行为档案键 | Behavior profile applied for the run")] string BehaviorProfileId,
    [property: Description("过滤条件摘要 | Summary of applied filter selections")] NoteCaptureFilterSelections FilterSelections,
    [property: Description("执行的拟人化动作序列 | Executed humanized actions during navigation")] IReadOnlyList<string> HumanizedActions,
    [property: Description("计划阶段的拟人化动作概览 | Summary of planned humanized actions")] HumanizedActionSummary Planned,
    [property: Description("执行阶段的拟人化动作概览 | Summary of executed humanized actions")] HumanizedActionSummary Executed,
    [property: Description("一致性校验告警 | Consistency warnings recorded during execution")] IReadOnlyList<string>? ConsistencyWarnings = null);


public sealed record NoteCaptureFilterSelections(
    [property: Description("排序方式（归一化）| Normalized sort option")] string SortBy,
    [property: Description("笔记类型（归一化）| Normalized note type filter")] string NoteType,
    [property: Description("发布时间（归一化）| Normalized publish time filter")] string PublishTime);









