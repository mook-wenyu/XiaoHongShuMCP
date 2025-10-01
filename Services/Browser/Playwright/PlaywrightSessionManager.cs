using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using HushOps.Servers.XiaoHongShu.Configuration;
using HushOps.Servers.XiaoHongShu.Services.Browser.Fingerprint;
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
    Task<PlaywrightSession> EnsureSessionAsync(BrowserOpenResult openResult, NetworkSessionContext networkContext, FingerprintContext fingerprintContext, CancellationToken cancellationToken);
    Task<IPage> EnsurePageAsync(BrowserOpenResult openResult, NetworkSessionContext networkContext, FingerprintContext fingerprintContext, CancellationToken cancellationToken);
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

    public async Task<IPage> EnsurePageAsync(BrowserOpenResult openResult, NetworkSessionContext networkContext, FingerprintContext fingerprintContext, CancellationToken cancellationToken)
    {
        var session = await EnsureSessionAsync(openResult, networkContext, fingerprintContext, cancellationToken).ConfigureAwait(false);
        if (session.Page is null || session.Page.IsClosed)
        {
            var page = await session.Context.NewPageAsync().ConfigureAwait(false);
            session.ReplacePage(page);
        }

        return session.Page ?? throw new InvalidOperationException("无法创建 Playwright 页面实例。");
    }

    private async Task<PlaywrightSession> CreateSessionAsync(BrowserOpenResult openResult, NetworkSessionContext networkContext, FingerprintContext fingerprintContext, CancellationToken cancellationToken)
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
                UserAgent = fingerprintContext.UserAgent,
                Locale = fingerprintContext.Language,
                TimezoneId = fingerprintContext.Timezone,
                AcceptDownloads = true,
                IgnoreHTTPSErrors = true,
                Headless = false, // 用户模式必须非headless以便手动登录
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

            context = await browser.NewContextAsync(contextOptions).ConfigureAwait(false);
        }

        if (fingerprintContext.ExtraHeaders.Count > 0)
        {
            await context.SetExtraHTTPHeadersAsync(fingerprintContext.ExtraHeaders).ConfigureAwait(false);
        }

        // 隐藏自动化检测特征（动态注入硬件参数）
        await context.AddInitScriptAsync(BuildWebdriverHideScript(fingerprintContext)).ConfigureAwait(false);

        if (fingerprintContext.CanvasNoise)
        {
            await context.AddInitScriptAsync(BuildCanvasNoiseScript(fingerprintContext)).ConfigureAwait(false);
        }

        if (fingerprintContext.WebglMask)
        {
            await context.AddInitScriptAsync(BuildWebglMaskScript(fingerprintContext)).ConfigureAwait(false);
        }

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
            fingerprintContext.UserAgent,
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

    /// <summary>
    /// 中文：全面反检测脚本（动态注入），隐藏所有自动化特征（Playwright 注入对象、CDP 变量、webdriver 标记等）。
    /// English: Comprehensive anti-detection script (dynamic injection) to hide all automation fingerprints (Playwright injection, CDP variables, webdriver flags, etc.).
    /// </summary>
    private string BuildWebdriverHideScript(FingerprintContext ctx) => $@"
