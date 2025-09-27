using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using HushOps.Servers.XiaoHongShu.Services.Browser.Fingerprint;
using HushOps.Servers.XiaoHongShu.Services.Browser.Network;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace HushOps.Servers.XiaoHongShu.Services.Browser.Playwright;

public sealed record PlaywrightSession(IBrowserContext Context, string ProfileKey);

public interface IPlaywrightSessionManager
{
    Task<PlaywrightSession> EnsureSessionAsync(BrowserOpenResult openResult, NetworkSessionContext networkContext, FingerprintContext fingerprintContext, CancellationToken cancellationToken);
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
    private readonly Random _random = new();

    public PlaywrightSessionManager(INetworkStrategyManager networkStrategyManager, ILogger<PlaywrightSessionManager> logger)
    {
        _networkStrategyManager = networkStrategyManager;
        _logger = logger;
        _playwright = new Lazy<Task<IPlaywright>>(Microsoft.Playwright.Playwright.CreateAsync);
        _browser = new Lazy<Task<IBrowser>>(CreateBrowserAsync);
    }

    public async Task<PlaywrightSession> EnsureSessionAsync(BrowserOpenResult openResult, NetworkSessionContext networkContext, FingerprintContext fingerprintContext, CancellationToken cancellationToken)
    {
        if (_sessions.TryGetValue(openResult.ProfileKey, out var existing))
        {
            return existing;
        }

        var session = await CreateSessionAsync(openResult, networkContext, fingerprintContext, cancellationToken).ConfigureAwait(false);
        _sessions[openResult.ProfileKey] = session;
        return session;
    }

    private async Task<PlaywrightSession> CreateSessionAsync(BrowserOpenResult openResult, NetworkSessionContext networkContext, FingerprintContext fingerprintContext, CancellationToken cancellationToken)
    {
        var browser = await _browser.Value.ConfigureAwait(false);

        var contextOptions = new BrowserNewContextOptions
        {
            UserAgent = fingerprintContext.UserAgent,
            Locale = fingerprintContext.Language,
            TimezoneId = fingerprintContext.Timezone,
            AcceptDownloads = true,
            IgnoreHTTPSErrors = true,
            ViewportSize = new ViewportSize
            {
                Width = fingerprintContext.ViewportWidth,
                Height = fingerprintContext.ViewportHeight
            },
            DeviceScaleFactor = (float?)fingerprintContext.DeviceScaleFactor,
            IsMobile = fingerprintContext.IsMobile,
            HasTouch = fingerprintContext.HasTouch
        };

        if (!string.IsNullOrWhiteSpace(networkContext.ProxyAddress))
        {
            contextOptions.Proxy = new Proxy
            {
                Server = networkContext.ProxyAddress
            };
        }

        if (Directory.Exists(openResult.ProfilePath))
        {
            var storagePath = Path.Combine(openResult.ProfilePath, "storage-state.json");
            if (File.Exists(storagePath))
            {
                contextOptions.StorageStatePath = storagePath;
            }
        }

        var context = await browser.NewContextAsync(contextOptions).ConfigureAwait(false);

        if (fingerprintContext.ExtraHeaders.Count > 0)
        {
            await context.SetExtraHTTPHeadersAsync(fingerprintContext.ExtraHeaders).ConfigureAwait(false);
        }

        if (fingerprintContext.CanvasNoise)
        {
            await context.AddInitScriptAsync(CanvasNoiseScript).ConfigureAwait(false);
        }

        if (fingerprintContext.WebglMask)
        {
            await context.AddInitScriptAsync(WebglMaskScript).ConfigureAwait(false);
        }

        await ApplyNetworkControlsAsync(openResult.ProfileKey, context, networkContext, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "[Playwright] context created profile={Profile} ua={UA} proxy={Proxy}",
            openResult.ProfileKey,
            fingerprintContext.UserAgent,
            networkContext.ProxyAddress ?? "none");

        return new PlaywrightSession(context, openResult.ProfileKey);
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

    private const string CanvasNoiseScript = "(() => {const addNoise = () => {const toDataURL = HTMLCanvasElement.prototype.toDataURL;HTMLCanvasElement.prototype.toDataURL = function(){const result = toDataURL.apply(this, arguments);return result.replace('A','A');};const getImageData = CanvasRenderingContext2D.prototype.getImageData;CanvasRenderingContext2D.prototype.getImageData = function(){const data = getImageData.apply(this, arguments);for (let i = 0; i < data.data.length; i += 4){data.data[i] += Math.floor(Math.random()*3)-1;data.data[i+1] += Math.floor(Math.random()*3)-1;data.data[i+2] += Math.floor(Math.random()*3)-1;}return data;};}; if (typeof window !== 'undefined') { addNoise(); } })();";

    private const string WebglMaskScript = "(() => {const random = () => Math.random() * 0.0001;const getParameter = WebGLRenderingContext.prototype.getParameter;WebGLRenderingContext.prototype.getParameter = function(parameter){const result = getParameter.call(this, parameter);if (typeof result === 'number') { return result + random(); }return result;};})();";

    private async Task<IBrowser> CreateBrowserAsync()
    {
        var playwright = await _playwright.Value.ConfigureAwait(false);
        return await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        }).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var session in _sessions.Values)
        {
            try
            {
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
