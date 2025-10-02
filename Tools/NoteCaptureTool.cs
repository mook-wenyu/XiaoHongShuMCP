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

    [McpServerTool(Name = "xhs_note_capture"), Description("拟人化抓取当前页面指定数量详细笔记（一个一个点击）并导出 CSV | Capture notes from current page with humanized clicking and export to CSV")]
    public Task<OperationResult<NoteCaptureToolResult>> CaptureAsync(
        [Description("笔记抓取请求参数 | Request payload describing the capture operation")] NoteCaptureToolRequest request,
        [Description("取消执行的令牌 | Token that cancels the operation if triggered")] CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var requestId = Guid.NewGuid().ToString("N");

        return ServerToolExecutor.TryAsync(
            _logger,
            nameof(NoteCaptureTool),
            nameof(CaptureAsync),
            requestId,
            async () => await ExecuteAsync(request, requestId, cancellationToken).ConfigureAwait(false),
            (ex, rid) => OperationResult<NoteCaptureToolResult>.Fail(ServerToolExecutor.MapExceptionCode(ex), ex.Message, new Dictionary<string, string>(BuildErrorMetadata(request, rid, ex), StringComparer.OrdinalIgnoreCase)));
    }

    private async Task<OperationResult<NoteCaptureToolResult>> ExecuteAsync(NoteCaptureToolRequest request, string requestId, CancellationToken cancellationToken)
    {
        // 硬编码默认值（破坏性变更）
        const bool DefaultIncludeAnalytics = false;
        const bool DefaultIncludeRaw = false;
        const string DefaultOutputDirectory = "./logs/note-capture";

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
        var selectedKeyword = keyword;
        var keywordCandidates = BuildKeywordCandidates(request, keyword);

        HumanizedActionPlan? navigationPlan = null;
        HumanizedActionOutcome? navigationOutcome = null;
        IReadOnlyDictionary<string, string>? navigationMetadata = null;

        try
        {
            navigationPlan = await _humanizedActionService.PrepareAsync(
                    new HumanizedActionRequest(keywordCandidates, request.PortraitId, null, browserKey, requestId, behaviorProfile),
                    HumanizedActionKind.KeywordBrowse,
                    cancellationToken)
                .ConfigureAwait(false);
            selectedKeyword = navigationPlan.ResolvedKeyword;

            navigationMetadata = navigationPlan.Metadata;

            navigationOutcome = await _humanizedActionService.ExecuteAsync(navigationPlan, cancellationToken).ConfigureAwait(false);
            navigationMetadata = navigationOutcome.Metadata;

            if (!navigationOutcome.Success)
            {
                _logger.LogWarning("[NoteCaptureTool] humanized navigation failed keyword={Keyword} status={Status}", keyword, navigationOutcome.Status);
                var failureMetadata = MergeMetadata(request, keyword, selectedKeyword, keywordCandidates, navigationOutcome.Metadata, profile!, navigationMetadata, requestId);
                return OperationResult<NoteCaptureToolResult>.Fail(
                    navigationOutcome.Status,
                    navigationOutcome.Message ?? "humanized navigation failed",
                    failureMetadata);
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
            var failureMetadata = MergeMetadata(request, keyword, selectedKeyword, keywordCandidates, new Dictionary<string, string>(), profile!, navigationMetadata, requestId);
            failureMetadata["error"] = ex.Message;
            return OperationResult<NoteCaptureToolResult>.Fail(status, ex.Message, failureMetadata);
        }

        var context = new NoteCaptureContext(
            keyword,
            NormalizeTargetCount(request.TargetCount),
            DefaultIncludeAnalytics,
            DefaultIncludeRaw,
            DefaultOutputDirectory);

        var captureResult = await _captureService.CaptureAsync(context, cancellationToken).ConfigureAwait(false);
        var metadata = MergeMetadata(request, keyword, selectedKeyword, keywordCandidates, captureResult.Metadata, profile!, navigationMetadata, requestId);

        var result = new NoteCaptureToolResult(
            keyword,
            captureResult.CsvPath,
            captureResult.Notes.Count);


        _logger.LogInformation("[NoteCaptureTool] success keyword={Keyword} collected={Count} csv={Csv}", keyword, captureResult.Notes.Count, captureResult.CsvPath);

        return OperationResult<NoteCaptureToolResult>.Ok(result, metadata: metadata);
    }

    private async Task<string> ResolveKeywordAsync(NoteCaptureToolRequest request, CancellationToken cancellationToken)
    {
        var fromRequest = PickKeywordFromCandidates(request.Keywords);
        if (!string.IsNullOrWhiteSpace(fromRequest))
        {
            return fromRequest!;
        }

        if (!string.IsNullOrWhiteSpace(request.PortraitId))
        {
            var portrait = await _portraitStore.GetAsync(request.PortraitId, cancellationToken).ConfigureAwait(false);
            if (portrait is not null && portrait.Tags.Count > 0)
            {
                return portrait.Tags[Random.Shared.Next(portrait.Tags.Count)];
            }
        }

        var fallback = await _defaultKeywordProvider.GetDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(fallback))
        {
            return fallback.Trim();
        }

        throw new InvalidOperationException("无法解析关键词：请提供关键词数组、画像或默认配置。");
    }

    private static int NormalizeTargetCount(int value)
        => Math.Clamp(value <= 0 ? 20 : value, 1, 200);

    private static string NormalizeString(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim().ToLowerInvariant();

    /// <summary>
    /// 中文：合并元数据，仅保留 requestId 字段。
    /// English: Merges metadata, keeping only requestId field.
    /// </summary>
    private static Dictionary<string, string> MergeMetadata(
        NoteCaptureToolRequest request,
        string keyword,
        string selectedKeyword,
        IReadOnlyList<string> keywordCandidates,
        IReadOnlyDictionary<string, string> captureMetadata,
        BrowserOpenResult profile,
        IReadOnlyDictionary<string, string>? navigationMetadata,
        string requestId)
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["requestId"] = requestId
        };
    }

    private static IReadOnlyList<string> BuildKeywordCandidates(NoteCaptureToolRequest request, string resolvedKeyword)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(resolvedKeyword))
        {
            set.Add(resolvedKeyword.Trim());
        }

        if (request.Keywords is not null)
        {
            foreach (var candidate in request.Keywords)
            {
                if (string.IsNullOrWhiteSpace(candidate))
                {
                    continue;
                }

                set.Add(candidate.Trim());
            }
        }

        return set.Count == 0 ? Array.Empty<string>() : set.ToArray();
    }

    private static string? PickKeywordFromCandidates(IReadOnlyList<string>? keywords)
    {
        if (keywords is null || keywords.Count == 0)
        {
            return null;
        }

        var candidates = keywords
            .Where(static k => !string.IsNullOrWhiteSpace(k))
            .Select(static k => k.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (candidates.Length == 0)
        {
            return null;
        }

        return candidates.Length == 1
            ? candidates[0]
            : candidates[Random.Shared.Next(candidates.Length)];
    }

    /// <summary>
    /// 中文：构建错误元数据，仅保留 requestId 字段。
    /// English: Builds error metadata, keeping only requestId field.
    /// </summary>
    private static IReadOnlyDictionary<string, string> BuildErrorMetadata(NoteCaptureToolRequest request, string? requestId, Exception ex)
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["requestId"] = requestId ?? string.Empty
        };
    }

    private static string NormalizeBrowserKey(string? browserKey)
        => string.IsNullOrWhiteSpace(browserKey) ? BrowserOpenRequest.UserProfileKey : browserKey.Trim();

    private static bool IsUserProfile(string browserKey)
        => string.Equals(browserKey, BrowserOpenRequest.UserProfileKey, StringComparison.OrdinalIgnoreCase);
}

public sealed record NoteCaptureToolRequest(
    [property: Description("候选关键词列表（用于筛选匹配笔记）| Candidate keywords for filtering notes")] string[]? Keywords = null,
    [property: Description("画像 ID，用于推荐关键词 | Portrait identifier for keyword fallback")] string PortraitId = "",
    [property: Description("目标笔记数量上限 | Maximum number of notes to collect")] int TargetCount = 20,
    [property: Description("浏览器键：user 表示用户配置，其它值映射为独立配置 | Browser key: 'user' for user profile, others map to isolated profiles")] string BrowserKey = "",
    [property: Description("行为档案键，用于覆盖默认拟人化配置 | Behavior profile key overriding the default humanization profile")] string BehaviorProfile = "");

public sealed record NoteCaptureToolResult(
    [property: Description("搜索关键词 | Search keyword")] string Keyword,
    [property: Description("CSV 文件路径 | CSV file path")] string CsvPath,
    [property: Description("采集笔记数量 | Number of notes collected")] int CollectedCount);























