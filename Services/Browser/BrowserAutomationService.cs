using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HushOps.FingerprintBrowser.Core;
using HushOps.Servers.XiaoHongShu.Services.Browser.Network;
using HushOps.Servers.XiaoHongShu.Services.Browser.Playwright;
using HushOps.Servers.XiaoHongShu.Services.Notes;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace HushOps.Servers.XiaoHongShu.Services.Browser;

/// <summary>
/// 中文：基于本地笔记数据集模拟浏览器行为，输出结构化日志并等待拟人化节奏。
/// </summary>
public sealed class BrowserAutomationService : IBrowserAutomationService
{
    private readonly INoteRepository _repository;
    private readonly IFileSystem _fileSystem;
    private readonly IFingerprintBrowser _fingerprintBrowser;
    private readonly INetworkStrategyManager _networkStrategyManager;
    private readonly Playwright.IPlaywrightSessionManager _playwrightSessionManager;
    private readonly ILogger<BrowserAutomationService> _logger;
    private readonly ConcurrentDictionary<string, BrowserOpenResult> _openedProfiles = new(StringComparer.OrdinalIgnoreCase);

    public BrowserAutomationService(
        INoteRepository repository,
        IFileSystem fileSystem,
        IFingerprintBrowser fingerprintBrowser,
        INetworkStrategyManager networkStrategyManager,
        Playwright.IPlaywrightSessionManager playwrightSessionManager,
        ILogger<BrowserAutomationService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _fingerprintBrowser = fingerprintBrowser ?? throw new ArgumentNullException(nameof(fingerprintBrowser));
        _networkStrategyManager = networkStrategyManager ?? throw new ArgumentNullException(nameof(networkStrategyManager));
        _playwrightSessionManager = playwrightSessionManager ?? throw new ArgumentNullException(nameof(playwrightSessionManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    private static string NormalizeKey(string profileKey)
        => string.IsNullOrWhiteSpace(profileKey) ? BrowserOpenRequest.UserProfileKey : profileKey.Trim();

    private static bool IsUserProfile(string profileKey)
        => string.Equals(profileKey, BrowserOpenRequest.UserProfileKey, StringComparison.OrdinalIgnoreCase);

    public async Task<BrowserOpenResult> EnsureProfileAsync(string profileKey, string? profilePath, CancellationToken cancellationToken)
    {
        var normalizedKey = NormalizeKey(profileKey);
        if (_openedProfiles.TryGetValue(normalizedKey, out var existing))
        {
            return existing;
        }

        if (!IsUserProfile(normalizedKey))
        {
            throw new InvalidOperationException($"浏览器键 {normalizedKey} 未打开，请先调用 xhs_browser_open。");
        }

        var request = BrowserOpenRequest.UseUserProfile(profilePath, normalizedKey);
        var opened = await OpenAsync(request, cancellationToken).ConfigureAwait(false);
        if (opened.AutoOpened)
        {
            return opened;
        }

        var autoOpened = opened with { AutoOpened = true };
        _openedProfiles[autoOpened.ProfileKey] = autoOpened;
        _logger.LogInformation(
            "[BrowserAutomation] auto-open key={Key} path={Path} fallback={Fallback}",
            autoOpened.ProfileKey,
            autoOpened.ProfilePath,
            autoOpened.UsedFallbackPath);
        return autoOpened;
    }

    public async Task<BrowserOpenResult> OpenAsync(BrowserOpenRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedRequest = request.EnsureValid();

        BrowserOpenResult result = normalizedRequest.Kind switch
        {
            BrowserProfileKind.User => CreateUserProfileResult(normalizedRequest),
            BrowserProfileKind.Isolated => CreateIsolatedProfileResult(normalizedRequest),
            _ => throw new ArgumentOutOfRangeException(nameof(request.Kind), request.Kind, "不支持的浏览器配置模式。")
        };

        var validated = result.EnsureValid();

        if (_openedProfiles.TryGetValue(validated.ProfileKey, out var existing))
        {
            if (!string.Equals(existing.ProfilePath, validated.ProfilePath, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"浏览器键 {validated.ProfileKey} 已占用，请更换 profileKey 或 folderName。");
            }

            var warningResult = existing with { AlreadyOpen = true };
            _logger.LogWarning(
                "[BrowserAutomation] key={Key} 已打开，返回缓存结果 path={Path}",
                warningResult.ProfileKey,
                warningResult.ProfilePath);
            _openedProfiles[warningResult.ProfileKey] = warningResult;
            return warningResult;
        }

        var fingerprint = await _fingerprintBrowser.GetProfileAsync(validated.ProfileKey, cancellationToken).ConfigureAwait(false);
        var network = await _networkStrategyManager.PrepareSessionAsync(validated.ProfileKey, cancellationToken).ConfigureAwait(false);
        var enriched = validated with
        {
            SessionMetadata = new BrowserSessionMetadata(
                null, // Hash 不再使用，SDK 内部管理
                fingerprint.UserAgent,
                fingerprint.TimezoneId,
                fingerprint.Locale,
                fingerprint.ViewportWidth,
                fingerprint.ViewportHeight,
                null, // DeviceScaleFactor 不再使用
                null, // IsMobile 不再使用
                null, // HasTouch 不再使用
                network.ProxyId,
                network.ProxyAddress,
                network.ExitIp?.ToString(),
                network.DelayMinMs,
                network.DelayMaxMs,
                network.RetryBaseDelayMs,
                network.MaxRetryAttempts,
                network.MitigationCount)
        };

        _logger.LogInformation(
            "[BrowserAutomation] open key={Key} mode={Mode} path={Path} isNewProfile={IsNew} fallback={Fallback}",
            enriched.ProfileKey,
            enriched.Kind,
            enriched.ProfilePath,
            enriched.IsNewProfile,
            enriched.UsedFallbackPath);

        if (enriched.SessionMetadata is not null)
        {
            _logger.LogDebug(
                "[BrowserAutomation] session metadata fingerprint={Fingerprint} proxy={Proxy} address={Address}",
                enriched.SessionMetadata.FingerprintHash,
                enriched.SessionMetadata.ProxyId,
                enriched.SessionMetadata.ProxyAddress);
        }

        await _playwrightSessionManager
            .EnsureSessionAsync(enriched, network, fingerprint, cancellationToken)
            .ConfigureAwait(false);

        _openedProfiles[enriched.ProfileKey] = enriched;

        return enriched;
    }

    public bool TryGetOpenProfile(string profileKey, out BrowserOpenResult? result)
    {
        if (string.IsNullOrWhiteSpace(profileKey))
        {
            result = default;
            return false;
        }

        var normalizedKey = NormalizeKey(profileKey);
        return _openedProfiles.TryGetValue(normalizedKey, out result);
    }

    public IReadOnlyDictionary<string, BrowserOpenResult> OpenProfiles => _openedProfiles;

    public async Task NavigateRandomAsync(string browserKey, string keyword, bool waitForLoad, CancellationToken cancellationToken)
    {
        var profile = EnsureProfileExists(browserKey);
        var notes = await _repository.QueryAsync(keyword, 10, cancellationToken).ConfigureAwait(false);
        var selected = notes.Count > 0 ? notes[Random.Shared.Next(notes.Count)] : null;
        LogNavigation(profile.ProfileKey, "random", keyword, selected);
        if (waitForLoad)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(350), cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task NavigateKeywordAsync(string browserKey, string keyword, bool waitForLoad, CancellationToken cancellationToken)
    {
        var profile = EnsureProfileExists(browserKey);
        var notes = await _repository.QueryAsync(keyword, 5, cancellationToken).ConfigureAwait(false);
        var selected = notes.Count > 0 ? notes[0] : null;
        LogNavigation(profile.ProfileKey, "keyword", keyword, selected);
        if (waitForLoad)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<BrowserPageContext> EnsurePageContextAsync(string browserKey, CancellationToken cancellationToken)
    {
        var normalizedKey = NormalizeKey(browserKey);
        if (!_openedProfiles.TryGetValue(normalizedKey, out var profile))
        {
            if (!IsUserProfile(normalizedKey))
            {
                throw new InvalidOperationException($"浏览器键 {normalizedKey} 未打开，请先调用 xhs_browser_open。");
            }

            profile = await EnsureProfileAsync(normalizedKey, null, cancellationToken).ConfigureAwait(false);
        }

        var fingerprint = await _fingerprintBrowser.GetProfileAsync(profile.ProfileKey, cancellationToken).ConfigureAwait(false);
        var network = await _networkStrategyManager.PrepareSessionAsync(profile.ProfileKey, cancellationToken).ConfigureAwait(false);

        var page = await _playwrightSessionManager.EnsurePageAsync(profile, network, fingerprint, cancellationToken).ConfigureAwait(false);
        return new BrowserPageContext(profile, fingerprint, network, page);
    }

    private BrowserOpenResult EnsureProfileExists(string browserKey)
    {
        var normalizedKey = NormalizeKey(browserKey);
        if (!_openedProfiles.TryGetValue(normalizedKey, out var profile))
        {
            throw new InvalidOperationException($"浏览器键 {normalizedKey} 未打开，请先调用 xhs_browser_open。");
        }

        return profile;
    }

    private void LogNavigation(string browserKey, string mode, string keyword, NoteRecord? note)
    {
        if (note is null)
        {
            _logger.LogWarning("[BrowserAutomation] 未找到匹配笔记 key={Key} mode={Mode} keyword={Keyword}", browserKey, mode, keyword);
            return;
        }

        _logger.LogInformation(
            "[BrowserAutomation] key={Key} mode={Mode} keyword={Keyword} target={NoteId} title={Title}",
            browserKey,
            mode,
            keyword,
            note.Id,
            note.Title);
    }

    private BrowserOpenResult CreateUserProfileResult(BrowserOpenRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.ProfilePath))
        {
            var fullPath = _fileSystem.Path.GetFullPath(request.ProfilePath);
            if (!_fileSystem.Directory.Exists(fullPath))
            {
                throw new InvalidOperationException($"指定的浏览器配置路径不存在：{fullPath}");
            }

            // 检测并警告，但不阻止操作（使用 profile-directory 可以共存）
            if (IsLikelyInUse(fullPath))
            {
                _logger.LogWarning(
                    "[BrowserAutomation] userDataDir 可能正在被浏览器使用：{Path}，请确保使用独立的 profile-directory 以避免冲突",
                    fullPath);
            }

            return new BrowserOpenResult(
                BrowserProfileKind.User,
                request.ProfileKey,
                fullPath,
                false,
                false,
                request.ProfileDirectoryName,
                false,
                false,
                null,
                request.ConnectionMode,
                request.CdpPort);
        }

        foreach (var candidate in EnumerateUserProfileCandidates())
        {
            if (_fileSystem.Directory.Exists(candidate))
            {
                // 检测并警告，但不阻止操作
                if (IsLikelyInUse(candidate))
                {
                    _logger.LogWarning(
                        "[BrowserAutomation] 自动探测的 userDataDir 可能正在被浏览器使用：{Path}，将使用独立的 profile-directory",
                        candidate);
                }

                // 使用固定的 profile-directory 实现会话持久化
                // 避免使用用户日常 profile (Default/Profile 1) 以防止冲突
                var effectiveDir = string.IsNullOrWhiteSpace(request.ProfileDirectoryName)
                    ? "HushOpsAutomation"
                    : request.ProfileDirectoryName.Trim();

                _logger.LogInformation(
                    "[BrowserAutomation] 自动探测到用户配置：{Path}，使用 profile-directory：{Dir}",
                    candidate,
                    effectiveDir);

                return new BrowserOpenResult(
                    BrowserProfileKind.User,
                    request.ProfileKey,
                    candidate,
                    false,
                    true,
                    effectiveDir,
                    false,
                    false,
                    null,
                    request.ConnectionMode,
                    request.CdpPort);
            }
        }

        throw new InvalidOperationException("未找到可用的用户浏览器配置，请显式提供 profilePath 参数。");
    }

    private BrowserOpenResult CreateIsolatedProfileResult(BrowserOpenRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ProfileDirectoryName))
        {
            throw new InvalidOperationException("独立浏览器配置必须提供 folderName。");
        }

        var folderName = request.ProfileDirectoryName.Trim();
        if (!string.IsNullOrWhiteSpace(request.ProfilePath))
        {
            throw new InvalidOperationException("独立浏览器模式不允许指定 profilePath。");
        }

        var targetPath = _fileSystem.Path.GetFullPath(_fileSystem.Path.Combine("storage", "browser-profiles", folderName));

        var existed = _fileSystem.Directory.Exists(targetPath);
        if (!existed)
        {
            _fileSystem.Directory.CreateDirectory(targetPath);
        }

        return new BrowserOpenResult(
            BrowserProfileKind.Isolated,
            request.ProfileKey,
            targetPath,
            !existed,
            false,
            folderName,
            false,
            false,
            null,
            BrowserConnectionMode.Launch,  // 独立配置强制 Launch 模式
            request.CdpPort);
    }

    private static IEnumerable<string> EnumerateUserProfileCandidates()
    {
        foreach (var path in EnumerateWindowsProfiles())
        {
            yield return path;
        }

        foreach (var path in EnumerateMacProfiles())
        {
            yield return path;
        }

        foreach (var path in EnumerateLinuxProfiles())
        {
            yield return path;
        }
    }

    private static IEnumerable<string> EnumerateWindowsProfiles()
    {
        if (!OperatingSystem.IsWindows())
        {
            yield break;
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
        {
            yield break;
        }

        var candidates = new[]
        {
            // 优先 Edge，与项目“msedge 通道优先”保持一致
            Path.Combine(localAppData, "Microsoft", "Edge", "User Data"),
            Path.Combine(localAppData, "Google", "Chrome", "User Data"),
            Path.Combine(localAppData, "BraveSoftware", "Brave-Browser", "User Data")
        };

        foreach (var candidate in candidates)
        {
            yield return Path.GetFullPath(candidate);
        }
    }

    private static IEnumerable<string> EnumerateMacProfiles()
    {
        if (!OperatingSystem.IsMacOS())
        {
            yield break;
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
        if (string.IsNullOrWhiteSpace(home))
        {
            yield break;
        }

        var candidates = new[]
        {
            // 优先 Edge
            Path.Combine(home, "Library", "Application Support", "Microsoft Edge"),
            Path.Combine(home, "Library", "Application Support", "Google", "Chrome"),
            Path.Combine(home, "Library", "Application Support", "BraveSoftware", "Brave-Browser")
        };

        foreach (var candidate in candidates)
        {
            yield return Path.GetFullPath(candidate);
        }
    }

    private static IEnumerable<string> EnumerateLinuxProfiles()
    {
        if (!OperatingSystem.IsLinux())
        {
            yield break;
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
        if (string.IsNullOrWhiteSpace(home))
        {
            yield break;
        }

        var candidates = new[]
        {
            // 优先 Edge
            Path.Combine(home, ".config", "microsoft-edge"),
            Path.Combine(home, ".config", "google-chrome"),
            Path.Combine(home, ".config", "chromium")
        };

        foreach (var candidate in candidates)
        {
            yield return Path.GetFullPath(candidate);
        }
    }

    // 基于锁文件时间戳的启发式判断目录是否正在被浏览器使用
    private bool IsLikelyInUse(string userDataDir)
    {
        try
        {
            var candidates = new[] { "SingletonLock", "SingletonCookie", "SingletonSocket", "LOCK" };
            foreach (var name in candidates)
            {
                var path = Path.Combine(userDataDir, name);
                if (File.Exists(path))
                {
                    var info = new FileInfo(path);
                    if (DateTime.UtcNow - info.LastWriteTimeUtc < TimeSpan.FromMinutes(5))
                    {
                        return true;
                    }
                }
            }
        }
        catch
        {
            // 忽略任何 IO 读取失败，将其视为未知状态
        }
        return false;
    }
}
