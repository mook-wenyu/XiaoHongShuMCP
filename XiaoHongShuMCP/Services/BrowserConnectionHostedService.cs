using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace XiaoHongShuMCP.Services;

/// <summary>
/// 浏览器连接后台服务
/// 在MCP服务器启动时自动连接浏览器并验证小红书登录状态
/// </summary>
public class BrowserConnectionHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BrowserConnectionHostedService> _logger;

    public BrowserConnectionHostedService(
        IServiceProvider serviceProvider,
        ILogger<BrowserConnectionHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("开始自动连接浏览器...");

            await TryConnectToBrowserAsync();
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
    /// 尝试连接浏览器并设置用户信息API监听
    /// </summary>
    private async Task TryConnectToBrowserAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var accountManager = scope.ServiceProvider.GetRequiredService<IAccountManager>();
            var browserManager = scope.ServiceProvider.GetRequiredService<IBrowserManager>();

            var result = await accountManager.ConnectToBrowserAsync();

            if (result.Success)
            {
                // 如果连接成功，设置用户信息API监听
                if (result.Data)
                {
                    await SetupUserInfoApiMonitoringAsync(browserManager);
                }
            }
            else
            {
                _logger.LogWarning("浏览器连接失败: {Error}", result.ErrorMessage);
                
                // 输出详细的用户指导
                _logger.LogError("╔══════════════════════════════════════════════════╗");
                _logger.LogError("║                浏览器自动连接失败                ║");
                _logger.LogError("╠══════════════════════════════════════════════════╣");
                _logger.LogError("║ 请按以下步骤手动解决：                           ║");
                _logger.LogError("║                                                  ║");
                _logger.LogError("║ 第一步：完全关闭浏览器                           ║");
                _logger.LogError("║   ✓ 关闭所有浏览器窗口和标签页                   ║");
                _logger.LogError("║   ✓ 右键任务栏浏览器图标 → 关闭所有窗口          ║");
                _logger.LogError("║                                                  ║");
                _logger.LogError("║ 第二步：清理浏览器进程                           ║");
                _logger.LogError("║   ✓ 按 Ctrl+Shift+Esc 打开任务管理器             ║");
                _logger.LogError("║   ✓ 在\"进程\"标签页中找到所有浏览器进程         ║");
                _logger.LogError("║     - Chrome: 查找所有 chrome.exe 进程           ║");
                _logger.LogError("║     - Edge: 查找所有 msedge.exe 进程             ║");
                _logger.LogError("║   ✓ 选中每个进程 → 右键 → 结束任务               ║");
                _logger.LogError("║                                                  ║");
                _logger.LogError("║ 第三步：重新启动浏览器（重要！）                 ║");
                _logger.LogError("║   Windows + R → 输入以下命令之一：               ║");
                _logger.LogError("║                                                  ║");
                _logger.LogError("║   Chrome 用户：                                  ║");
                _logger.LogError("║   chrome.exe --remote-debugging-port=9222        ║");
                _logger.LogError("║                                                  ║");
                _logger.LogError("║   Edge 用户：                                    ║");
                _logger.LogError("║   msedge.exe --remote-debugging-port=9222        ║");
                _logger.LogError("║                                                  ║");
                _logger.LogError("║ 第四步：验证连接                                 ║");
                _logger.LogError("║   在浏览器地址栏输入：                           ║");
                _logger.LogError("║   http://localhost:9222                          ║");
                _logger.LogError("║   应该能看到调试页面列表                         ║");
                _logger.LogError("║                                                  ║");
                _logger.LogError("║ 第五步：重新尝试连接                             ║");
                _logger.LogError("║   在 AI 客户端中调用 ConnectToBrowser 工具       ║");
                _logger.LogError("╚══════════════════════════════════════════════════╝");
                
                _logger.LogInformation("提示：MCP服务器已正常启动，浏览器连接可稍后手动建立");
            }
        }
        catch (InvalidOperationException ex)
        {
            // 这些是来自 PlaywrightBrowserManager 的详细错误，已经包含用户指导
            _logger.LogError("浏览器连接配置错误: {Message}", ex.Message);
            _logger.LogError("详细错误信息已在上方显示，请按照指导步骤操作");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "自动连接浏览器异常");
            
            // 对于未预期的错误，提供通用的故障排除指导
            _logger.LogError("发生了未预期的错误，建议故障排除步骤：");
            _logger.LogError("1. 重启应用程序");
            _logger.LogError("2. 确保浏览器已正确安装");
            _logger.LogError("3. 检查防火墙设置是否阻止了端口 9222");
            _logger.LogError("4. 尝试以管理员权限运行应用程序");
            _logger.LogError("5. 如问题持续存在，请联系技术支持");
            
            _logger.LogInformation("如需手动连接，请在AI客户端调用 ConnectToBrowser 工具");
        }
    }

    /// <summary>
    /// 设置用户信息API监听
    /// 监听 https://edith.xiaohongshu.com/api/sns/web/v2/user/me 的GET请求
    /// </summary>
    private async Task SetupUserInfoApiMonitoringAsync(IBrowserManager browserManager)
    {
        try
        {
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
}
