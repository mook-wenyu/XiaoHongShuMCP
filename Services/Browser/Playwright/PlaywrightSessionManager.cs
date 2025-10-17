using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using HushOps.FingerprintBrowser.Core;
using HushOps.FingerprintBrowser.Playwright;
using HushOps.Servers.XiaoHongShu.Configuration;
using HushOps.Servers.XiaoHongShu.Services.Browser.Network;
using HushOps.Servers.XiaoHongShu.Services.Browser.Profile;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace HushOps.Servers.XiaoHongShu.Services.Browser.Playwright;

public sealed class PlaywrightSession
{
    public PlaywrightSession(IBrowserContext context, IPage page, string profileKey)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        Page = page ?? throw new ArgumentNullException(nameof(page));
        ProfileKey = profileKey;
    }

    public IBrowserContext Context { get; }

    public string ProfileKey { get; }

    public IPage Page { get; private set; }

    public void ReplacePage(IPage page)
    {
        Page = page;
    }
}

public interface IPlaywrightSessionManager
{
    Task<PlaywrightSession> EnsureSessionAsync(BrowserOpenResult openResult, NetworkSessionContext networkContext, FingerprintProfile fingerprintProfile, CancellationToken cancellationToken);
    Task<IPage> EnsurePageAsync(BrowserOpenResult openResult, NetworkSessionContext networkContext, FingerprintProfile fingerprintProfile, CancellationToken cancellationToken);
}

/// <summary>
/// 中文：负责创建并缓存 Playwright 上下文，结合指纹与网络策略。
/// English: Creates and caches Playwright contexts enriched with fingerprint and network settings.
/// </summary>
public sealed class PlaywrightSessionManager : IPlaywrightSessionManager, IAsyncDisposable
{
    private readonly ILogger<PlaywrightSessionManager> _logger;
    private readonly ConcurrentDictionary<string, PlaywrightSession> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lazy<Task<IPlaywright>> _playwright;
    private readonly Lazy<Task<IBrowser>> _browser;
    private readonly INetworkStrategyManager _networkStrategyManager;
    private readonly IProfileManager _profileManager;
    private readonly XiaoHongShuOptions _xhsOptions;
    private readonly Random _random = new();
    private readonly IBrowserHost _browserHost;

    public PlaywrightSessionManager(
        INetworkStrategyManager networkStrategyManager,
        IProfileManager profileManager,
        IOptions<XiaoHongShuOptions> xhsOptions,
        ILogger<PlaywrightSessionManager> logger,
        IBrowserHost browserHost)
    {
        _networkStrategyManager = networkStrategyManager;
        _profileManager = profileManager;
        _xhsOptions = xhsOptions.Value;
        _logger = logger;
        _browserHost = browserHost;
        _playwright = new Lazy<Task<IPlaywright>>(Microsoft.Playwright.Playwright.CreateAsync);
        _browser = new Lazy<Task<IBrowser>>(CreateBrowserAsync);
    }

    public async Task<PlaywrightSession> EnsureSessionAsync(BrowserOpenResult openResult, NetworkSessionContext networkContext, FingerprintProfile fingerprintProfile, CancellationToken cancellationToken)
    {
        if (_sessions.TryGetValue(openResult.ProfileKey, out var existing))
        {
            return existing;
        }

        var session = await CreateSessionAsync(openResult, networkContext, fingerprintProfile, cancellationToken).ConfigureAwait(false);
        _sessions[openResult.ProfileKey] = session;
        return session;
    }

    public async Task<IPage> EnsurePageAsync(BrowserOpenResult openResult, NetworkSessionContext networkContext, FingerprintProfile fingerprintProfile, CancellationToken cancellationToken)
    {
        var session = await EnsureSessionAsync(openResult, networkContext, fingerprintProfile, cancellationToken).ConfigureAwait(false);
        if (session.Page is null || session.Page.IsClosed)
        {
            var page = await session.Context.NewPageAsync().ConfigureAwait(false);
            session.ReplacePage(page);
        }

        return session.Page ?? throw new InvalidOperationException("无法创建 Playwright 页面实例。");
    }

