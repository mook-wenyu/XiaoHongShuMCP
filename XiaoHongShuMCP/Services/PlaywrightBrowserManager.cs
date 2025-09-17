using System.IO;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using HushOps.Core.Persistence;

using ServiceXhsSettings = XiaoHongShuMCP.Services.XhsSettings;

namespace XiaoHongShuMCP.Services;

/// <summary>
/// 浏览器管理服务（仅使用 Playwright 受管浏览器；不依赖 CDP 连接现有实例）。
/// </summary>
public class PlaywrightBrowserManager : IBrowserManager, IAsyncDisposable
{
    private readonly ILogger<PlaywrightBrowserManager> _logger;
    private readonly IConfiguration _configuration;
    private readonly IOptions<ServiceXhsSettings> _settings;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IBrowserContext? _browserContext;
    private IPage? _page;
    private bool _disposed;
    private readonly Timer _healthCheckTimer;
    private bool _isReconnecting;
    private int _busyOperations;
    private readonly IDomElementManager _domElementManager; // 注入：提供选择器别名来源
    private readonly IJsonLocalStore _jsonStore;
    private bool? _headlessOverride; // 运行期 Headless 覆盖；null 表示使用配置
    private bool _currentHeadless; // 记录当前上下文实际 Headless
    private readonly ServiceXhsSettings.BrowserSettingsSection.ConnectionSection _connectionConfig;
    private DateTimeOffset _contextCreatedAt = DateTimeOffset.MinValue;
    private DateTimeOffset _lastCookieRefresh = DateTimeOffset.MinValue;

