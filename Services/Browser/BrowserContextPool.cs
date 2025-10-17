using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using Microsoft.Playwright;

namespace HushOps.Servers.XiaoHongShu.Services.Browser;

/// <summary>
/// 中文：浏览器上下文连接池接口，提供上下文的获取和归还操作。
/// English: Browser context connection pool interface for acquiring and returning contexts.
/// </summary>
public interface IBrowserContextPool
{
    /// <summary>
    /// 中文：从池中获取浏览器上下文（如池为空则创建新实例）。
    /// English: Get browser context from pool (creates new if pool is empty).
    /// </summary>
    IBrowserContext Get();

    /// <summary>
    /// 中文：将浏览器上下文归还到池中（经过健康检查后复用或销毁）。
    /// English: Return browser context to pool (reused or disposed after health check).
    /// </summary>
    void Return(IBrowserContext context);
}

/// <summary>
/// 中文：浏览器上下文连接池实现，管理上下文的生命周期和复用。
/// English: Browser context connection pool implementation for lifecycle management and reuse.
/// </summary>
public sealed class BrowserContextPool : IBrowserContextPool
{
    private readonly ObjectPool<IBrowserContext> _pool;
    private readonly ILogger<BrowserContextPool> _logger;

    /// <summary>
    /// 中文：初始化浏览器上下文连接池实例。
    /// English: Initialize browser context pool instance.
    /// </summary>
    /// <param name="pool">底层对象池 / Underlying object pool</param>
    /// <param name="logger">日志记录器 / Logger</param>
    public BrowserContextPool(
        ObjectPool<IBrowserContext> pool,
        ILogger<BrowserContextPool> logger)
    {
        _pool = pool ?? throw new ArgumentNullException(nameof(pool));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public IBrowserContext Get()
    {
        var context = _pool.Get();
        _logger.LogDebug("从连接池获取上下文 | Context retrieved from pool");
        return context;
    }

    /// <inheritdoc />
    public void Return(IBrowserContext context)
    {
        if (context == null)
        {
            _logger.LogWarning("尝试归还空上下文 | Attempted to return null context");
            return;
        }

        _pool.Return(context);
        _logger.LogDebug("上下文已归还连接池 | Context returned to pool");
    }
}
