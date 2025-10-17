using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HushOps.Servers.XiaoHongShu.Services.Browser;

/// <summary>
/// 中文：在应用启动时自动安装 Playwright Chromium 浏览器，降低用户手动安装复杂度。
/// English: Automatically installs Playwright Chromium browser on application startup to reduce manual installation complexity.
/// </summary>
internal sealed class PlaywrightBrowserInstallService : BackgroundService
{
    private readonly ILogger<PlaywrightBrowserInstallService> _logger;

    public PlaywrightBrowserInstallService(ILogger<PlaywrightBrowserInstallService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[PlaywrightInstall] 开始检查并安装 Playwright Chromium 浏览器");

        try
        {
            var exitCode = await Task.Run(() =>
            {
                try
                {
                    // 使用 Playwright 官方 API 安装 Chromium
                    // Windows 默认安装路径：%USERPROFILE%\AppData\Local\ms-playwright
                    // 可通过 PLAYWRIGHT_BROWSERS_PATH 环境变量自定义路径
                    return Microsoft.Playwright.Program.Main(new[] { "install", "chromium" });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[PlaywrightInstall] Chromium 安装过程发生异常");
                    return -1;
                }
            }, stoppingToken).ConfigureAwait(false);

            if (exitCode == 0)
            {
                _logger.LogInformation("[PlaywrightInstall] Chromium 浏览器安装成功");
            }
            else
            {
                _logger.LogError("[PlaywrightInstall] Chromium 浏览器安装失败，退出代码：{ExitCode}", exitCode);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("[PlaywrightInstall] Chromium 安装被取消");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PlaywrightInstall] Chromium 安装失败");
        }
    }
}
