using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace XiaoHongShuMCP.Services;

/// <summary>
/// 单用户浏览器管理服务 - 集成 Playwright
/// </summary>
public class PlaywrightBrowserManager : IBrowserManager, IAsyncDisposable
{
    private readonly ILogger<PlaywrightBrowserManager> _logger;
    private readonly IConfiguration _configuration;
    private readonly IHumanizedInteractionService _humanizedInteraction;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IBrowserContext? _browserContext;
    private IPage? _page;
    private bool _disposed = false;

    public PlaywrightBrowserManager(
        ILogger<PlaywrightBrowserManager> logger,
        IConfiguration configuration,
        IHumanizedInteractionService humanizedInteraction)
    {
        _logger = logger;
        _configuration = configuration;
        _humanizedInteraction = humanizedInteraction;
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

            var remoteDebuggingPort = _configuration.GetValue<int>("BrowserSettings:RemoteDebuggingPort", 9222);
            var endpointURL = $"http://localhost:{remoteDebuggingPort}";

            _browser = await _playwright.Chromium.ConnectOverCDPAsync(endpointURL);
            _logger.LogInformation("成功连接到现有浏览器实例");
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// 获取浏览器上下文 - 单用户模式实现
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
            var page = context.Pages.FirstOrDefault();
            if (page == null)
            {
                page = await context.NewPageAsync();
            }
            _page = page;

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
    /// 导航到小红书首页
    /// </summary>
    public async Task<bool> NavigateToXiaoHongShuAsync()
    {
        try
        {
            var page = await GetPageAsync();
            var baseUrl = _configuration.GetValue<string>("BaseUrl", "https://www.xiaohongshu.com");

            _logger.LogInformation("导航到小红书: {Url}", baseUrl);

            await page.GotoAsync(baseUrl, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle,
                Timeout = 30000
            });

            // 等待页面加载完成
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导航到小红书失败");
            return false;
        }
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
    /// 等待并处理登录
    /// </summary>
    public async Task<bool> WaitForLoginAsync(int timeoutSeconds = 60)
    {
        try
        {
            var page = await GetPageAsync();
            _logger.LogInformation("等待完成登录，超时时间: {Timeout}秒", timeoutSeconds);

            // 等待用户手动登录或登录状态改变
            await page.WaitForFunctionAsync(@"
                () => {
                    return document.querySelector('.avatar, .user-info') !== null;
                }
            ", new PageWaitForFunctionOptions {Timeout = timeoutSeconds * 1000});

            _logger.LogInformation("登录成功");
            return true;
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("登录超时");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "等待登录过程中出现异常");
            return false;
        }
    }

    /// <summary>
    /// 释放浏览器资源
    /// </summary>
    public async Task ReleaseBrowserAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            if (_browserContext != null)
            {
                await _browserContext.CloseAsync();
                _browserContext = null;
                _page = null;

                _logger.LogInformation("释放浏览器资源完成");
            }
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
            _logger.LogInformation("正在关闭浏览器资源...");

            // 关闭浏览器上下文
            if (_browserContext != null)
            {
                try
                {
                    await _browserContext.CloseAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "关闭浏览器上下文时出现异常");
                }
            }

            // 关闭浏览器
            if (_browser != null)
            {
                await _browser.CloseAsync();
            }

            // 释放 Playwright
            _playwright?.Dispose();

            _browserContext = null;
            _page = null;
            _disposed = true;

            _logger.LogInformation("浏览器资源释放完成");
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
