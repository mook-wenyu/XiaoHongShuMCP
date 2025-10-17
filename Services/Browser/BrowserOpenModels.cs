using System;
using HushOps.FingerprintBrowser.Core;
using HushOps.Servers.XiaoHongShu.Services.Browser.Network;
using Microsoft.Playwright;

namespace HushOps.Servers.XiaoHongShu.Services.Browser;

/// <summary>
/// 中文：浏览器配置模式，区分用户浏览器配置与独立配置。
/// English: Browser profile kind distinguishing user and isolated configurations.
/// </summary>
public enum BrowserProfileKind
{
    User,
    Isolated
}

/// <summary>
/// 中文：浏览器打开请求模型，封装 profile 键、路径及目录名。
/// English: Browser open request describing profile key, optional path, and folder name.
/// </summary>
public sealed record BrowserOpenRequest
{
    public const string UserProfileKey = "user";

    private BrowserOpenRequest(
        BrowserProfileKind kind,
        string profileKey,
        string? profilePath,
        string? profileDirectoryName,
        BrowserConnectionMode connectionMode = BrowserConnectionMode.Auto,
        int cdpPort = 9222)
    {
        Kind = kind;
        ProfileKey = profileKey;
        ProfilePath = profilePath;
        ProfileDirectoryName = profileDirectoryName;
        ConnectionMode = connectionMode;
        CdpPort = cdpPort;
    }

    public BrowserProfileKind Kind { get; init; }

    public string ProfileKey { get; init; }

    public string? ProfilePath { get; init; }

    public string? ProfileDirectoryName { get; init; }

    /// <summary>
    /// 中文：浏览器连接模式，默认自动选择。
    /// English: Browser connection mode, defaults to Auto.
    /// </summary>
    public BrowserConnectionMode ConnectionMode { get; init; }

    /// <summary>
    /// 中文：CDP 远程调试端口，默认 9222。
    /// English: CDP remote debugging port, defaults to 9222.
    /// </summary>
    public int CdpPort { get; init; }

    /// <summary>
    /// 中文：创建用户浏览器配置请求，支持 CDP 连接以获得最佳性能。
    /// English: Creates user browser profile request with CDP connection support for optimal performance.
    /// </summary>
    /// <param name="profilePath">
    /// 用户数据目录路径，null 时自动探测系统默认浏览器配置（通常是 Edge/Chrome 用户数据目录）。
    /// User data directory path, auto-detected to system default browser profile when null (typically Edge/Chrome User Data).
    /// </param>
    /// <returns>配置请求对象 | Configuration request object</returns>
    /// <remarks>
    /// CDP 连接性能优势：启动时间 ~100-200ms（vs Launch 模式 ~2-3s），内存节省 200-500MB，保留浏览器状态和登录信息。
    /// 默认使用 Auto 连接模式：优先尝试 CDP 连接，失败则自动回退到 Launch 模式。
    ///
    /// Performance advantages: ~100-200ms startup (vs ~2-3s in Launch mode), saves 200-500MB memory, preserves browser state and login sessions.
    /// Defaults to Auto connection mode: tries CDP connection first, falls back to Launch on failure.
    /// </remarks>
    public static BrowserOpenRequest ForUser(string? profilePath = null)
        => UseUserProfile(profilePath, UserProfileKey);

    /// <summary>
    /// 中文：创建独立隔离浏览器配置请求，用于隔离测试或多账号场景。
    /// English: Creates isolated browser profile request for isolated testing or multi-account scenarios.
    /// </summary>
    /// <param name="profileDirectoryName">
    /// 配置目录名，同时用作 ProfileKey 和 ProfileDirectoryName。
    /// 自动创建在 storage/browser-profiles/{profileDirectoryName}/ 目录下。
    /// Profile directory name, used as both ProfileKey and ProfileDirectoryName.
    /// Automatically created under storage/browser-profiles/{profileDirectoryName}/.
    /// </param>
    /// <returns>配置请求对象 | Configuration request object</returns>
    /// <remarks>
    /// 独立配置始终使用 Launch 模式（不支持 CDP 连接），确保完全隔离。
    /// 每个独立配置拥有独立的浏览器数据目录，互不干扰。
    ///
    /// Isolated profiles always use Launch mode (CDP not supported) for complete isolation.
    /// Each isolated profile has its own browser data directory without interference.
    /// </remarks>
    public static BrowserOpenRequest ForIsolated(string profileDirectoryName)
        => new BrowserOpenRequest(
            BrowserProfileKind.Isolated,
            profileDirectoryName,
            null,
            profileDirectoryName,
            BrowserConnectionMode.Launch,  // 独立配置强制使用 Launch 模式
            9222).EnsureValid();

