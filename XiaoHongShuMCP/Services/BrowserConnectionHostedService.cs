using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using HushOps.Core.Runtime.Playwright.AntiDetection;
using ServiceXhsSettings = XiaoHongShuMCP.Services.XhsSettings;

namespace XiaoHongShuMCP.Services;

/// <summary>
/// 浏览器连接后台服务
/// 在MCP服务器启动时自动连接浏览器并验证小红书登录状态
/// </summary>
public class BrowserConnectionHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BrowserConnectionHostedService> _logger;
    private readonly IPlaywrightAntiDetectionPipeline _antiDetection;
    private readonly IOptions<ServiceXhsSettings> _settings;

    public BrowserConnectionHostedService(
        IServiceProvider serviceProvider,
        ILogger<BrowserConnectionHostedService> logger,
        IPlaywrightAntiDetectionPipeline antiDetection,
        IOptions<ServiceXhsSettings> settings)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _antiDetection = antiDetection;
        _settings = settings;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var connection = _settings.Value?.BrowserSettings?.Connection
                             ?? new ServiceXhsSettings.BrowserSettingsSection.ConnectionSection();

            var initialDelay = TimeSpan.FromSeconds(Math.Max(0, connection.InitialDelaySeconds));
            var baseRetrySeconds = Math.Max(1, connection.RetryIntervalSeconds);
            var maxRetrySeconds = Math.Max(baseRetrySeconds, connection.RetryIntervalMaxSeconds);
            var healthInterval = TimeSpan.FromSeconds(Math.Max(1, connection.HealthCheckIntervalSeconds));

            if (initialDelay > TimeSpan.Zero)
            {
                _logger.LogDebug("首次连接前延迟 {Delay}", initialDelay);
                await Task.Delay(initialDelay, stoppingToken);
            }

            var currentRetry = TimeSpan.FromSeconds(baseRetrySeconds);

            while (!stoppingToken.IsCancellationRequested)
            {
                var connected = await EnsureBrowserSessionAsync(stoppingToken).ConfigureAwait(false);

                if (connected)
                {
                    currentRetry = TimeSpan.FromSeconds(baseRetrySeconds);
                    _logger.LogDebug("浏览器健康，{NextCheck} 后再次检测", healthInterval);
                    await Task.Delay(healthInterval, stoppingToken);
                    continue;
                }

                _logger.LogWarning("浏览器连接失败，将在 {Delay} 后重试", currentRetry);
                await Task.Delay(currentRetry, stoppingToken);

                var nextSeconds = Math.Min(maxRetrySeconds, Math.Max(baseRetrySeconds, currentRetry.TotalSeconds * 2));
                currentRetry = TimeSpan.FromSeconds(nextSeconds);
            }
        }
        catch (OperationCanceledException)
        {
            // 服务正常停止，忽略此异常
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "浏览器连接后台服务异常");
        }
    }

    /// <summary>
    /// 检查现有会话是否健康，必要时触发重连。
    /// </summary>
    private async Task<bool> EnsureBrowserSessionAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var accountManager = scope.ServiceProvider.GetRequiredService<IAccountManager>();
            var browserManager = scope.ServiceProvider.GetRequiredService<IBrowserManager>();

            if (await accountManager.IsLoggedInAsync().ConfigureAwait(false))
            {
                await browserManager.EnsureSessionFreshAsync(cancellationToken).ConfigureAwait(false);
                return true;
            }

            _logger.LogInformation("浏览器未检测到登录态，开始自动重连");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "检测浏览器状态时发生异常，将尝试重新连接");
        }

        return await TryConnectToBrowserAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 尝试连接浏览器并设置用户信息API监听
    /// </summary>
    private async Task<bool> TryConnectToBrowserAsync(CancellationToken cancellationToken)
    {
        var connected = false;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var scope = _serviceProvider.CreateScope();
            var accountManager = scope.ServiceProvider.GetRequiredService<IAccountManager>();
            var browserManager = scope.ServiceProvider.GetRequiredService<IBrowserManager>();

            var result = await accountManager.ConnectToBrowserAsync().ConfigureAwait(false);

            if (result.Success)
            {
                try
                {
                    var context = await browserManager.GetBrowserContextAsync().ConfigureAwait(false);
                    await _antiDetection.ApplyAsync(context, cancellationToken).ConfigureAwait(false);
                    _logger.LogInformation("已应用反检测管线到浏览器上下文（集中化注入，业务层零注入）");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "应用反检测管线失败（将以审计模式继续）");
                }

                if (result.Data)
                {
                    await SetupUserInfoApiMonitoringAsync(browserManager, cancellationToken).ConfigureAwait(false);
                    await NavigateToHomePageAsync(browserManager, cancellationToken).ConfigureAwait(false);
                }

                await browserManager.EnsureSessionFreshAsync(cancellationToken).ConfigureAwait(false);

                connected = true;
                return connected;
            }

            _logger.LogWarning("浏览器连接失败: {Error}", result.ErrorMessage);
            _logger.LogError("╔══════════════════════════════════════════════════╗");
            _logger.LogError("║                浏览器自动连接失败                ║");
            _logger.LogError("╠══════════════════════════════════════════════════╣");
            _logger.LogError("║ 建议排查：                                       ║");
            _logger.LogError("║   1) 已执行 Playwright 浏览器安装                ║");
            _logger.LogError("║      - dotnet 工程：在仓库根目录运行             ║");
            _logger.LogError("║        `pwsh ./bin/Debug/net8.0/playwright.ps1 install` ║");
            _logger.LogError("║      - 或者系统已安装 Chrome/Edge，并通过        ║");
            _logger.LogError("║        XHS:BrowserSettings:Channel/ExecutablePath 指定 ║");
            _logger.LogError("║   2) `XHS:BrowserSettings:UserDataDir` 可写       ║");
            _logger.LogError("║   3) 无安全软件拦截浏览器启动                    ║");
            _logger.LogError("║   4) 重启应用再试                                ║");
            _logger.LogError("╚══════════════════════════════════════════════════╝");

            _logger.LogInformation("提示：MCP 服务器已正常运行，可稍后再次尝试连接");
            connected = false;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError("浏览器连接配置错误: {Message}", ex.Message);
            _logger.LogError("详细错误信息已在上方显示，请按照指导步骤操作");
            connected = false;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "自动连接浏览器异常");
            _logger.LogError("发生了未预期的错误，建议故障排除步骤：");
            _logger.LogError("1. 重启应用程序");
            _logger.LogError("2. 执行 Playwright 浏览器安装脚本");
            _logger.LogError("3. 指定备用浏览器 Channel 或 ExecutablePath");
            _logger.LogError("4. 尝试以管理员权限运行应用程序");
            _logger.LogError("5. 如问题持续存在，请联系技术支持");
            _logger.LogInformation("如需手动连接，请在AI客户端调用 ConnectToBrowser 工具");
            connected = false;
        }

        return connected;
    }

    /// <summary>
    /// 设置用户信息API监听
    /// 监听 https://edith.xiaohongshu.com/api/sns/web/v2/user/me 的GET请求
    /// </summary>
    private async Task SetupUserInfoApiMonitoringAsync(IBrowserManager browserManager, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var page = await browserManager.GetPageAsync();

            if (page == null)
            {
                _logger.LogWarning("无法获取浏览器页面，跳过用户信息API监听设置");
                return;
            }

            // 设置响应监听器
            page.Response += async (_, response) =>
            {
                try
                {
                    var url = response.Url;
                    if (url.Contains("edith.xiaohongshu.com/api/sns/web/v2/user/me") &&
                        response.Request.Method == "GET" &&
                        response.Status == 200)
                    {
                        var responseBody = await response.TextAsync();

                        using var scope = _serviceProvider.CreateScope();
                        var accountManager = scope.ServiceProvider.GetRequiredService<IAccountManager>();

                        if (accountManager.UpdateFromApiResponse(responseBody))
                        {
                            _logger.LogDebug("用户信息API响应已更新: {UserSummary}", accountManager.GetUserInfoSummary());
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "处理用户信息API响应时发生异常（不影响主功能）");
                }
            };

            _logger.LogInformation("用户信息API监听已设置，将自动更新用户信息");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "设置用户信息API监听失败（不影响主功能）");
        }
    }

    /// <summary>
    /// 连接浏览器成功后，自动导航到小红书主页（可由配置项 BaseUrl 覆盖，默认探索页）。
    /// </summary>
    private async Task NavigateToHomePageAsync(IBrowserManager browserManager, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var scope = _serviceProvider.CreateScope();
            var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            var pageLoadWaitService = scope.ServiceProvider.GetRequiredService<IPageLoadWaitService>();

            // 允许通过配置覆盖默认主页地址；默认跳转到探索页以便后续功能可用
            var targetUrl = configuration["BaseUrl"] ?? "https://www.xiaohongshu.com/explore";

            var page = await browserManager.GetPageAsync();
            var currentUrl = page.Url;

            if (currentUrl.Contains("xiaohongshu.com")
                && currentUrl.Contains("/explore")
                && !currentUrl.Contains("/explore/")
                && !currentUrl.Contains("/explore?"))
            {
                _logger.LogInformation("已位于小红书探索/主页，无需跳转。当前: {Url}", currentUrl);
                return;
            }

            _logger.LogInformation("浏览器连接成功，正在导航到小红书主页: {Target}", targetUrl);

            browserManager.BeginOperation();
            try
            {
                try
                {
                    await page.GotoAsync(targetUrl, new PageGotoOptions
                    {
                        WaitUntil = WaitUntilState.DOMContentLoaded,
                        Timeout = 15000
                    });
                }
                catch (Microsoft.Playwright.PlaywrightException)
                {
                    _logger.LogWarning("页面在导航主页时关闭，尝试重新获取页面并重试");
                    page = await browserManager.GetPageAsync();
                    await page.GotoAsync(targetUrl, new PageGotoOptions
                    {
                        WaitUntil = WaitUntilState.DOMContentLoaded,
                        Timeout = 15000
                    });
                }

                // 统一的页面加载等待（具备重试/降级）
                var autoPage = HushOps.Core.Runtime.Playwright.PlaywrightAutoFactory.Wrap(page);
                await pageLoadWaitService.WaitForPageLoadAsync(autoPage, cancellationToken);
                _logger.LogInformation("已导航到小红书主页: {FinalUrl}", page.Url);
            }
            finally
            {
                browserManager.EndOperation();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "导航到小红书主页失败（不影响主功能）");
        }
    }
}
