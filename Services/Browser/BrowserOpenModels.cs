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
        string? profileDirectoryName)
    {
        Kind = kind;
        ProfileKey = profileKey;
        ProfilePath = profilePath;
        ProfileDirectoryName = profileDirectoryName;
    }

    public BrowserProfileKind Kind { get; init; }

    public string ProfileKey { get; init; }

    public string? ProfilePath { get; init; }

    public string? ProfileDirectoryName { get; init; }

    public static BrowserOpenRequest ForUser(string? profilePath = null)
        => UseUserProfile(profilePath, UserProfileKey);

    public static BrowserOpenRequest ForIsolated(string profileDirectoryName)
        => UseIsolatedProfile(profileDirectoryName, profileDirectoryName);

    public static BrowserOpenRequest UseUserProfile(string? profilePath, string profileKey)
        => new BrowserOpenRequest(BrowserProfileKind.User, profileKey, profilePath, null).EnsureValid();

    public static BrowserOpenRequest UseIsolatedProfile(string profileDirectoryName, string profileKey)
        => new BrowserOpenRequest(BrowserProfileKind.Isolated, profileKey, null, profileDirectoryName).EnsureValid();

    public BrowserOpenRequest EnsureValid()
    {
        if (string.IsNullOrWhiteSpace(ProfileKey))
        {
            throw new InvalidOperationException("ProfileKey 不能为空。");
        }

        var normalizedKey = ProfileKey.Trim();
        var normalizedPath = string.IsNullOrWhiteSpace(ProfilePath) ? null : ProfilePath.Trim();
        var normalizedDirectory = string.IsNullOrWhiteSpace(ProfileDirectoryName) ? null : ProfileDirectoryName.Trim();

        if (Kind == BrowserProfileKind.User)
        {
            if (normalizedDirectory is not null)
            {
                throw new InvalidOperationException("用户浏览器配置不需要 folderName。");
            }

            return new BrowserOpenRequest(BrowserProfileKind.User, normalizedKey, normalizedPath, null);
        }

        if (normalizedDirectory is null)
        {
            throw new InvalidOperationException("独立浏览器配置必须提供 folderName。");
        }

        if (normalizedPath is not null)
        {
            throw new InvalidOperationException("独立浏览器配置禁止指定 profilePath。");
        }

        return new BrowserOpenRequest(BrowserProfileKind.Isolated, normalizedKey, null, normalizedDirectory);
    }
}

/// <summary>
/// 中文：浏览器打开结果，包含配置键、路径、目录名与状态标记。
/// English: Browser open result containing profile key, path, directory name, and state flags.
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
    BrowserSessionMetadata? SessionMetadata)
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
