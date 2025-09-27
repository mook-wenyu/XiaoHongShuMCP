using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
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

    public BrowserTool(IBrowserAutomationService browserService, ILogger<BrowserTool> logger)
    {
        _browserService = browserService ?? throw new ArgumentNullException(nameof(browserService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [McpServerTool(Name = "xhs_browser_open"), Description("打开或复用浏览器配置 | Open or reuse a browser profile")]
    public Task<OperationResult<BrowserOpenResult>> OpenAsync(
        [Description("浏览器打开请求参数 | Request payload describing the browser profile to open")] BrowserOpenToolRequest request,
        [Description("取消执行的令牌 | Token that cancels the operation if triggered")] CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        return ServerToolExecutor.TryAsync(
            _logger,
            nameof(BrowserTool),
            nameof(OpenAsync),
            request.RequestId,
            async () =>
            {
                var openRequest = BuildRequest(request);
                var result = await _browserService.OpenAsync(openRequest, cancellationToken).ConfigureAwait(false);
                var status = result.AlreadyOpen ? "already_open" : "ok";
                return OperationResult<BrowserOpenResult>.Ok(result, status, BuildSuccessMetadata(request, result));
            },
            (ex, rid) => OperationResult<BrowserOpenResult>.Fail(
                ServerToolExecutor.MapExceptionCode(ex),
                ex.Message,
                BuildErrorMetadata(request, rid, ex)));
    }

    private static BrowserOpenRequest BuildRequest(BrowserOpenToolRequest request)
    {
        var normalizedKey = NormalizeProfileKey(request.ProfileKey);
        var normalizedPath = NormalizePath(request.ProfilePath);

        if (IsUserProfile(normalizedKey))
        {
            return BrowserOpenRequest.UseUserProfile(normalizedPath, normalizedKey);
        }

        if (normalizedPath is not null)
        {
            throw new ArgumentException("非 user 模式不允许设置 profilePath。", nameof(request.ProfilePath));
        }

        return BrowserOpenRequest.UseIsolatedProfile(normalizedKey, normalizedKey);
    }

    private static string? NormalizePath(string? path)
        => string.IsNullOrWhiteSpace(path) ? null : path.Trim();

    private static string NormalizeProfileKey(string? profileKey)
        => string.IsNullOrWhiteSpace(profileKey) ? BrowserOpenRequest.UserProfileKey : profileKey.Trim();

    private static bool IsUserProfile(string profileKey)
        => string.Equals(profileKey, BrowserOpenRequest.UserProfileKey, StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyDictionary<string, string> BuildSuccessMetadata(BrowserOpenToolRequest request, BrowserOpenResult result)
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["requestId"] = request.RequestId ?? string.Empty,
            ["mode"] = result.Kind.ToString(),
            ["profilePath"] = result.ProfilePath,
            ["profileKey"] = result.ProfileKey,
            ["isNewProfile"] = result.IsNewProfile.ToString(),
            ["usedFallbackPath"] = result.UsedFallbackPath.ToString(),
            ["alreadyOpen"] = result.AlreadyOpen.ToString(),
            ["autoOpened"] = result.AutoOpened.ToString()
        };

        if (!string.IsNullOrWhiteSpace(request.ProfilePath))
        {
            metadata["inputProfilePath"] = request.ProfilePath.Trim();
        }

        var resolvedFolder = string.IsNullOrWhiteSpace(result.ProfileDirectoryName)
            ? BrowserOpenRequest.UserProfileKey
            : result.ProfileDirectoryName!;
        metadata["folderName"] = resolvedFolder;
        metadata["resolvedFolderName"] = resolvedFolder;

        if (result.SessionMetadata is not null)
        {
            metadata["fingerprintHash"] = result.SessionMetadata.FingerprintHash ?? string.Empty;
            metadata["fingerprintUserAgent"] = result.SessionMetadata.UserAgent ?? string.Empty;
            metadata["fingerprintTimezone"] = result.SessionMetadata.Timezone ?? string.Empty;
            metadata["fingerprintLanguage"] = result.SessionMetadata.Language ?? string.Empty;
            metadata["fingerprintViewportWidth"] = result.SessionMetadata.ViewportWidth?.ToString() ?? string.Empty;
            metadata["fingerprintViewportHeight"] = result.SessionMetadata.ViewportHeight?.ToString() ?? string.Empty;
            metadata["fingerprintDeviceScale"] = result.SessionMetadata.DeviceScaleFactor?.ToString("F1") ?? string.Empty;
            metadata["fingerprintIsMobile"] = result.SessionMetadata.IsMobile?.ToString() ?? string.Empty;
            metadata["fingerprintHasTouch"] = result.SessionMetadata.HasTouch?.ToString() ?? string.Empty;
            metadata["networkProxyId"] = result.SessionMetadata.ProxyId ?? string.Empty;
            metadata["networkProxyAddress"] = result.SessionMetadata.ProxyAddress ?? string.Empty;
            metadata["networkExitIp"] = result.SessionMetadata.ExitIpAddress ?? string.Empty;
            if (result.SessionMetadata.NetworkDelayMinMs.HasValue)
            {
                metadata["networkDelayMinMs"] = result.SessionMetadata.NetworkDelayMinMs.Value.ToString();
            }
            if (result.SessionMetadata.NetworkDelayMaxMs.HasValue)
            {
                metadata["networkDelayMaxMs"] = result.SessionMetadata.NetworkDelayMaxMs.Value.ToString();
            }
            if (result.SessionMetadata.NetworkRetryBaseDelayMs.HasValue)
            {
                metadata["networkRetryBaseDelayMs"] = result.SessionMetadata.NetworkRetryBaseDelayMs.Value.ToString();
            }
            if (result.SessionMetadata.NetworkMaxRetryAttempts.HasValue)
            {
                metadata["networkMaxRetryAttempts"] = result.SessionMetadata.NetworkMaxRetryAttempts.Value.ToString();
            }
            if (result.SessionMetadata.NetworkMitigationCount.HasValue)
            {
                metadata["networkMitigationCount"] = result.SessionMetadata.NetworkMitigationCount.Value.ToString();
            }
        }

        return metadata;
    }

    private static IReadOnlyDictionary<string, string> BuildErrorMetadata(BrowserOpenToolRequest request, string? requestId, Exception ex)
    {
        var resolvedKey = NormalizeProfileKey(request.ProfileKey);
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["requestId"] = requestId ?? string.Empty,
            ["mode"] = IsUserProfile(resolvedKey) ? BrowserProfileKind.User.ToString() : BrowserProfileKind.Isolated.ToString(),
            ["profilePath"] = request.ProfilePath ?? string.Empty,
            ["folderName"] = resolvedKey,
            ["error"] = ex.Message
        };

        return metadata;
    }
}

public sealed record BrowserOpenToolRequest(
    [property: Description("可选模式提示，保留向后兼容 | Optional mode hint kept for backwards compatibility")] string? Mode = null,
    [property: Description("用户浏览器配置路径，若为空则自动探测 | User profile path; auto-detected when empty")] string? ProfilePath = null,
    [property: Description("浏览器键：user 代表用户配置，其他值作为独立配置目录名 | Browser key: 'user' for the user profile, other values act as isolated profile folder names")] string? ProfileKey = null,
    [property: Description("请求 ID，便于审计与重试 | Request identifier for auditing and retries")] string? RequestId = null);