    private async Task<PlaywrightSession> CreateSessionAsync(BrowserOpenResult openResult, NetworkSessionContext networkContext, FingerprintProfile fingerprintProfile, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "[Playwright] CreateSessionAsync 开始 profile={Profile} kind={Kind} mode={Mode} cdpPort={Port} profilePath={Path}",
            openResult.ProfileKey,
            openResult.Kind,
            openResult.ConnectionMode,
            openResult.CdpPort,
            openResult.ProfilePath ?? "<null>");

        // 使用系统已安装的 Edge（msedge 通道），跳过 Playwright 浏览器下载/安装。
        var playwright = await _playwright.Value.ConfigureAwait(false);

        // 一次定型 Profile（若不存在则初始化），region 取 proxyId 作为提示
        var profile = await _profileManager.EnsureInitializedAsync(openResult.ProfileKey, networkContext.ProxyId, cancellationToken).ConfigureAwait(false);

        // 选择代理端点：优先 Profile 固定端点，否则使用当前网络策略并补写回 Profile
        var proxyEndpoint = !string.IsNullOrWhiteSpace(profile.ProxyEndpoint)
            ? profile.ProxyEndpoint
            : networkContext.ProxyAddress;
        if (string.IsNullOrWhiteSpace(profile.ProxyEndpoint) && !string.IsNullOrWhiteSpace(networkContext.ProxyAddress))
        {
            await _profileManager.AssignProxyIfEmptyAsync(profile, networkContext.ProxyAddress!, cancellationToken).ConfigureAwait(false);
            proxyEndpoint = networkContext.ProxyAddress;
        }

        // CDP 连接逻辑：Auto 模式或显式 ConnectCdp 模式
        if (openResult.ConnectionMode == BrowserConnectionMode.ConnectCdp || 
            (openResult.ConnectionMode == BrowserConnectionMode.Auto && openResult.Kind == BrowserProfileKind.User))
        {
            // ConnectCdp 模式：仅连接，不允许自动启动（allowAutoLaunch = false）
            // Auto 模式：允许自动启动（allowAutoLaunch = true）
            var allowAutoLaunch = openResult.ConnectionMode == BrowserConnectionMode.Auto;
            
            var cdpSession = await TryConnectViaCdpAsync(
                playwright, 
                openResult, 
                profile, 
                networkContext,
                allowAutoLaunch,
                cancellationToken).ConfigureAwait(false);
            
            if (cdpSession != null)
            {
                return cdpSession;
            }

            // 显式 ConnectCdp 模式下连接失败则抛出异常
            if (openResult.ConnectionMode == BrowserConnectionMode.ConnectCdp)
            {
                throw new InvalidOperationException(
                    $"CDP 连接失败。请确保浏览器已启动并带上参数：--remote-debugging-port={openResult.CdpPort}\n" +
                    $"示例命令：msedge.exe --remote-debugging-port={openResult.CdpPort}");
            }

            // Auto 模式下 CDP 失败，记录警告并回退到 Launch 模式
            _logger.LogWarning(
                "[Playwright] CDP 连接失败，回退到 Launch 模式 profile={Profile}",
                openResult.ProfileKey);
            // 继续执行下方的 Launch 逻辑
        }

        // 构造 WebRTC 策略参数
        var webrtcArg = profile.WebRtc == WebRtcMode.ForceProxy
            ? "--force-webrtc-ip-handling-policy=disable_non_proxied_udp"
            : "--force-webrtc-ip-handling-policy=default_public_interface_only";

