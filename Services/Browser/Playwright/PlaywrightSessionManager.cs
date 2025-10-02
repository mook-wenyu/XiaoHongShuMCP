using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using HushOps.FingerprintBrowser.Core;
using HushOps.FingerprintBrowser.Installation;
using HushOps.Servers.XiaoHongShu.Configuration;
using HushOps.Servers.XiaoHongShu.Services.Browser.Network;
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
    private readonly PlaywrightInstallationOptions _installationOptions;
    private readonly Random _random = new();

    public PlaywrightSessionManager(
        INetworkStrategyManager networkStrategyManager,
        ILogger<PlaywrightSessionManager> logger,
        IOptions<PlaywrightInstallationOptions> installationOptions)
    {
        _networkStrategyManager = networkStrategyManager;
        _logger = logger;
        _playwright = new Lazy<Task<IPlaywright>>(Microsoft.Playwright.Playwright.CreateAsync);
        _browser = new Lazy<Task<IBrowser>>(CreateBrowserAsync);
        _installationOptions = installationOptions?.Value ?? new PlaywrightInstallationOptions();
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
        await PlaywrightInstaller.EnsureInstalledAsync(_installationOptions, _logger, cancellationToken).ConfigureAwait(false);

        var playwright = await _playwright.Value.ConfigureAwait(false);

        // 如果是用户配置模式且提供了profilePath,使用持久化上下文保留登录状态
        IBrowserContext context;
        if (openResult.Kind == BrowserProfileKind.User && !string.IsNullOrWhiteSpace(openResult.ProfilePath))
        {
            _logger.LogInformation(
                "[Playwright] 使用持久化上下文 (LaunchPersistentContext) profile={Profile} path={Path}",
                openResult.ProfileKey,
                openResult.ProfilePath);

            var launchOptions = new BrowserTypeLaunchPersistentContextOptions
            {
                UserAgent = fingerprintProfile.UserAgent,
                Locale = fingerprintProfile.Locale,
                TimezoneId = fingerprintProfile.TimezoneId,
                AcceptDownloads = true,
                IgnoreHTTPSErrors = true,
                Headless = false, // 用户模式必须非headless以便手动登录
                ViewportSize = new ViewportSize
                {
                    Width = fingerprintProfile.ViewportWidth,
                    Height = fingerprintProfile.ViewportHeight
                },
                DeviceScaleFactor = 1.0f,
                IsMobile = false,
                HasTouch = false
            };

            if (!string.IsNullOrWhiteSpace(networkContext.ProxyAddress))
            {
                launchOptions.Proxy = new Proxy
                {
                    Server = networkContext.ProxyAddress
                };
            }

            context = await playwright.Chromium.LaunchPersistentContextAsync(
                openResult.ProfilePath,
                launchOptions).ConfigureAwait(false);
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
                UserAgent = fingerprintProfile.UserAgent,
                Locale = fingerprintProfile.Locale,
                TimezoneId = fingerprintProfile.TimezoneId,
                AcceptDownloads = true,
                IgnoreHTTPSErrors = true,
                ViewportSize = new ViewportSize
                {
                    Width = fingerprintProfile.ViewportWidth,
                    Height = fingerprintProfile.ViewportHeight
                },
                DeviceScaleFactor = 1.0f,
                IsMobile = false,
                HasTouch = false
            };

            if (!string.IsNullOrWhiteSpace(networkContext.ProxyAddress))
            {
                contextOptions.Proxy = new Proxy
                {
                    Server = networkContext.ProxyAddress
                };
            }

            context = await browser.NewContextAsync(contextOptions).ConfigureAwait(false);
        }

        // 注意：反检测脚本现在由 FingerprintBrowser SDK 自动注入，这里不再需要手动调用

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
        return await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            Args = new[]
            {
                // 最关键：禁用自动化控制特征（底层标记）
                "--disable-blink-features=AutomationControlled",
                // 移除自动化提示信息
                "--exclude-switches=enable-automation",
                // 禁用信息栏
                "--disable-infobars",
                // 禁用扩展
                "--disable-extensions",
                // 禁用开发者模式扩展加载提示
                "--disable-dev-shm-usage",
                // 禁用站点隔离（避免某些检测）
                "--disable-features=IsolateOrigins,site-per-process",
                "--disable-site-isolation-trials",
                // 窗口位置（避免检测默认位置）
                "--window-position=0,0"
            }
        }).ConfigureAwait(false);
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