    /// <summary>
    /// 中文：创建用户浏览器配置请求（高级 API），支持自定义连接模式、端口和子配置。
    /// English: Creates user browser profile request (advanced API) with custom connection mode, port, and sub-profile support.
    /// </summary>
    /// <param name="profilePath">
    /// 用户数据目录路径，null 时自动探测。
    /// User data directory path, auto-detected when null.
    /// </param>
    /// <param name="profileKey">
    /// 配置键，用于标识和管理浏览器会话。
    /// Profile key for identifying and managing browser sessions.
    /// </param>
    /// <param name="chromiumProfileDirectory">
    /// Chromium 的 --profile-directory 参数值（如 "Default"、"Profile 1"），用于在同一用户数据目录下切换子配置，避免与日常浏览冲突。
    /// Chromium --profile-directory parameter value (e.g., "Default", "Profile 1") for switching between sub-profiles within the same user data directory, avoiding conflicts with daily browsing.
    /// </param>
    /// <param name="connectionMode">
    /// 连接模式：Auto（优先 CDP，失败回退 Launch）、Launch（直接启动）、ConnectCdp（仅 CDP，失败抛错）。
    /// Connection mode: Auto (CDP first, fallback to Launch), Launch (direct start), ConnectCdp (CDP only, throws on failure).
    /// </param>
    /// <param name="cdpPort">
    /// CDP 远程调试端口，默认 9222。需与浏览器启动参数 --remote-debugging-port 一致。
    /// CDP remote debugging port, defaults to 9222. Must match browser launch parameter --remote-debugging-port.
    /// </param>
    /// <returns>配置请求对象 | Configuration request object</returns>
    /// <remarks>
    /// 连接模式选择指南：
    /// - Auto：推荐用于开发环境，灵活自适应浏览器状态。
    /// - Launch：推荐用于生产环境和 CI/CD，确保隔离性和可预测性。
    /// - ConnectCdp：推荐用于人工测试，快速迭代（需手动启动浏览器并添加 --remote-debugging-port=9222）。
    ///
    /// Connection mode selection guide:
    /// - Auto: Recommended for development, flexible adaptation to browser state.
    /// - Launch: Recommended for production and CI/CD, ensures isolation and predictability.
    /// - ConnectCdp: Recommended for manual testing, fast iteration (requires manually starting browser with --remote-debugging-port=9222).
    /// </remarks>
    public static BrowserOpenRequest UseUserProfile(
        string? profilePath,
        string profileKey,
        string? chromiumProfileDirectory = null,
        BrowserConnectionMode connectionMode = BrowserConnectionMode.Auto,
        int cdpPort = 9222)
        => new BrowserOpenRequest(
            BrowserProfileKind.User,
            profileKey,
            profilePath,
            chromiumProfileDirectory,
            connectionMode,
            cdpPort).EnsureValid();


    public BrowserOpenRequest EnsureValid()
    {
        if (string.IsNullOrWhiteSpace(ProfileKey))
        {
            throw new InvalidOperationException("ProfileKey 不能为空。");
        }

        // 验证 CDP 端口范围
        if (CdpPort < 1 || CdpPort > 65535)
        {
            throw new InvalidOperationException($"CDP 端口必须在 1-65535 范围内，当前值：{CdpPort}。");
        }

        // 独立配置禁止使用 CDP 连接模式
        if (Kind == BrowserProfileKind.Isolated && ConnectionMode == BrowserConnectionMode.ConnectCdp)
        {
            throw new InvalidOperationException("独立浏览器配置不支持 CDP 连接模式，只能使用 Launch 模式。");
        }

        var normalizedKey = ProfileKey.Trim();
        var normalizedPath = string.IsNullOrWhiteSpace(ProfilePath) ? null : ProfilePath.Trim();
        var normalizedDirectory = string.IsNullOrWhiteSpace(ProfileDirectoryName) ? null : ProfileDirectoryName.Trim();

        if (Kind == BrowserProfileKind.User)
        {
            // 允许在用户浏览器配置下提供 profile-directory 名称（Chromium 的 --profile-directory）
            return new BrowserOpenRequest(
                BrowserProfileKind.User, 
                normalizedKey, 
                normalizedPath, 
                normalizedDirectory,
                ConnectionMode,
                CdpPort);
        }

        if (normalizedDirectory is null)
        {
            throw new InvalidOperationException("独立浏览器配置必须提供 folderName。");
        }

        if (normalizedPath is not null)
        {
            throw new InvalidOperationException("独立浏览器配置禁止指定 profilePath。");
        }

        return new BrowserOpenRequest(
            BrowserProfileKind.Isolated, 
            normalizedKey, 
            null, 
            normalizedDirectory,
            BrowserConnectionMode.Launch,  // 独立配置强制 Launch
            CdpPort);
    }
}

/// <summary>
/// 中文：浏览器打开结果，包含配置键、路径、目录名、连接模式与状态标记。
/// English: Browser open result containing profile key, path, directory name, connection mode, and state flags.
/// </summary>
public sealed record BrowserOpenResult(
    BrowserProfileKind Kind,
    string ProfileKey,
    string ProfilePath,
    bool IsNewProfile,
    bool UsedFallbackPath,
    string? ProfileDirectoryName,
    bool AlreadyOpen,
    bool AutoOpened,
    BrowserSessionMetadata? SessionMetadata,
    BrowserConnectionMode ConnectionMode,
    int CdpPort)
{
    public BrowserOpenResult EnsureValid()
    {
        if (string.IsNullOrWhiteSpace(ProfileKey))
        {
            throw new InvalidOperationException("ProfileKey 不能为空。");
        }

        if (string.IsNullOrWhiteSpace(ProfilePath))
        {
            throw new InvalidOperationException("ProfilePath 不能为空。");
        }

        return this;
    }
}

/// <summary>
/// 中文：浏览器会话元数据，包含指纹与网络摘要。
/// English: Describes fingerprint and network metadata attached to an open browser session.
/// </summary>
public sealed record BrowserSessionMetadata(
    string? FingerprintHash,
    string? UserAgent,
    string? Timezone,
    string? Language,
    int? ViewportWidth,
    int? ViewportHeight,
    double? DeviceScaleFactor,
    bool? IsMobile,
    bool? HasTouch,
    string? ProxyId,
    string? ProxyAddress,
    string? ExitIpAddress,
    int? NetworkDelayMinMs,
    int? NetworkDelayMaxMs,
    int? NetworkRetryBaseDelayMs,
    int? NetworkMaxRetryAttempts,
    int? NetworkMitigationCount);

public sealed record BrowserPageContext(
    BrowserOpenResult Profile,
    FingerprintProfile Fingerprint,
    NetworkSessionContext Network,
    IPage Page);