        // 优先走强类型 SDK BrowserHost；不可用时回退本地实现
        IBrowserContext context;
        if (_browserHost is not null)
        {
            var fp = MapToSdkFingerprint(profile, openResult);
            var net = new NetworkSessionOptions
            {
                ProxyAddress = proxyEndpoint,
                ProxyUsername = networkContext.ProxyUsername,
                ProxyPassword = networkContext.ProxyPassword,
                WebRtcPolicy = profile.WebRtc == WebRtcMode.ForceProxy ? "force-proxy" : "default-route-only"
            };

            if (openResult.Kind == BrowserProfileKind.User && !string.IsNullOrWhiteSpace(openResult.ProfilePath))
            {
                // 当用户明确指定了系统 userDataDir 时，使用本地持久化上下文创建，避免依赖 SDK 新重载
                _logger.LogInformation(
                    "[Playwright] 使用持久化上下文 (LaunchPersistentContext) profile={Profile} path={Path} dir={Dir}",
                    openResult.ProfileKey,
                    openResult.ProfilePath,
                    openResult.ProfileDirectoryName ?? "<auto>");

                var args = new List<string> { webrtcArg };
                if (!string.IsNullOrWhiteSpace(openResult.ProfileDirectoryName))
                {
                    args.Add($"--profile-directory={openResult.ProfileDirectoryName}");
                }

                var launchOptions = new BrowserTypeLaunchPersistentContextOptions
                {
                    Channel = profile.BrowserChannel, // msedge
                    Args = args.ToArray(),
                    UserAgent = profile.UserAgent,
                    Locale = profile.Locale,
                    TimezoneId = profile.TimezoneId,
                    AcceptDownloads = true,
                    IgnoreHTTPSErrors = true,
                    Headless = false,
                    ViewportSize = new ViewportSize { Width = profile.ViewportWidth, Height = profile.ViewportHeight },
                    DeviceScaleFactor = (float)profile.DeviceScaleFactor,
                    IsMobile = false,
                    HasTouch = false
                };

                var persistentProxy = BuildProxy(proxyEndpoint, networkContext.ProxyUsername, networkContext.ProxyPassword);
                if (persistentProxy != null)
                {
                    launchOptions.Proxy = persistentProxy;
                }

                // 优先使用 ExecutablePath（更可靠），Channel 作为 fallback
                // 基于研究发现：GitHub issue #34797 显示 Channel="msedge" 在某些环境下不稳定
                var edgePath = ResolveSystemEdgePath();
                if (!string.IsNullOrWhiteSpace(edgePath))
                {
                    _logger.LogInformation(
                        "[Playwright] 使用 ExecutablePath 优先策略: {Path}",
                        edgePath);
                    launchOptions.ExecutablePath = edgePath;
                    launchOptions.Channel = null;
                }
                else
                {
                    _logger.LogInformation(
                        "[Playwright] 未找到 Edge 可执行文件，使用 Channel 回退: {Channel}",
                        profile.BrowserChannel);
                    // launchOptions.Channel 保持原值
                }

                try
                {
                    context = await playwright.Chromium.LaunchPersistentContextAsync(openResult.ProfilePath, launchOptions).ConfigureAwait(false);

                    _logger.LogInformation(
                        "[Playwright] 持久化上下文启动成功 method={Method}",
                        string.IsNullOrWhiteSpace(edgePath) ? "Channel" : "ExecutablePath");
                }
                catch (PlaywrightException ex)
                {
                    _logger.LogError(ex,
                        "[Playwright] 持久化上下文启动失败。异常类型: {ExceptionType}, 消息: {Message}",
                        ex.GetType().Name,
                        ex.Message);
                    throw;
                }
            }
            else
            {
                // 使用 SDK 旧重载（不显式覆写 userDataDir）
                context = await _browserHost.GetPersistentContextAsync(openResult.ProfileKey, fp, net, cancellationToken).ConfigureAwait(false);
            }
        }
        else
        {
            if (openResult.Kind == BrowserProfileKind.User && !string.IsNullOrWhiteSpace(openResult.ProfilePath))
            {
                // 如果是用户配置模式且提供了profilePath,使用持久化上下文保留登录状态
                _logger.LogInformation(
                    "[Playwright] 使用持久化上下文 (LaunchPersistentContext) profile={Profile} path={Path}",
                    openResult.ProfileKey,
                    openResult.ProfilePath);

                var launchOptions = new BrowserTypeLaunchPersistentContextOptions
                {
                    Channel = profile.BrowserChannel, // msedge
                    Args = new[] { webrtcArg },
                    UserAgent = profile.UserAgent,
                    Locale = profile.Locale,
                    TimezoneId = profile.TimezoneId,
                    AcceptDownloads = true,
                    IgnoreHTTPSErrors = true,
                    Headless = false, // 用户模式必须非headless以便手动登录
                    ViewportSize = new ViewportSize
                    {
                        Width = profile.ViewportWidth,
                        Height = profile.ViewportHeight
                    },
                    DeviceScaleFactor = (float)profile.DeviceScaleFactor,
                    IsMobile = false,
                    HasTouch = false
                };

                var persistentProxy = BuildProxy(proxyEndpoint, networkContext.ProxyUsername, networkContext.ProxyPassword);
                if (persistentProxy != null)
                {
                    launchOptions.Proxy = persistentProxy;
                }

                // 优先使用 ExecutablePath（更可靠），Channel 作为 fallback
                // 基于研究发现：GitHub issue #34797 显示 Channel="msedge" 在某些环境下不稳定
                var edgePath = ResolveSystemEdgePath();
                if (!string.IsNullOrWhiteSpace(edgePath))
                {
                    _logger.LogInformation(
                        "[Playwright] (_browserHost 为 null) 使用 ExecutablePath 优先策略: {Path}",
                        edgePath);
                    launchOptions.ExecutablePath = edgePath;
                    launchOptions.Channel = null;
                }
                else
                {
                    _logger.LogInformation(
                        "[Playwright] (_browserHost 为 null) 未找到 Edge 可执行文件，使用 Channel 回退: msedge");
                    // launchOptions.Channel 保持原值 "msedge"
                }

                try
                {
                    context = await playwright.Chromium.LaunchPersistentContextAsync(
                        openResult.ProfilePath,
                        launchOptions).ConfigureAwait(false);

                    _logger.LogInformation(
                        "[Playwright] 持久化上下文启动成功 method={Method}",
                        string.IsNullOrWhiteSpace(edgePath) ? "Channel" : "ExecutablePath");
                }
                catch (PlaywrightException ex)
                {
                    _logger.LogError(ex,
                        "[Playwright] 持久化上下文启动失败。异常类型: {ExceptionType}, 消息: {Message}",
                        ex.GetType().Name,
                        ex.Message);
                    throw;
                }
            }
            else
            {
                // 独立配置或未指定路径,使用临时上下文
                _logger.LogInformation(
                    "[Playwright] 使用临时上下文 (NewContext) profile={Profile}",
                    openResult.ProfileKey);

                var browser = await _browser.Value.ConfigureAwait(false);

                var contextOptions = new BrowserNewContextOptions
                {
                    UserAgent = profile.UserAgent,
                    Locale = profile.Locale,
                    TimezoneId = profile.TimezoneId,
                    AcceptDownloads = true,
                    IgnoreHTTPSErrors = true,
                    ViewportSize = new ViewportSize
                    {
                        Width = profile.ViewportWidth,
                        Height = profile.ViewportHeight
                    },
                    DeviceScaleFactor = (float)profile.DeviceScaleFactor,
                    IsMobile = false,
                    HasTouch = false
                };

                var tempContextProxy = BuildProxy(proxyEndpoint, networkContext.ProxyUsername, networkContext.ProxyPassword);
                if (tempContextProxy != null)
                {
                    contextOptions.Proxy = tempContextProxy;
                }

                context = await browser.NewContextAsync(contextOptions).ConfigureAwait(false);
            }
        }

