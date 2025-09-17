using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HushOps.Core.AntiDetection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace XiaoHongShuMCP.Services.CommentFlow;

/// <summary>
/// 评论反馈协调器：汇聚网络反馈、触发反检测调优并输出审计日志。
/// </summary>
internal sealed class FeedbackCoordinator : IFeedbackCoordinator
{
    private readonly IUniversalApiMonitor _apiMonitor;
    private readonly XhsSettings.EndpointRetrySection _endpointRetry;
    private readonly IAntiDetectionOrchestrator _antiDetectionOrchestrator;
    private readonly ILogger<FeedbackCoordinator> _logger;
    private readonly string _contextKey;
    private readonly string[] _contextTags;

    public FeedbackCoordinator(
        IUniversalApiMonitor apiMonitor,
        IOptions<XhsSettings> settings,
        IAntiDetectionOrchestrator antiDetectionOrchestrator,
        ILogger<FeedbackCoordinator> logger)
    {
        _apiMonitor = apiMonitor;
        _endpointRetry = settings.Value.EndpointRetry ?? new XhsSettings.EndpointRetrySection();
        _antiDetectionOrchestrator = antiDetectionOrchestrator;
        _logger = logger;
        _contextKey = BuildContextKey(settings.Value);
        _contextTags = BuildContextTags(settings.Value);
    }

    /// <inheritdoc />
    public void Initialize(IPage page, IReadOnlyCollection<ApiEndpointType> endpoints)
    {
        var endpointSet = endpoints is HashSet<ApiEndpointType> set
            ? set
            : new HashSet<ApiEndpointType>(endpoints);

        _apiMonitor.SetupMonitor(page, endpointSet);
    }

    /// <inheritdoc />
    public void Reset(ApiEndpointType endpoint)
    {
        _apiMonitor.ClearMonitoredData(endpoint);
    }

    /// <inheritdoc />
    public async Task<ApiFeedback> ObserveAsync(ApiEndpointType endpoint, CancellationToken ct)
    {
        var attemptTimeout = TimeSpan.FromMilliseconds(Math.Max(1, _endpointRetry.AttemptTimeoutMs));
        var attempts = Math.Max(1, _endpointRetry.MaxRetries);

        for (var attempt = 0; attempt < attempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            var success = await _apiMonitor.WaitForResponsesAsync(endpoint, attemptTimeout, 1).ConfigureAwait(false);
            if (success)
            {
                break;
            }
        }

        var responses = _apiMonitor.GetRawResponses(endpoint);
        var message = responses.Count > 0 ? "已捕获 API 响应" : "未捕获 API 响应";
        var ids = responses.Select((_, index) => $"{endpoint}-{index}").ToList();
        var rawPayload = responses.LastOrDefault()?.ProcessedData;
        IReadOnlyDictionary<string, object?>? payload = rawPayload?.ToDictionary(k => k.Key, v => (object?)v.Value);

        await RecordAntiDetectionSignalAsync(endpoint, responses.Count, ct).ConfigureAwait(false);

        return new ApiFeedback(responses.Count > 0, message, ids, payload);
    }

    /// <inheritdoc />
    public void Audit(string operation, string keyword, FeedbackContext context)
    {
        _logger.LogInformation(
            "[Audit] 操作={Operation} | 关键词={Keyword} | DOM校验={Dom} | API确认={Api} | 耗时={Elapsed}ms | 备注={Extra}",
            operation,
            keyword,
            context.DomVerified,
            context.ApiConfirmed,
            (long)context.Duration.TotalMilliseconds,
            context.Extra ?? "-");
    }

    private async Task RecordAntiDetectionSignalAsync(ApiEndpointType endpoint, int responseCount, CancellationToken ct)
    {
        try
        {
            var stats = _apiMonitor.GetNetworkStats();
            var total = Math.Max(Math.Max(stats.TotalResponses, responseCount), 1);
            var humanScore = 1.0 - ((stats.Http429 + stats.Http403) / (double)total);
            var metrics = new Dictionary<string, double>
            {
                ["success2xx"] = stats.Success2xx,
                ["http429"] = stats.Http429,
                ["http403"] = stats.Http403,
                ["captcha"] = stats.CaptchaHints,
                ["endpointHits"] = stats.EndpointHits.TryGetValue(endpoint, out var hits) ? hits : 0
            };

            var signal = new AntiDetectionSignal
            {
                ContextId = $"{_contextKey}-{endpoint}",
                Workflow = endpoint.ToString(),
                ObservedAtUtc = DateTimeOffset.UtcNow,
                TotalInteractions = total,
                Http429 = stats.Http429,
                Http403 = stats.Http403,
                CaptchaChallenges = stats.CaptchaHints,
                P95LatencyMs = 0,
                P99LatencyMs = 0,
                HumanLikeScore = Math.Clamp(humanScore, 0, 1),
                JsInjectionFallbackUsed = false,
                Tags = _contextTags,
                Metrics = metrics
            };

            var adjustment = await _antiDetectionOrchestrator.RecordAsync(signal, ct).ConfigureAwait(false);
            _logger?.LogDebug(
                "[AntiDetect] endpoint={Endpoint} 节奏={Pacing} 轮换指纹={Rotate} 暂停={Pause} 原因={Reason}",
                endpoint,
                adjustment.PacingProfile,
                adjustment.RotateFingerprint,
                adjustment.PauseInteractions,
                adjustment.Reason);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger?.LogDebug(ex, "[AntiDetect] 记录反检测信号失败");
        }
    }

    private static string BuildContextKey(XhsSettings settings)
    {
        var userData = settings.BrowserSettings?.UserDataDir;
        var scope = string.IsNullOrWhiteSpace(userData)
            ? "default"
            : Path.GetFileName(userData.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return $"{Environment.MachineName}-{scope}";
    }

    private static string[] BuildContextTags(XhsSettings settings)
    {
        var tags = new List<string>
        {
            settings.BrowserSettings?.Channel ?? "chromium"
        };
        var userData = settings.BrowserSettings?.UserDataDir;
        if (!string.IsNullOrWhiteSpace(userData))
        {
            tags.Add(Path.GetFileName(userData.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)));
        }
        return tags.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
