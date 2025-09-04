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
    /// 尝试连接浏览器
    /// </summary>
    private async Task TryConnectToBrowserAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var accountManager = scope.ServiceProvider.GetRequiredService<IAccountManager>();

            var result = await accountManager.ConnectToBrowserAsync();

            if (result.Success)
            {
                var loginStatus = result.Data?.IsLoggedIn == true ? "已登录" : "未登录";
                var userInfo = result.Data?.Username ?? result.Data?.UserId ?? "未知用户";

                _logger.LogInformation("浏览器连接成功 | 登录状态: {LoginStatus} | 用户: {UserInfo}",
                    loginStatus, userInfo);
            }
            else
            {
                _logger.LogWarning("浏览器连接失败: {Error}", result.ErrorMessage);
                _logger.LogInformation("请确保浏览器已启动远程调试模式（--remote-debugging-port=9222）");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "自动连接浏览器异常");
            _logger.LogInformation("如需手动连接，请在AI客户端调用 ConnectToBrowser 工具");
        }
    }
}