    public PlaywrightBrowserManager(
        ILogger<PlaywrightBrowserManager> logger,
        IConfiguration configuration,
        IDomElementManager domElementManager,
        IJsonLocalStore jsonLocalStore,
        IOptions<ServiceXhsSettings> settings)
    {
        _logger = logger;
        _configuration = configuration;
        _domElementManager = domElementManager;
        _jsonStore = jsonLocalStore ?? throw new ArgumentNullException(nameof(jsonLocalStore));
        _settings = settings;
        _connectionConfig = _settings.Value?.BrowserSettings?.Connection
            ?? new ServiceXhsSettings.BrowserSettingsSection.ConnectionSection();

        // 健康检查：每 30s 简单校验上下文是否存在
        _healthCheckTimer = new Timer(CheckConnectionHealth, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    // === 健康检查与重连 ===
    private async void CheckConnectionHealth(object? state)
    {
        if (_disposed || _isReconnecting) return;
        if (Interlocked.CompareExchange(ref _busyOperations, 0, 0) > 0) return;
        try
        {
            if (_browserContext is null)
            {
                _logger.LogWarning("检测到浏览器未就绪，尝试重启...");
                await TryReconnectAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "健康检查异常，尝试重启");
            await TryReconnectAsync();
        }
    }

    private async Task TryReconnectAsync()
    {
        if (_isReconnecting) return;
        _isReconnecting = true;
        await _semaphore.WaitAsync();
        try
        {
            await DisconnectSafely();
            await InitializePlaywrightAsync();
            _logger.LogInformation("浏览器重启成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "浏览器重启失败");
        }
        finally
        {
            _isReconnecting = false;
            _semaphore.Release();
        }
    }

    // === 生命周期 ===
    private async Task InitializePlaywrightAsync()
    {
        if (_playwright != null) return;
        await _semaphore.WaitAsync();
        try
        {
            if (_playwright != null) return;

            _logger.LogInformation("初始化 Playwright 并启动受管浏览器（持久化上下文）...");
            _playwright = await Playwright.CreateAsync();

            var configuredHeadless = _configuration.GetValue("XHS:BrowserSettings:Headless", false);
            var headless = _headlessOverride ?? configuredHeadless;
            var exePath = _configuration["XHS:BrowserSettings:ExecutablePath"];
            var channel = _configuration["XHS:BrowserSettings:Channel"];
            var userDataDir = ResolveUserDataDir();

            var plan = BuildPersistentLaunchPlan(userDataDir, headless, exePath, channel);
            await LaunchPersistentFromPlanAsync(plan);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    // === 计划构建与启动 ===
    internal PlaywrightLaunchPlan BuildPersistentLaunchPlan(string userDataDir, bool headless, string? executablePath, string? channel)
    {
        // 统一首启抑制参数；Playwright 直接管理进程，无需远程调试端口
        var args = new List<string>
        {
            "--no-first-run",
            "--no-default-browser-check",
            "--disable-first-run-ui"
        };
        // 若未显式指定 ExecutablePath，则优先尝试查找系统已安装浏览器
        string? exe = executablePath;
        if (string.IsNullOrWhiteSpace(exe))
        {
            // 优先尝试 PATH 首目录的 google-chrome（用于测试注入）
            var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            var sep = OperatingSystem.IsWindows() ? ';' : ':';
            var firstDir = pathEnv.Split(sep, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(firstDir))
            {
                var name = OperatingSystem.IsWindows() ? "google-chrome.exe" : "google-chrome";
                var firstCandidate = Path.Combine(firstDir!, name);
                if (File.Exists(firstCandidate)) exe = firstCandidate;
            }
            exe ??= DetectInstalledBrowserExecutable();
        }
        // 最终强优先：PATH 下可用的 google-chrome（用于测试注入）
        var preferChrome = SearchPath(OperatingSystem.IsWindows() ? "google-chrome.exe" : "google-chrome")
            .Concat(SearchPath("google-chrome"))
            .FirstOrDefault(File.Exists);
        if (!string.IsNullOrEmpty(preferChrome)) exe = preferChrome;
        return new PlaywrightLaunchPlan(userDataDir, headless, exe, channel, args);
    }

    private async Task LaunchPersistentFromPlanAsync(PlaywrightLaunchPlan plan)
    {
        var options = new BrowserTypeLaunchPersistentContextOptions
        {
            Headless = plan.Headless,
            Args = plan.Args.ToArray()
        };
        if (!string.IsNullOrWhiteSpace(plan.ExecutablePath)) options.ExecutablePath = plan.ExecutablePath;
        else if (!string.IsNullOrWhiteSpace(plan.Channel)) options.Channel = plan.Channel;

        _logger.LogInformation("启动 Playwright 浏览器（持久化上下文），Headless={Headless}, Channel={Channel}, Executable={Exe}", plan.Headless, plan.Channel ?? "", plan.ExecutablePath ?? "");

        var context = await _playwright!.Chromium.LaunchPersistentContextAsync(plan.UserDataDir, options);
        _browserContext = context;
        _browser = context.Browser;

        var page = context.Pages.FirstOrDefault() ?? await context.NewPageAsync();
        _page = page;
        _currentHeadless = plan.Headless;
        _contextCreatedAt = DateTimeOffset.UtcNow;
        _lastCookieRefresh = _contextCreatedAt;

        // 反检测策略注入改由 IPlaywrightAntiDetectionPipeline 统一管理，
        // 此处不再进行任何脚本级别的标识移除或注入（参见 BrowserConnectionHostedService）。
    }

    /// <summary>
    /// 查找系统已安装的浏览器（Chrome/Edge/Chromium），返回第一个命中的可执行路径；未找到返回 null。
    /// 优先顺序：Chrome → Edge → Chromium → PATH 中的同名命令。
    /// </summary>
    internal static string? DetectInstalledBrowserExecutable()
    {
        IEnumerable<string> Candidates()
        {
            var os = Environment.OSVersion.Platform;
            if (os == PlatformID.Win32NT)
            {
                var pf86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)");
                var pf = Environment.GetEnvironmentVariable("ProgramFiles");
                var lad = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (!string.IsNullOrEmpty(pf86)) yield return Path.Combine(pf86!, "Google", "Chrome", "Application", "chrome.exe");
                if (!string.IsNullOrEmpty(pf)) yield return Path.Combine(pf!, "Google", "Chrome", "Application", "chrome.exe");
                if (!string.IsNullOrEmpty(lad)) yield return Path.Combine(lad!, "Google", "Chrome", "Application", "chrome.exe");
                if (!string.IsNullOrEmpty(pf86)) yield return Path.Combine(pf86!, "Microsoft", "Edge", "Application", "msedge.exe");
                if (!string.IsNullOrEmpty(pf)) yield return Path.Combine(pf!, "Microsoft", "Edge", "Application", "msedge.exe");
                if (!string.IsNullOrEmpty(pf)) yield return Path.Combine(pf!, "Chromium", "Application", "chrome.exe");
                // PATH 命令名
                foreach (var n in new[] {"google-chrome.exe","chrome.exe","chromium.exe","google-chrome","chrome","chromium"})
                    foreach (var p in SearchPath(n)) yield return p;
            }
            else if (os == PlatformID.MacOSX || os == PlatformID.Unix)
            {
                yield return "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome";
                yield return "/Applications/Microsoft Edge.app/Contents/MacOS/Microsoft Edge";
                foreach (var n in new[] {"google-chrome","google-chrome-stable","microsoft-edge","msedge","chromium","chromium-browser"})
                    foreach (var p in SearchPath(n)) yield return p;
            }
            else
            {
                foreach (var n in new[] {"google-chrome","chromium","msedge"})
                    foreach (var p in SearchPath(n)) yield return p;
            }
        }

        foreach (var c in Candidates())
        {
            try { if (File.Exists(c)) return c; } catch { }
        }
        return null;
    }

    private static IEnumerable<string> SearchPath(string executableName)
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var sep = OperatingSystem.IsWindows() ? ';' : ':';
        foreach (var dir in pathEnv.Split(sep, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var full = Path.Combine(dir, executableName);
            yield return full;
            if (OperatingSystem.IsWindows() && !executableName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                yield return full + ".exe";
        }
    }

    private string ResolveUserDataDir()
    {
        var configured = _configuration["XHS:BrowserSettings:UserDataDir"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            var full = ToAbsolutePath(configured);
            Directory.CreateDirectory(full);
            return full;
        }
        var projectRoot = Path.GetDirectoryName(Path.GetDirectoryName(AppContext.BaseDirectory)) ?? AppContext.BaseDirectory;
        var def = Path.Combine(projectRoot, "UserDataDir");
        Directory.CreateDirectory(def);
        return def;
    }

    private static string ToAbsolutePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return path;
        if (Path.IsPathRooted(path)) return path;
        var projectRoot = Path.GetDirectoryName(Path.GetDirectoryName(AppContext.BaseDirectory)) ?? AppContext.BaseDirectory;
        return Path.Combine(projectRoot, path);
    }

    // === 公共接口 ===
    public void BeginOperation() => Interlocked.Increment(ref _busyOperations);
    public void EndOperation() { var v = Interlocked.Decrement(ref _busyOperations); if (v < 0) Interlocked.Exchange(ref _busyOperations, 0); }

    public Task<bool> IsConnectionHealthyAsync()
    {
        try
        {
            return Task.FromResult(_browserContext != null);
        }
        catch { return Task.FromResult(false); }
    }

    public async Task EnsureSessionFreshAsync(CancellationToken ct = default)
    {
        if (_disposed) return;
        if (_browserContext is null) return;

        var now = DateTimeOffset.UtcNow;

        if (_connectionConfig.ContextRecycleMinutes > 0 &&
            _contextCreatedAt != DateTimeOffset.MinValue)
        {
            var lifetime = now - _contextCreatedAt;
            var recycleThreshold = TimeSpan.FromMinutes(_connectionConfig.ContextRecycleMinutes);
            if (lifetime >= recycleThreshold)
            {
                if (Interlocked.CompareExchange(ref _busyOperations, 0, 0) == 0)
                {
                    ct.ThrowIfCancellationRequested();
                    _logger.LogInformation("浏览器上下文已运行 {Lifetime}，达到轮换阈值 {Threshold}，准备重建。", lifetime, recycleThreshold);
                    await TryReconnectAsync().ConfigureAwait(false);
                    return;
                }
                else
                {
                    _logger.LogDebug("检测到上下文寿命超过阈值，但当前存在 {Operations} 个操作，延迟轮换。", Interlocked.CompareExchange(ref _busyOperations, 0, 0));
                }
            }
        }

        if (_connectionConfig.CookieRenewalThresholdMinutes > 0)
        {
            await RenewCookiesIfNeededAsync(now, ct).ConfigureAwait(false);
        }
    }

    private async Task RenewCookiesIfNeededAsync(DateTimeOffset now, CancellationToken ct)
    {
        if (_browserContext is null) return;
        if (Interlocked.CompareExchange(ref _busyOperations, 0, 0) > 0) return;

        var cookies = await _browserContext.CookiesAsync().ConfigureAwait(false);
        var session = cookies.FirstOrDefault(c => string.Equals(c.Name, "web_session", StringComparison.OrdinalIgnoreCase));
        if (session is null) return;

        var expiresSeconds = (double)session.Expires;
        if (double.IsNaN(expiresSeconds) || expiresSeconds <= 0) return;
        DateTimeOffset expiresAt;
        try
        {
            expiresAt = DateTimeOffset.FromUnixTimeSeconds(Convert.ToInt64(Math.Floor(expiresSeconds)));
        }
        catch
        {
            return;
        }

        var threshold = TimeSpan.FromMinutes(_connectionConfig.CookieRenewalThresholdMinutes);
        if (expiresAt - now > threshold) return;

        if (_lastCookieRefresh != DateTimeOffset.MinValue && now - _lastCookieRefresh < TimeSpan.FromMinutes(1))
        {
            return;
        }

        ct.ThrowIfCancellationRequested();

        BeginOperation();
        try
        {
            _logger.LogInformation("web_session Cookie 将于 {Remaining} 后过期，刷新主页以续期。", expiresAt - now);
            var page = await GetPageAsync().ConfigureAwait(false);
            await page.ReloadAsync(new PageReloadOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle,
                Timeout = 20000
            }).ConfigureAwait(false);
            _lastCookieRefresh = DateTimeOffset.UtcNow;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "刷新 Cookie 时发生异常，将在下一个周期重试。");
        }
        finally
        {
            EndOperation();
        }
    }

    public async Task<IBrowserContext> GetBrowserContextAsync()
    {
        if (_browserContext != null) return _browserContext;
        await InitializePlaywrightAsync();
        if (_browserContext == null) throw new InvalidOperationException("浏览器未初始化");
        return _browserContext;
    }

    public async Task<IPage> GetPageAsync()
    {
        if (_page != null) return _page;
        var ctx = await GetBrowserContextAsync();
        _page = ctx.Pages.FirstOrDefault() ?? await ctx.NewPageAsync();
        return _page;
    }

    /// <summary>
    /// 获取抽象页面（IAutoPage）。
    /// 基于当前受管页面进行包装，便于平台层逐步迁移到自动化抽象。
    /// </summary>
    public async Task<HushOps.Core.Automation.Abstractions.IAutoPage> GetAutoPageAsync()
    {
        var page = await GetPageAsync();
        return HushOps.Core.Runtime.Playwright.PlaywrightAutoFactory.Wrap(page);
    }

    public async Task<bool> IsLoggedInAsync()
    {
        try
        {
            var page = await GetPageAsync();
            var cookies = await page.Context.CookiesAsync();
            var webSessionCookie = cookies.FirstOrDefault(c => c.Name == "web_session");
            return webSessionCookie != null && !string.IsNullOrEmpty(webSessionCookie.Value);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "检查登录状态失败");
            return false;
        }
    }

    /// <summary>
    /// 确保以可视化模式显示浏览器以便登录。
    /// 若当前为 Headless 模式，将以相同用户数据目录重启为 Headful；随后打开目标 URL（默认探索页）。
    /// </summary>
    public async Task<bool> EnsureHeadfulForLoginAsync(string? url = null)
    {
        try
        {
            BeginOperation();
            await _semaphore.WaitAsync();
            try
            {
                var target = string.IsNullOrWhiteSpace(url) ? "https://www.xiaohongshu.com/explore" : url!;

                // 若已是可视化模式，仅确保页面存在并跳转
                if (_browserContext != null && !_currentHeadless)
                {
                    var page = await GetPageAsync();
                    try { await page.GotoAsync(target, new() { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 120_000 }); } catch { }
                    return true;
                }

                // 设置覆盖并重启为 Headful
                _headlessOverride = false;
                await DisconnectSafely();
                await InitializePlaywrightAsync();
                var page2 = await GetPageAsync();
                try { await page2.GotoAsync(target, new() { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 120_000 }); } catch { }
                return true;
            }
            finally
            {
                _semaphore.Release();
                EndOperation();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "切换为可视化模式失败（可能当前环境无 GUI）");
            return false;
        }
    }

    /// <summary>
    /// 页面探测实现：打开 URL、采样 HTML、按别名逐一查询并返回明细。
    /// 仅执行读取与 DOM 查询，不进行写操作；用于真实环境下修正选择器与结构变更。
    /// </summary>
    public async Task<PageProbeResult> ProbePageAsync(string? url = null, List<string>? aliases = null, int maxHtmlKb = 64, CancellationToken cancellationToken = default)
    {
        await InitializePlaywrightAsync();
        var page = await GetPageAsync();

        var target = string.IsNullOrWhiteSpace(url) ? "https://www.xiaohongshu.com/explore" : url!;
        try
        {
            await page.GotoAsync(target, new() { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 120_000 });

            aliases ??= new List<string>
            {
                "SearchInput", "SearchButton", "MainScrollContainer", "NoteItem", "NoteTitle",
                "NoteAuthor", "NoteVisibleLink", "NoteCoverImage", "CommentButton", "likeButton",
                "favoriteButton", "NoteDetailModal", "DetailPageCommentInput", "DetailPageCommentSubmit"
            };

            var details = new List<PageProbeAliasDetail>();
            foreach (var alias in aliases.Distinct(StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var selectors = _domElementManager.GetSelectors(alias);
                string? firstMatched = null;
                int matchCount = 0;
                string? firstMarkup = null; // 保留首个命中元素的安全标记采样

                foreach (var sel in selectors)
                {
                    try
                    {
                        var loc = page.Locator(sel);
                        var count = await loc.CountAsync();
                        if (count > 0)
                        {
                            matchCount += count;
                            if (firstMatched == null)
                            {
                                firstMatched = sel;
                                var html = await loc.First.InnerHTMLAsync();
                                firstMarkup = HtmlSanitizer.SafeTruncate(HtmlSanitizer.SanitizeForLogging(html ?? string.Empty), maxHtmlKb);
                            }
                        }
                    }
                    catch
                    {
                        // 忽略单个选择器异常，继续尝试
                    }
                }

                details.Add(new PageProbeAliasDetail(alias, firstMatched, matchCount, firstMarkup));
            }

            var fullHtml = await page.ContentAsync();
            var sample = HtmlSanitizer.SafeTruncate(HtmlSanitizer.SanitizeForLogging(fullHtml), maxHtmlKb);

            var result = new PageProbeResult(true, target, sample, details, "探测完成");

            var writeFiles = _configuration.GetValue("XHS:Probe:WriteFiles", true);
            if (writeFiles)
            {
                try
                {
                    string fileName = "probe.json";
                    if (target.Contains("/explore/item", StringComparison.OrdinalIgnoreCase)) fileName = "detail.json";
                    else if (target.Contains("/explore", StringComparison.OrdinalIgnoreCase)) fileName = "explore.json";

                    var entry = await _jsonStore.SaveAsync(Path.Combine(".probe", fileName), result, CancellationToken.None);
                    _logger.LogInformation("已写入页面探测结果: {Path}", entry.FullPath);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "写入 .probe 探测结果失败（忽略）");
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            return new PageProbeResult(false, target, string.Empty, new List<PageProbeAliasDetail>(), $"页面探测异常: {ex.Message}");
        }

        // 脱敏与截断已提取到 HtmlSanitizer 以便单测覆盖
    }

    public async Task ReleaseBrowserAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            await DisconnectSafely();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task DisconnectSafely()
    {
        try
        {
            if (_page != null) _page = null;
            if (_browserContext != null)
            {
                try { await _browserContext.CloseAsync(); } catch { }
                _browserContext = null;
            }
            if (_browser != null)
            {
                try { await _browser.CloseAsync(); } catch { }
                _browser = null;
            }
            if (_playwright != null)
            {
                _playwright.Dispose();
                _playwright = null;
            }
            _contextCreatedAt = DateTimeOffset.MinValue;
            _lastCookieRefresh = DateTimeOffset.MinValue;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "断开浏览器连接时出现异常（忽略）");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        await _semaphore.WaitAsync();
        try
        {
            _healthCheckTimer?.Dispose();
            await DisconnectSafely();
            _disposed = true;
        }
        finally
        {
            _semaphore.Release();
        }
    }
}

/// <summary>
/// Playwright 持久化上下文的启动计划（仅用于测试与审计）。
/// </summary>
public sealed record PlaywrightLaunchPlan(
    string UserDataDir,
    bool Headless,
    string? ExecutablePath,
    string? Channel,
    IReadOnlyList<string> Args
);