(() => {{
    // 1. 隐藏 navigator.webdriver（最基础检测）
    Object.defineProperty(navigator, 'webdriver', {{
        get: () => false,
        configurable: true
    }});

    // 2. 删除 Playwright 注入的全局对象
    delete window.__playwright__;
    delete window.__pw_manual;
    delete window.__PW_inspect;
    delete window.__pwInitScripts;
    delete window.__playwright__binding__;

    // 3. 删除所有 CDP (Chrome DevTools Protocol) 变量
    Object.keys(window).forEach(key => {{
        if (key.startsWith('cdc_') || key.includes('$cdc_') || key.includes('$chrome_')) {{
            delete window[key];
        }}
    }});

    // 4. 修复 navigator.plugins（返回真实的空 PluginArray，而不是假数组）
    Object.defineProperty(navigator, 'plugins', {{
        get: () => {{
            const plugins = [];
            plugins.length = 0;
            plugins.item = function(index) {{ return null; }};
            plugins.namedItem = function(name) {{ return null; }};
            plugins.refresh = function() {{}};
            return Object.freeze(plugins);
        }},
        configurable: true
    }});

    // 5. 处理 navigator.permissions（自动化环境通常返回 granted，真实环境返回 prompt/denied）
    if (navigator.permissions && navigator.permissions.query) {{
        const originalQuery = navigator.permissions.query.bind(navigator.permissions);
        navigator.permissions.query = function(parameters) {{
            // notifications 通常返回 prompt，不返回 granted（避免自动化特征）
            if (parameters && parameters.name === 'notifications') {{
                return Promise.resolve({{
                    state: 'prompt',
                    name: 'notifications',
                    onchange: null
                }});
            }}
            return originalQuery(parameters);
        }};
    }}

    // 6. 完善 window.chrome 对象（自动化环境通常缺失或不完整）
    if (!window.chrome || !window.chrome.runtime) {{
        window.chrome = {{
            runtime: {{
                connect: () => {{}},
                sendMessage: () => {{}},
                onMessage: {{
                    addListener: () => {{}},
                    removeListener: () => {{}},
                    hasListener: () => false
                }}
            }},
            loadTimes: () => {{}},
            csi: () => {{}},
            app: {{}}
        }};
    }}

    // 7. 修复 iframe.contentWindow（iframe 内的 webdriver 也要隐藏）
    try {{
        const originalContentWindow = Object.getOwnPropertyDescriptor(HTMLIFrameElement.prototype, 'contentWindow');
        if (originalContentWindow) {{
            Object.defineProperty(HTMLIFrameElement.prototype, 'contentWindow', {{
                get: function() {{
                    const win = originalContentWindow.get.call(this);
                    if (win) {{
                        try {{
                            Object.defineProperty(win.navigator, 'webdriver', {{
                                get: () => false,
                                configurable: true
                            }});
                        }} catch (e) {{
                            // 跨域 iframe 无法修改，忽略
                        }}
                    }}
                    return win;
                }},
                configurable: true
            }});
        }}
    }} catch (e) {{
        // 某些环境可能不支持，忽略
    }}

    // 8. 处理 document.documentElement.getAttribute('webdriver')（某些网站检查 HTML 元素属性）
    const originalGetAttribute = HTMLElement.prototype.getAttribute;
    HTMLElement.prototype.getAttribute = function(name) {{
        if (name === 'webdriver') {{
            return null;
        }}
        return originalGetAttribute.call(this, name);
    }};

    // 9. 修复 navigator.languages（保持原值，只确保存在）
    if (!navigator.languages || navigator.languages.length === 0) {{
        Object.defineProperty(navigator, 'languages', {{
            get: () => ['zh-CN', 'zh', 'en'],
            configurable: true
        }});
    }}

    // 10. 处理 navigator.userAgentData（Chromium 新 API，自动化环境可能返回异常值）
    if (navigator.userAgentData) {{
        Object.defineProperty(navigator, 'userAgentData', {{
            get: () => ({{
                brands: [
                    {{ brand: 'Chromium', version: '131' }},
                    {{ brand: 'Google Chrome', version: '131' }},
                    {{ brand: 'Not_A Brand', version: '8' }}
                ],
                mobile: false,
                platform: 'Windows'
            }}),
            configurable: true
        }});
    }}

    // 11. 修复 navigator.hardwareConcurrency（动态注入 CPU 核心数）
    Object.defineProperty(navigator, 'hardwareConcurrency', {{
        get: () => {ctx.HardwareConcurrency},
        configurable: true
    }});

    // 12. 修复 navigator.vendor（动态注入厂商信息）
    Object.defineProperty(navigator, 'vendor', {{
        get: () => '{ctx.Vendor}',
        configurable: true
    }});

    // 13. 修复 window outer dimensions（真实浏览器的 outer 尺寸应大于 inner）
    Object.defineProperty(window, 'outerWidth', {{
        get: () => window.innerWidth + 10,
        configurable: true
    }});
    Object.defineProperty(window, 'outerHeight', {{
        get: () => window.innerHeight + 85,
        configurable: true
    }});
}})();
";

    /// <summary>
    /// 中文：Canvas 指纹噪声脚本（动态注入），使用固定种子确保一致性。
    /// English: Canvas fingerprint noise script (dynamic injection) with fixed seed for consistency.
    /// </summary>
    private string BuildCanvasNoiseScript(FingerprintContext ctx) => $@"