        // 注意：反检测脚本现在由 FingerprintBrowser SDK 自动注入（当使用 IBrowserHost 路径）

        // 在统一位置设置通用请求头（如 Accept-Language），避免分支重复（IBrowserHost 已设置，此处再次设置保持幂等）
        await ApplyCommonHeadersAsync(context, profile.AcceptLanguage).ConfigureAwait(false);

        await ApplyNetworkControlsAsync(openResult.ProfileKey, context, networkContext, cancellationToken).ConfigureAwait(false);

        var page = await context.NewPageAsync().ConfigureAwait(false);

        // 自动导航到小红书首页（这是唯一允许的 URL 跳转，之后所有导航都通过点击完成）
        try
        {
            await page.GotoAsync("https://www.xiaohongshu.com/explore", new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle,
                Timeout = 30000
            }).ConfigureAwait(false);

            _logger.LogInformation(
                "[Playwright] navigated to Xiaohongshu homepage profile={Profile}",
                openResult.ProfileKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[Playwright] failed to navigate to Xiaohongshu homepage profile={Profile}, continuing with blank page",
                openResult.ProfileKey);
        }

        _logger.LogInformation(
            "[Playwright] context created profile={Profile} ua={UA} proxy={Proxy}",
            openResult.ProfileKey,
            fingerprintProfile.UserAgent,
            networkContext.ProxyAddress ?? "none");

        return new PlaywrightSession(context, page, openResult.ProfileKey);
    }

    private static FingerprintProfile MapToSdkFingerprint(ProfileRecord profile, BrowserOpenResult open)
    {
        var profileType = open.Kind == BrowserProfileKind.User ? ProfileType.User : ProfileType.Synthetic;
        return new FingerprintProfile(
            open.ProfileKey,
            profileType,
            profile.UserAgent,
            profile.Platform,
            profile.ViewportWidth,
            profile.ViewportHeight,
            profile.Locale,
            profile.TimezoneId,
            profile.HardwareConcurrency,
            profile.Vendor,
            profile.WebglVendor,
            profile.WebglRenderer,
            profile.CanvasSeed,
            profile.WebglSeed,
            null,
            open.Kind == BrowserProfileKind.User ? open.ProfilePath : null
        );
    }

    private static Proxy? BuildProxy(string? endpoint, string? username, string? password)
    {
        if (string.IsNullOrWhiteSpace(endpoint)) return null;
        var proxy = new Proxy { Server = endpoint };
        if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
        {
            proxy.Username = username;
            proxy.Password = password;
        }
        return proxy;
    }

    private static Task ApplyCommonHeadersAsync(IBrowserContext context, string acceptLanguage)
    {
        return context.SetExtraHTTPHeadersAsync(new[]
        {
            new KeyValuePair<string, string>("Accept-Language", acceptLanguage)
        });
    }

    private async Task ApplyNetworkControlsAsync(string profileKey, IBrowserContext context, NetworkSessionContext networkContext, CancellationToken cancellationToken)
    {
        if (networkContext.DelayMaxMs > 0)
        {
            await context.RouteAsync("**/*", async route =>
            {
                var delay = _random.Next(networkContext.DelayMinMs, networkContext.DelayMaxMs + 1);
                if (delay > 0)
                {
                    try
                    {
                        await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        // 任务取消时直接继续请求
                    }
                }

                await route.ContinueAsync().ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        context.RequestFinished += (_, request) =>
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var response = await request.ResponseAsync().ConfigureAwait(false);
                    var status = response?.Status;
                    if (status == 429 || status == 403)
                    {
                        if (status.HasValue)
                        {
                            _networkStrategyManager.RecordMitigation(profileKey, status.Value);
                        }
                        _logger.LogWarning(
                            "[Playwright] mitigation triggered profile={Profile} status={Status} url={Url} total={Total}",
                            profileKey,
                            status,
                            request.Url,
                            _networkStrategyManager.GetMitigationCount(profileKey));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "[Playwright] failed to inspect response for {Url}", request.Url);
                }
            }, cancellationToken);
        };
    }

    private async Task<IBrowser> CreateBrowserAsync()
    {
        var playwright = await _playwright.Value.ConfigureAwait(false);
        var args = new[]
        {
            "--disable-blink-features=AutomationControlled",
            "--exclude-switches=enable-automation",
            "--disable-infobars",
            "--disable-extensions",
            "--disable-dev-shm-usage",
            "--disable-features=IsolateOrigins,site-per-process",
            "--disable-site-isolation-trials",
            "--force-webrtc-ip-handling-policy=default_public_interface_only",
            "--window-position=0,0"
        };

        var headless = _xhsOptions.Headless;

        // 首选系统通道 msedge；失败则回退到显式可执行路径
        try
        {
            return await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Channel = "msedge",
                Headless = headless,
                Args = args
            }).ConfigureAwait(false);
        }
        catch (PlaywrightException ex)
        {
            _logger.LogWarning(ex, "[Playwright] msedge channel launch failed, trying ExecutablePath fallback.");
            var edgePath = ResolveSystemEdgePath();
            if (!string.IsNullOrWhiteSpace(edgePath))
            {
                return await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
                {
                    ExecutablePath = edgePath,
                    Headless = headless,
                    Args = args
                }).ConfigureAwait(false);
            }

            _logger.LogError("[Playwright] 未找到系统 Edge 可执行文件，无法启动浏览器。");
            throw;
        }
    }

    private string? ResolveSystemEdgePath()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                _logger.LogDebug("[Playwright] 开始搜索 Windows 系统 Edge 路径");

                var candidates = new List<string?>
                {
                    Environment.GetEnvironmentVariable("ProgramFiles"),
                    Environment.GetEnvironmentVariable("ProgramFiles(x86)"),
                    Environment.GetEnvironmentVariable("LOCALAPPDATA")
                };

                _logger.LogDebug(
                    "[Playwright] 候选环境变量: ProgramFiles={PF}, ProgramFiles(x86)={PFx86}, LOCALAPPDATA={Local}",
                    candidates[0] ?? "<null>",
                    candidates[1] ?? "<null>",
                    candidates[2] ?? "<null>");

                foreach (var root in candidates)
                {
                    if (string.IsNullOrWhiteSpace(root))
                    {
                        _logger.LogDebug("[Playwright] 跳过空环境变量");
                        continue;
                    }

                    var probe = System.IO.Path.Combine(root, "Microsoft", "Edge", "Application", "msedge.exe");
                    _logger.LogDebug("[Playwright] 检查路径: {Path}", probe);

                    if (System.IO.File.Exists(probe))
                    {
                        _logger.LogInformation("[Playwright] 找到 Edge 可执行文件: {Path}", probe);
                        return probe;
                    }
                }

                _logger.LogWarning("[Playwright] 未在任何候选路径中找到 Edge");
            }
            else if (OperatingSystem.IsMacOS())
            {
                var macPath = "/Applications/Microsoft Edge.app/Contents/MacOS/Microsoft Edge";
                if (System.IO.File.Exists(macPath)) return macPath;
            }
            else if (OperatingSystem.IsLinux())
            {
                var linuxCandidates = new[]
                {
                    "/usr/bin/microsoft-edge-stable",
                    "/usr/bin/microsoft-edge"
                };
                foreach (var path in linuxCandidates)
                {
                    if (System.IO.File.Exists(path)) return path;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Playwright] ResolveSystemEdgePath 发生异常");
        }
        return null;
    }

    /// <summary>
    /// 中文：尝试通过 CDP 连接浏览器，支持"仅连接"或"连接+自动启动"模式。
    /// English: Tries to connect via CDP, supports "connect-only" or "connect-with-auto-launch" modes.
    /// </summary>
    /// <param name="allowAutoLaunch">
    /// 中文：是否允许在连接失败时自动启动浏览器。
    /// true = Auto 模式（连接失败后自动启动新浏览器）
    /// false = ConnectCdp 模式（仅连接，不启动，失败返回 null）
    /// English: Whether to allow auto-launching browser on connection failure.
    /// true = Auto mode (auto-launch new browser on failure)
    /// false = ConnectCdp mode (connect-only, no launch, return null on failure)
    /// </param>
    private async Task<PlaywrightSession?> TryConnectViaCdpAsync(
        IPlaywright playwright,
        BrowserOpenResult openResult,
        ProfileRecord profile,
        NetworkSessionContext networkContext,
        bool allowAutoLaunch,
        CancellationToken cancellationToken)
    {
        var cdpEndpoint = $"http://localhost:{openResult.CdpPort}";
        
        // 第一次尝试：直接连接现有浏览器
        var session = await TryConnectToCdpAsync(playwright, openResult, profile, networkContext, cdpEndpoint, cancellationToken).ConfigureAwait(false);
        if (session != null)
        {
            _logger.LogInformation(
                "[Playwright] CDP 连接成功，已复用现有浏览器 profile={Profile} port={Port}",
                openResult.ProfileKey,
                openResult.CdpPort);
            return session;
        }

        // ConnectCdp 模式：仅连接，不允许自动启动
        if (!allowAutoLaunch)
        {
            _logger.LogWarning(
                "[Playwright] CDP 连接失败且不允许自动启动 profile={Profile} port={Port}。" +
                "请手动启动浏览器并带上参数：--remote-debugging-port={Port}",
                openResult.ProfileKey,
                openResult.CdpPort,
                openResult.CdpPort);
            return null;
        }

        // Auto 模式：连接失败，尝试自动启动浏览器
        _logger.LogInformation(
            "[Playwright] CDP 连接失败，尝试自动启动浏览器 profile={Profile} port={Port}",
            openResult.ProfileKey,
            openResult.CdpPort);

        var launched = await TryLaunchBrowserWithCdpAsync(openResult, profile, cancellationToken).ConfigureAwait(false);
        if (!launched)
        {
            _logger.LogWarning(
                "[Playwright] 无法自动启动浏览器 profile={Profile}",
                openResult.ProfileKey);
            return null;
        }

        // 等待浏览器启动并稳定
        await Task.Delay(2000, cancellationToken).ConfigureAwait(false);

        // 第二次尝试：连接到刚启动的浏览器
        session = await TryConnectToCdpAsync(playwright, openResult, profile, networkContext, cdpEndpoint, cancellationToken).ConfigureAwait(false);
        if (session != null)
        {
            _logger.LogInformation(
                "[Playwright] 自动启动浏览器成功并建立 CDP 连接 profile={Profile}",
                openResult.ProfileKey);
            return session;
        }

        _logger.LogWarning(
            "[Playwright] 浏览器已启动但 CDP 连接仍失败 profile={Profile}",
            openResult.ProfileKey);
        return null;
    }

    /// <summary>
    /// 中文：尝试连接到 CDP 端点。
    /// English: Attempts to connect to CDP endpoint.
    /// </summary>
    private async Task<PlaywrightSession?> TryConnectToCdpAsync(
        IPlaywright playwright,
        BrowserOpenResult openResult,
        ProfileRecord profile,
        NetworkSessionContext networkContext,
        string cdpEndpoint,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug(
                "[Playwright] 尝试 CDP 连接 profile={Profile} endpoint={Endpoint}",
                openResult.ProfileKey,
                cdpEndpoint);

            var cdpBrowser = await playwright.Chromium.ConnectOverCDPAsync(cdpEndpoint).ConfigureAwait(false);
            
            // 获取第一个可用的浏览器上下文（通常是已经打开的用户配置）
            var cdpContext = cdpBrowser.Contexts.Count > 0 
                ? cdpBrowser.Contexts[0] 
                : throw new InvalidOperationException("CDP 连接成功但未找到可用的浏览器上下文，请确保浏览器已打开且有活动页面。");

            _logger.LogInformation(
                "[Playwright] CDP 连接成功 profile={Profile} contexts={Count}",
                openResult.ProfileKey,
                cdpBrowser.Contexts.Count);

            // 应用网络控制和通用请求头
            await ApplyCommonHeadersAsync(cdpContext, profile.AcceptLanguage).ConfigureAwait(false);
            await ApplyNetworkControlsAsync(openResult.ProfileKey, cdpContext, networkContext, cancellationToken).ConfigureAwait(false);

            // 获取或创建页面
            var cdpPage = cdpContext.Pages.Count > 0 
                ? cdpContext.Pages[0] 
                : await cdpContext.NewPageAsync().ConfigureAwait(false);

            _logger.LogInformation(
                "[Playwright] CDP 会话创建成功 profile={Profile} pages={PageCount}",
                openResult.ProfileKey,
                cdpContext.Pages.Count);

            return new PlaywrightSession(cdpContext, cdpPage, openResult.ProfileKey);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex,
                "[Playwright] CDP 连接失败 profile={Profile} endpoint={Endpoint}",
                openResult.ProfileKey,
                cdpEndpoint);
            return null;
        }
    }

    /// <summary>
    /// 中文：自动启动带远程调试端口的浏览器实例。
    /// English: Automatically launches browser instance with remote debugging port.
    /// </summary>
    private Task<bool> TryLaunchBrowserWithCdpAsync(
        BrowserOpenResult openResult,
        ProfileRecord profile,
        CancellationToken cancellationToken)
    {
        try
        {
            var browserPath = ResolveSystemEdgePath();
            if (string.IsNullOrWhiteSpace(browserPath))
            {
                _logger.LogWarning("[Playwright] 未找到系统浏览器可执行文件");
                return Task.FromResult(false);
            }

            var args = new List<string>
            {
                $"--remote-debugging-port={openResult.CdpPort}",
                "--no-first-run",
                "--no-default-browser-check"
            };

            // 如果有用户数据目录，添加相关参数
            if (!string.IsNullOrWhiteSpace(openResult.ProfilePath))
            {
                args.Add($"--user-data-dir=\"{openResult.ProfilePath}\"");
                
                // 如果指定了 profile-directory，添加参数
                if (!string.IsNullOrWhiteSpace(openResult.ProfileDirectoryName))
                {
                    args.Add($"--profile-directory=\"{openResult.ProfileDirectoryName}\"");
                }
            }

            // 启动到小红书首页
            args.Add("https://www.xiaohongshu.com/explore");

            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = browserPath,
                Arguments = string.Join(" ", args),
                UseShellExecute = true,
                CreateNoWindow = false
            };

            _logger.LogInformation(
                "[Playwright] 启动浏览器 path={Path} port={Port} profile={Profile}",
                browserPath,
                openResult.CdpPort,
                openResult.ProfilePath ?? "<default>");

            var process = System.Diagnostics.Process.Start(startInfo);
            if (process == null)
            {
                _logger.LogWarning("[Playwright] 浏览器进程启动失败");
                return Task.FromResult(false);
            }

            _logger.LogInformation(
                "[Playwright] 浏览器进程已启动 pid={Pid}",
                process.Id);

            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[Playwright] 启动浏览器时发生异常 profile={Profile}",
                openResult.ProfileKey);
            return Task.FromResult(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var session in _sessions.Values)
        {
            try
            {
                if (session.Page is not null && !session.Page.IsClosed)
                {
                    await session.Page.CloseAsync().ConfigureAwait(false);
                }
                await session.Context.CloseAsync().ConfigureAwait(false);
            }
            catch
            {
                // ignore
            }
        }

        if (_browser.IsValueCreated)
        {
            try
            {
                await (await _browser.Value.ConfigureAwait(false)).CloseAsync().ConfigureAwait(false);
            }
            catch
            {
                // ignore
            }
        }

        if (_playwright.IsValueCreated)
        {
            (await _playwright.Value.ConfigureAwait(false)).Dispose();
        }
    }
}
