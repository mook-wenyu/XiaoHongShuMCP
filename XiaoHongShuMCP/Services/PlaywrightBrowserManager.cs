using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace XiaoHongShuMCP.Services;

/// <summary>
/// 浏览器管理服务 - 集成 Playwright
/// </summary>
public class PlaywrightBrowserManager : IBrowserManager, IAsyncDisposable
{
    private readonly ILogger<PlaywrightBrowserManager> _logger;
    private readonly IConfiguration _configuration;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IBrowserContext? _browserContext;
    private IPage? _page;
    private bool _disposed;
    private string? _lastEndpointUrl;
    private DateTime _lastConnectionTime;
    private readonly Timer _healthCheckTimer;
    private bool _isReconnecting;

    public PlaywrightBrowserManager(
        ILogger<PlaywrightBrowserManager> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        
        // 初始化健康检查定时器（每30秒检查一次）
        _healthCheckTimer = new Timer(CheckConnectionHealth, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    /// <summary>
    /// 连接健康检查
    /// </summary>
    private async void CheckConnectionHealth(object? state)
    {
        if (_disposed || _browser == null || _isReconnecting)
            return;

        try
        {
            // 检查浏览器连接是否仍然有效
            var contexts = _browser.Contexts;
            if (contexts == null || !contexts.Any())
            {
                _logger.LogWarning("浏览器连接可能已断开，尝试重连...");
                await TryReconnectAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "连接健康检查失败，尝试重连...");
            await TryReconnectAsync();
        }
    }

    /// <summary>
    /// 尝试重新连接到浏览器
    /// </summary>
    private async Task TryReconnectAsync()
    {
        if (_isReconnecting || string.IsNullOrEmpty(_lastEndpointUrl))
            return;

        _isReconnecting = true;
        await _semaphore.WaitAsync();
        
        try
        {
            _logger.LogInformation("开始重新连接到浏览器...");
            
            // 清理现有连接（不关闭浏览器进程）
            await DisconnectSafely();
            
            // 重新初始化连接
            await InitializePlaywrightAsync();
            
            _logger.LogInformation("重新连接成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "重新连接失败");
        }
        finally
        {
            _semaphore.Release();
            _isReconnecting = false;
        }
    }

    /// <summary>
    /// 安全断开连接（不关闭浏览器进程）
    /// </summary>
    private async Task DisconnectSafely()
    {
        try
        {
            // 只断开连接，不关闭浏览器进程
            if (_browserContext != null)
            {
                // 不调用 CloseAsync，只清理引用
                _browserContext = null;
                _page = null;
            }

            if (_browser != null)
            {
                // 重要：不调用 _browser.CloseAsync()，这会关闭整个浏览器进程
                // 只断开 Playwright 的连接
                _browser = null;
            }

            // 重新初始化 Playwright
            if (_playwright != null)
            {
                _playwright.Dispose();
                _playwright = null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "断开连接时出现异常");
        }
    }

    /// <summary>
    /// 检查浏览器连接是否健康
    /// </summary>
    public async Task<bool> IsConnectionHealthyAsync()
    {
        try
        {
            if (_browser == null)
                return false;

            var contexts = _browser.Contexts;
            return contexts.Any();
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 移除 webdriver 标识以降低检测风险
    /// </summary>
    /// <param name="page">浏览器页面实例</param>
    private async Task RemoveWebDriverIdentifiersAsync(IPage page)
    {
        try
        {
            _logger.LogDebug("正在移除 webdriver 标识...");

            // 移除 navigator.webdriver 属性
            await page.AddInitScriptAsync(@"
                Object.defineProperty(navigator, 'webdriver', {
                    get: () => false,
                    configurable: true
                });

                // 移除 Chrome DevTools Runtime 相关属性
                Object.defineProperty(window, 'chrome', {
                    get: () => ({
                        runtime: {},
                        loadTimes: function() {},
                        csi: function() {},
                        app: {}
                    }),
                    configurable: true
                });

                // 修改 plugins 长度
                Object.defineProperty(navigator, 'plugins', {
                    get: () => [1, 2, 3, 4, 5],
                    configurable: true
                });

                // 修改 languages 属性
                Object.defineProperty(navigator, 'languages', {
                    get: () => ['zh-CN', 'zh', 'en'],
                    configurable: true
                });

                // 移除 webdriver 相关的 window 属性
                delete window.cdc_adoQpoasnfa76pfcZLmcfl_Array;
                delete window.cdc_adoQpoasnfa76pfcZLmcfl_Promise;
                delete window.cdc_adoQpoasnfa76pfcZLmcfl_Symbol;
                delete window.$chrome_asyncScriptInfo;
                delete window.$cdc_asdjflasutopfhvcZLmcfl_;
            ");

            _logger.LogDebug("webdriver 标识移除完成");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "移除 webdriver 标识时发生错误，继续执行");
        }
    }

    private static string RedactUrl(string url)
    {
        if (string.IsNullOrEmpty(url)) return url;
        var q = url.IndexOf('?');
        return q > 0 ? url[..q] : url;
    }

    private async Task ValidateRemoteDebuggingEndpointAsync(string baseUrl)
    {
        using var http = new HttpClient();
        
        // 从配置文件读取超时时间，默认为10秒
        var timeoutSeconds = _configuration.GetValue<int>("BrowserSettings:ConnectionTimeoutSeconds", 10);
        http.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
        
        _logger.LogDebug($"验证远程调试端点: {baseUrl}, 超时时间: {timeoutSeconds}秒");
        try
        {
            // 调用 /json/version 进行最小验证（本地端口，无敏感信息）
            var resp = await http.GetAsync($"{baseUrl}/json/version");
            resp.EnsureSuccessStatusCode();
            await using var s = await resp.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(s);
            if (!doc.RootElement.TryGetProperty("webSocketDebuggerUrl", out _))
            {
                _logger.LogWarning("DevTools 端点响应缺少 webSocketDebuggerUrl，可能未开启远程调试");
            }
        }
        catch (HttpRequestException httpEx)
        {
            _logger.LogError(httpEx, "浏览器连接失败 - HTTP请求异常");
            _logger.LogError("╔══════════════════════════════════════════════════╗");
            _logger.LogError("║              浏览器启动失败解决步骤              ║");
            _logger.LogError("╠══════════════════════════════════════════════════╣");
            _logger.LogError("║ 步骤 1: 完全关闭浏览器                           ║");
            _logger.LogError("║   - 关闭所有浏览器窗口                           ║");
            _logger.LogError("║   - 右键点击任务栏浏览器图标，选择\"关闭窗口\"   ║");
            _logger.LogError("║                                                  ║");
            _logger.LogError("║ 步骤 2: 在任务管理器中结束浏览器进程             ║");
            _logger.LogError("║   - 按 Ctrl+Shift+Esc 打开任务管理器             ║");
            _logger.LogError("║   - 找到所有 Chrome 或 Edge 进程                 ║");
            _logger.LogError("║   - 右键选择\"结束任务\"                         ║");
            _logger.LogError("║   - 确保没有残留的浏览器进程                     ║");
            _logger.LogError("║                                                  ║");
            _logger.LogError("║ 步骤 3: 重新启动浏览器（带远程调试参数）         ║");
            _logger.LogError("║   Chrome:                                        ║");
            _logger.LogError("║   chrome.exe --remote-debugging-port=9222        ║");
            _logger.LogError("║                                                  ║");
            _logger.LogError("║   Edge:                                          ║");
            _logger.LogError("║   msedge.exe --remote-debugging-port=9222        ║");
            _logger.LogError("║                                                  ║");
            _logger.LogError("║ 步骤 4: 验证远程调试端口                         ║");
            _logger.LogError("║   - 在浏览器中访问: http://localhost:9222        ║");
            _logger.LogError("║   - 应该能看到调试页面列表                       ║");
            _logger.LogError("║                                                  ║");
            _logger.LogError("║ 步骤 5: 重新尝试连接                             ║");
            _logger.LogError("║   - 在AI客户端调用 ConnectToBrowser 工具         ║");
            _logger.LogError("╚══════════════════════════════════════════════════╝");
            throw new InvalidOperationException("浏览器远程调试端口连接失败。请按照上述步骤重新启动浏览器。", httpEx);
        }
        catch (TaskCanceledException timeoutEx)
        {
            _logger.LogError(timeoutEx, $"浏览器连接超时 (超时时间: {timeoutSeconds}秒)");
            _logger.LogError("╔══════════════════════════════════════════════════╗");
            _logger.LogError("║                连接超时解决方案                  ║");
            _logger.LogError("╠══════════════════════════════════════════════════╣");
            _logger.LogError("║ 连接超时可能的原因：                             ║");
            _logger.LogError("║   1. 浏览器未启动或未开启远程调试                ║");
            _logger.LogError("║   2. 端口9222被其他程序占用                      ║");
            _logger.LogError("║   3. 防火墙阻止了端口访问                        ║");
            _logger.LogError("║   4. 系统负载过高，响应缓慢                      ║");
            _logger.LogError("║                                                  ║");
            _logger.LogError("║ 立即检查步骤：                                   ║");
            _logger.LogError("║   1. 在浏览器访问: http://localhost:9222         ║");
            _logger.LogError("║      - 如果无法访问，说明远程调试未启用          ║");
            _logger.LogError("║   2. 检查任务管理器中是否有多个浏览器进程        ║");
            _logger.LogError("║   3. 重启浏览器并使用正确的启动参数              ║");
            _logger.LogError("╚══════════════════════════════════════════════════╝");
            
            throw new TimeoutException($"连接浏览器远程调试端点超时 ({timeoutSeconds}秒)。请检查浏览器是否正确启动了远程调试功能。", timeoutEx);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "验证浏览器远程调试端点时发生未预期的错误");
            _logger.LogError("如果问题持续存在，请尝试：");
            _logger.LogError("1. 重启计算机");
            _logger.LogError("2. 检查是否有安全软件阻止了连接");
            _logger.LogError("3. 尝试使用不同的端口（如9223）");
            throw new InvalidOperationException("浏览器连接验证失败。请按照错误提示重新配置。", ex);
        }
    }

    /// <summary>
    /// 初始化Playwright
    /// </summary>
    private async Task InitializePlaywrightAsync()
    {
        if (_playwright != null) return;

        await _semaphore.WaitAsync();
        try
        {
            if (_playwright != null) return;

            _logger.LogInformation("初始化 Playwright 并连接到现有浏览器...");

            _playwright = await Playwright.CreateAsync();

            var remoteDebuggingPort = _configuration.GetValue("BrowserSettings:RemoteDebuggingPort", 9222);
            var endpointURL = $"http://localhost:{remoteDebuggingPort}";

            // 运行前检查：验证 DevTools/CDP 端点可用（不记录敏感头）
            await ValidateRemoteDebuggingEndpointAsync(endpointURL);

            try
            {
                _browser = await _playwright.Chromium.ConnectOverCDPAsync(endpointURL);
                _lastEndpointUrl = endpointURL;
                _lastConnectionTime = DateTime.UtcNow;
                _logger.LogInformation("成功连接到现有浏览器实例");
            }
            catch (PlaywrightException playwrightEx)
            {
                _logger.LogError(playwrightEx, "Playwright 连接到浏览器失败");
                _logger.LogError("╔══════════════════════════════════════════════════╗");
                _logger.LogError("║            Playwright 浏览器连接失败             ║");
                _logger.LogError("╠══════════════════════════════════════════════════╣");
                _logger.LogError("║ 可能的原因：                                     ║");
                _logger.LogError("║ 1. 浏览器版本与 Playwright 不兼容                ║");
                _logger.LogError("║ 2. 浏览器进程状态异常                            ║");
                _logger.LogError("║ 3. CDP协议连接被中断                             ║");
                _logger.LogError("║                                                  ║");
                _logger.LogError("║ 解决方案：                                       ║");
                _logger.LogError("║ 1. 完全关闭浏览器（包括后台进程）                ║");
                _logger.LogError("║ 2. 在任务管理器中结束所有浏览器进程              ║");
                _logger.LogError("║ 3. 重新启动浏览器：                              ║");
                _logger.LogError("║    chrome.exe --remote-debugging-port=9222       ║");
                _logger.LogError("║ 4. 确保浏览器版本是最新的                        ║");
                _logger.LogError("╚══════════════════════════════════════════════════╝");
            }
            catch (Exception connectionEx)
            {
                _logger.LogError(connectionEx, "浏览器连接过程中发生未预期的错误");
                _logger.LogError("建议的故障排除步骤：");
                _logger.LogError("1. 检查浏览器是否正确启动");
                _logger.LogError("2. 验证端口 {Port} 未被占用", remoteDebuggingPort);
                _logger.LogError("3. 重启浏览器并确保使用正确的调试参数");
                _logger.LogError("4. 如果问题持续，尝试重启应用程序");
            }
        }
        catch (Exception ex) when (!(ex is InvalidOperationException))
        {
            _logger.LogError(ex, "初始化 Playwright 时发生严重错误");
            _logger.LogError("系统级错误，建议：");
            _logger.LogError("1. 重启应用程序");
            _logger.LogError("2. 检查系统资源是否充足");
            _logger.LogError("3. 确认 Playwright 依赖正确安装");
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// 获取浏览器上下文
    /// </summary>
    public async Task<IBrowserContext> GetBrowserContextAsync()
    {
        if (_browserContext != null)
        {
            return _browserContext;
        }

        await InitializePlaywrightAsync();

        if (_browser == null)
            throw new InvalidOperationException("浏览器未初始化或连接失败");

        await _semaphore.WaitAsync();
        try
        {
            if (_browserContext != null)
            {
                return _browserContext;
            }

            _logger.LogInformation("获取默认浏览器上下文...");

            // 当通过CDP连接时，默认上下文通常是第一个
            var context = _browser.Contexts.FirstOrDefault();
            if (context == null)
            {
                _logger.LogInformation("未找到现有上下文，将创建一个新的");
                context = await _browser.NewContextAsync();
            }

            _browserContext = context;

            // 获取或创建一个页面
            var page = context.Pages.FirstOrDefault() ?? await context.NewPageAsync();
            _page = page;

            // 移除 webdriver 标识以降低检测风险
            await RemoveWebDriverIdentifiersAsync(page);

            // 附加网络事件，打印最小信息并脱敏（不输出 Cookie/Authorization）
            try
            {
                context.Request += (_, request) =>
                {
                    try
                    {
                        _logger.LogDebug("HTTP {Method} {Url}", request.Method, RedactUrl(request.Url));
                    }
                    catch { /* ignore logging errors */ }
                };
                context.Response += async (_, response) =>
                {
                    try
                    {
                        var url = RedactUrl(response.Url);
                        _logger.LogDebug("HTTP {Status} {Url}", response.Status, url);
                        await Task.CompletedTask;
                    }
                    catch { /* ignore logging errors */ }
                };
            }
            catch
            {
                // 忽略事件绑定异常，不影响主流程
            }

            _logger.LogInformation("默认浏览器上下文和页面已准备就绪");
            return context;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// 获取页面
    /// </summary>
    public async Task<IPage> GetPageAsync()
    {
        if (_page == null)
        {
            await GetBrowserContextAsync();
            if (_page == null)
                throw new InvalidOperationException("页面未初始化");
        }

        return _page;
    }

    /// <summary>
    /// 检查是否已登录 - 基于 Cookie 检测
    /// 通过检测 web_session cookie 来判断登录状态
    /// </summary>
    public async Task<bool> IsLoggedInAsync()
    {
        try
        {
            var page = await GetPageAsync();

            // 获取所有 cookies
            var cookies = await page.Context.CookiesAsync();
            
            // 查找 web_session cookie
            var webSessionCookie = cookies.FirstOrDefault(c => c.Name == "web_session");
            
            // 检查 web_session 是否存在且有值
            var isLoggedIn = webSessionCookie != null && !string.IsNullOrEmpty(webSessionCookie.Value);

            if (isLoggedIn)
            {
                _logger.LogDebug("检测到有效的 web_session cookie，用户已登录");
            }
            else
            {
                _logger.LogDebug("未检测到有效的 web_session cookie，用户未登录");
            }

            return isLoggedIn;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "检查登录状态失败");
            return false;
        }
    }

    /// <summary>
    /// 释放浏览器资源（安全模式：不关闭浏览器进程）
    /// </summary>
    public async Task ReleaseBrowserAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            _logger.LogInformation("安全释放浏览器连接...");
            await DisconnectSafely();
            _logger.LogInformation("浏览器连接已断开（浏览器进程保持运行）");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "释放浏览器资源失败");
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        await _semaphore.WaitAsync();
        try
        {
            _logger.LogInformation("正在安全关闭浏览器连接...");

            // 停止健康检查定时器
            _healthCheckTimer?.Dispose();

            // 安全断开连接（不关闭浏览器进程）
            await DisconnectSafely();

            _disposed = true;
            _logger.LogInformation("浏览器连接已安全断开（浏览器进程保持运行，可重连）");
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
