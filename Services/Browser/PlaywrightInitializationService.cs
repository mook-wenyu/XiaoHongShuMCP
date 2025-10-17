using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace HushOps.Servers.XiaoHongShu.Services.Browser;

/// <summary>
/// 中文：异步初始化 Playwright 引擎并提供单例访问，支持后台预热和延迟初始化。
/// English: Asynchronously initializes Playwright engine and provides singleton access, supports background preheating and lazy initialization.
/// </summary>
internal sealed class PlaywrightInitializationService : IHostedService, IAsyncDisposable
{
    private readonly ILogger<PlaywrightInitializationService> _logger;
    private IPlaywright? _playwright;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private Task<IPlaywright>? _initTask;

    public PlaywrightInitializationService(ILogger<PlaywrightInitializationService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 中文：启动后台预热任务，不阻塞应用启动。
    /// English: Starts background preheating task without blocking application startup.
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        // 后台预热，不阻塞 MCP 服务器启动
        _ = Task.Run(async () =>
        {
            try
            {
                await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("[PlaywrightInit] 预热成功");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[PlaywrightInit] 预热失败，等待首次调用重试");
            }
        }, cancellationToken);

        return Task.CompletedTask;
    }

    /// <summary>
    /// 中文：停止服务（无特殊清理逻辑）。
    /// English: Stops the service (no special cleanup logic).
    /// </summary>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// 中文：确保 Playwright 引擎已初始化，使用 SemaphoreSlim 双检锁保证线程安全。
    /// English: Ensures Playwright engine is initialized, using SemaphoreSlim double-check lock for thread safety.
    /// </summary>
    /// <param name="cancellationToken">取消令牌 / Cancellation token</param>
    /// <returns>Playwright 引擎实例 / Playwright engine instance</returns>
    public async Task<IPlaywright> EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        // 第一次检查：快速路径，避免锁竞争
        if (_playwright != null)
        {
            return _playwright;
        }

        await _initLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // 第二次检查：防止重复初始化
            if (_playwright != null)
            {
                return _playwright;
            }

            // 如果初始化任务已在进行中，等待其完成
            if (_initTask != null)
            {
                return await _initTask.ConfigureAwait(false);
            }

            _logger.LogInformation("[PlaywrightInit] 开始初始化 Playwright 引擎");
            _initTask = Microsoft.Playwright.Playwright.CreateAsync();
            _playwright = await _initTask.ConfigureAwait(false);
            _logger.LogInformation("[PlaywrightInit] 初始化完成");

            return _playwright;
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <summary>
    /// 中文：异步释放 Playwright 资源和同步锁。
    /// English: Asynchronously disposes Playwright resources and synchronization lock.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_playwright != null)
        {
            _playwright.Dispose();
            _playwright = null;
        }

        _initLock.Dispose();
        await Task.CompletedTask;
    }
}
