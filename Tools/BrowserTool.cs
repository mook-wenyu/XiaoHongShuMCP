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
}

public sealed record BrowserOpenToolRequest(
    [property: Description("用户浏览器配置路径，若为空则自动探测 | User profile path; auto-detected when empty")] string ProfilePath = "",
    [property: Description("浏览器键：user 代表用户配置，其他值作为独立配置目录名 | Browser key: 'user' for the user profile, other values act as isolated profile folder names")] string ProfileKey = "");