(() => {{
    // 使用固定种子初始化随机数生成器
    let seed = {ctx.CanvasSeed};
    const seededRandom = () => {{
        const x = Math.sin(seed++) * 10000;
        return x - Math.floor(x);
    }};

    const toBlob = HTMLCanvasElement.prototype.toBlob;
    const toDataURL = HTMLCanvasElement.prototype.toDataURL;
    const getImageData = CanvasRenderingContext2D.prototype.getImageData;

    const noisify = function(canvas, context) {{
        if (!context) return;
        const shift = {{
            'r': seededRandom() > 0.5 ? 1 : -1,
            'g': seededRandom() > 0.5 ? 1 : -1,
            'b': seededRandom() > 0.5 ? 1 : -1,
            'a': seededRandom() > 0.5 ? 1 : -1
        }};

        const width = canvas.width;
        const height = canvas.height;
        if (width === 0 || height === 0) return;

        const imageData = getImageData.apply(context, [0, 0, width, height]);

        for (let i = 0; i < imageData.data.length; i += 4) {{
            imageData.data[i] = imageData.data[i] + shift.r;
            imageData.data[i + 1] = imageData.data[i + 1] + shift.g;
            imageData.data[i + 2] = imageData.data[i + 2] + shift.b;
            imageData.data[i + 3] = imageData.data[i + 3] + shift.a;
        }}

        context.putImageData(imageData, 0, 0);
    }};

    Object.defineProperty(HTMLCanvasElement.prototype, 'toBlob', {{
        value: function() {{
            noisify(this, this.getContext('2d'));
            return toBlob.apply(this, arguments);
        }}
    }});

    Object.defineProperty(HTMLCanvasElement.prototype, 'toDataURL', {{
        value: function() {{
            noisify(this, this.getContext('2d'));
            return toDataURL.apply(this, arguments);
        }}
    }});

    Object.defineProperty(CanvasRenderingContext2D.prototype, 'getImageData', {{
        value: function() {{
            noisify(this.canvas, this);
            return getImageData.apply(this, arguments);
        }}
    }});
}})();
";

    /// <summary>
    /// 中文：WebGL 指纹伪装脚本（动态注入），使用固定种子确保一致性。
    /// English: WebGL fingerprint spoofing script (dynamic injection) with fixed seed for consistency.
    /// </summary>
    private string BuildWebglMaskScript(FingerprintContext ctx) => $@"
(() => {{
    // 使用固定种子初始化随机数生成器
    let seed = {ctx.WebglSeed};
    const seededRandom = () => {{
        const x = Math.sin(seed++) * 10000;
        return x - Math.floor(x);
    }};

    const getParameter = WebGLRenderingContext.prototype.getParameter;
    WebGLRenderingContext.prototype.getParameter = function(parameter) {{
        // UNMASKED_VENDOR_WEBGL (37445) - 动态注入硬件厂商
        if (parameter === 37445) {{
            return '{ctx.WebglVendor}';
        }}
        // UNMASKED_RENDERER_WEBGL (37446) - 动态注入渲染器名称
        if (parameter === 37446) {{
            return '{ctx.WebglRenderer}';
        }}

        const result = getParameter.call(this, parameter);

        // 对数值型参数添加固定种子的微小随机噪声
        if (typeof result === 'number') {{
            return result + (seededRandom() * 0.0001 - 0.00005);
        }}

        return result;
    }};

    // 同样处理 WebGL2
    if (typeof WebGL2RenderingContext !== 'undefined') {{
        const getParameter2 = WebGL2RenderingContext.prototype.getParameter;
        WebGL2RenderingContext.prototype.getParameter = function(parameter) {{
            if (parameter === 37445) return '{ctx.WebglVendor}';
            if (parameter === 37446) return '{ctx.WebglRenderer}';

            const result = getParameter2.call(this, parameter);

            if (typeof result === 'number') {{
                return result + (seededRandom() * 0.0001 - 0.00005);
            }}

            return result;
        }};
    }}
}})();
";

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
