using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HushOps.Servers.XiaoHongShu.Infrastructure.Telemetry;
using HushOps.Servers.XiaoHongShu.Infrastructure.ToolExecution;
using HushOps.Servers.XiaoHongShu.Services.Browser;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace HushOps.Servers.XiaoHongShu.Tools;

[McpServerToolType]
public sealed class BrowserTool
{
    private readonly IBrowserAutomationService _browserService;
    private readonly ILogger<BrowserTool> _logger;
    private readonly MetricsRecorder _metricsRecorder;

    public BrowserTool(
        IBrowserAutomationService browserService,
        ILogger<BrowserTool> logger,
        MetricsRecorder metricsRecorder)
    {
        _browserService = browserService ?? throw new ArgumentNullException(nameof(browserService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _metricsRecorder = metricsRecorder ?? throw new ArgumentNullException(nameof(metricsRecorder));
    }

    [McpServerTool(Name = "browser_open"), Description("打开或复用浏览器配置 | Open or reuse a browser profile")]
    public Task<OperationResult<BrowserOpenResult>> OpenAsync(
        [Description("浏览器打开请求参数 | Request payload describing the browser profile to open")] BrowserOpenToolRequest request,
        [Description("取消执行的令牌 | Token that cancels the operation if triggered")] CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var requestId = Guid.NewGuid().ToString("N");

        return ServerToolExecutor.TryAsync(
            _logger,
            nameof(BrowserTool),
            nameof(OpenAsync),
            requestId,
            async () =>
            {
                var openRequest = BuildRequest(request);
                var result = await _browserService.OpenAsync(openRequest, cancellationToken).ConfigureAwait(false);
                var status = result.AlreadyOpen ? "already_open" : "ok";
                return OperationResult<BrowserOpenResult>.Ok(result, status, BuildSuccessMetadata(request, result, requestId));
            },
            (ex, rid) => OperationResult<BrowserOpenResult>.Fail(
                ServerToolExecutor.MapExceptionCode(ex),
                BuildDetailedErrorMessage(ex),
                BuildErrorMetadata(request, rid, ex)),
            _metricsRecorder);
    }

    private static BrowserOpenRequest BuildRequest(BrowserOpenToolRequest request)
    {
        var normalizedKey = NormalizeProfileKey(request.ProfileKey);

        if (IsUserProfile(normalizedKey))
        {
            // user 模式：自动探测系统浏览器，使用 CDP 连接（高性能）
            return BrowserOpenRequest.UseUserProfile(null, normalizedKey, null);
        }

        // 独立模式：创建隔离配置文件夹
        return BrowserOpenRequest.ForIsolated(normalizedKey);
    }

    private static string NormalizeProfileKey(string? profileKey)
        => string.IsNullOrWhiteSpace(profileKey) ? BrowserOpenRequest.UserProfileKey : profileKey.Trim();

    private static bool IsUserProfile(string profileKey)
        => string.Equals(profileKey, BrowserOpenRequest.UserProfileKey, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 中文：构建成功元数据，仅保留 requestId 字段。
    /// English: Builds success metadata, keeping only requestId field.
    /// </summary>
    private static Dictionary<string, string> BuildSuccessMetadata(BrowserOpenToolRequest request, BrowserOpenResult result, string requestId)
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["requestId"] = requestId
        };
    }

    /// <summary>
    /// 中文：构建错误元数据，仅保留 requestId 字段。
    /// English: Builds error metadata, keeping only requestId field.
    /// </summary>
    private static Dictionary<string, string> BuildErrorMetadata(BrowserOpenToolRequest request, string? requestId, Exception ex)
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["requestId"] = requestId ?? string.Empty
        };
    }

    /// <summary>
    /// 中文：构建详细的错误消息，包含异常类型、消息和内部异常信息。
    /// English: Builds detailed error message including exception type, message, and inner exception details.
    /// </summary>
    private static string BuildDetailedErrorMessage(Exception ex)
    {
        var message = $"[{ex.GetType().Name}] {ex.Message}";

        if (ex.InnerException != null)
        {
            message += $" | Inner: [{ex.InnerException.GetType().Name}] {ex.InnerException.Message}";
        }

        // 对于 AggregateException,展示所有内部异常
        if (ex is AggregateException aggEx)
        {
            var innerMessages = string.Join(" | ", aggEx.InnerExceptions.Select(e => $"[{e.GetType().Name}] {e.Message}"));
            message += $" | Aggregated: {innerMessages}";
        }

        return message;
    }
}

/// <summary>
/// 中文：浏览器打开工具请求参数。
/// English: Browser open tool request parameters.
/// </summary>
/// <param name="ProfileKey">
/// 浏览器配置键：
/// - 留空或 "user"：使用 CDP 连接系统默认浏览器（Chrome/Edge），启动快（~100-200ms），保留登录状态
/// - 其他值（如 "account1"）：创建独立配置文件夹在 storage/browser-profiles/{ProfileKey}/，完全隔离
/// 
/// Browser profile key:
/// - Empty or "user": Use CDP connection to system default browser (Chrome/Edge), fast startup (~100-200ms), preserves login state
/// - Other values (e.g., "account1"): Create isolated profile folder at storage/browser-profiles/{ProfileKey}/, complete isolation
/// </param>
public sealed record BrowserOpenToolRequest(
    [property: Description("浏览器配置键：留空或 'user' 使用系统浏览器（CDP 连接），其他值创建独立配置 | Browser profile key: empty or 'user' for system browser (CDP), other values for isolated profiles")] 
    string ProfileKey = "");
