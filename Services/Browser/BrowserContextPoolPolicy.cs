using System;
using HushOps.FingerprintBrowser.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using Microsoft.Playwright;

namespace HushOps.Servers.XiaoHongShu.Services.Browser;

/// <summary>
/// 中文：浏览器上下文对象池策略，定义上下文的创建、返回和销毁逻辑。
/// English: Browser context object pool policy that defines creation, return, and destruction logic.
/// </summary>
public sealed class BrowserContextPoolPolicy : IPooledObjectPolicy<IBrowserContext>
{
    private readonly IFingerprintBrowser _fingerprintBrowser;
    private readonly ILogger<BrowserContextPoolPolicy> _logger;
    private readonly string _profileKey;

    /// <summary>
    /// 中文：初始化浏览器上下文池策略实例。
    /// English: Initialize browser context pool policy instance.
    /// </summary>
    /// <param name="fingerprintBrowser">指纹浏览器实例 / Fingerprint browser instance</param>
    /// <param name="logger">日志记录器 / Logger</param>
    /// <param name="profileKey">配置文件键，默认 "user" / Profile key, default "user"</param>
    public BrowserContextPoolPolicy(
        IFingerprintBrowser fingerprintBrowser,
        ILogger<BrowserContextPoolPolicy> logger,
        string profileKey = "user")
    {
        _fingerprintBrowser = fingerprintBrowser ?? throw new ArgumentNullException(nameof(fingerprintBrowser));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _profileKey = profileKey;
    }

    /// <summary>
    /// 中文：创建新的浏览器上下文实例（池为空时调用）。
    /// English: Create new browser context instance (called when pool is empty).
    /// </summary>
    public IBrowserContext Create()
    {
        try
        {
            var (context, mode) = _fingerprintBrowser.CreateContextAsync(_profileKey).GetAwaiter().GetResult();
            _logger.LogInformation(
                "浏览器上下文已创建 | Browser context created: ProfileKey={ProfileKey}, Mode={Mode}",
                _profileKey, mode);
            return context;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建浏览器上下文失败 | Failed to create browser context: ProfileKey={ProfileKey}", _profileKey);
            throw;
        }
    }

    /// <summary>
    /// 中文：验证上下文是否可以返回池中（健康检查）。
    /// English: Validate if context can be returned to pool (health check).
    /// </summary>
    /// <param name="obj">待验证的上下文 / Context to validate</param>
    /// <returns>true=健康可返回，false=已损坏需销毁 / true=healthy, false=corrupted and will be disposed</returns>
    public bool Return(IBrowserContext obj)
    {
        // 中文：健康检查：验证上下文是否仍然可用
        // English: Health check: Validate if context is still usable
        try
        {
            // 中文：简单检查：确保 Pages 属性可访问（验证上下文未被关闭）
            // English: Simple check: Ensure Pages property is accessible (context not closed)
            _ = obj.Pages;
            return true; // 中文：上下文健康，可以返回池中 / English: Context healthy, can return to pool
        }
        catch
        {
            _logger.LogWarning("浏览器上下文已损坏，将被销毁 | Browser context corrupted, will be destroyed");
            return false; // 中文：上下文已损坏，ObjectPool 会自动调用 Dispose / English: Context corrupted, ObjectPool will auto-dispose
        }
    }
}
