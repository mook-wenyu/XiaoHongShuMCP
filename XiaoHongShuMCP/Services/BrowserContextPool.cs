using HushOps.Core.Automation.Abstractions;
using HushOps.Core.Runtime.Playwright;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace XiaoHongShuMCP.Services;

/// <summary>
/// 浏览器上下文/页面池（多页面池化版本）。
/// - 设计目标：在单持久化 Context 下复用多 Page，利用租约抽象向上层提供“独占页面”的作用域对象；
/// - 健康与自愈：定时清理已关闭页面，不足时按需补充；
/// - 容量控制：通过信号量限制池内并发租约数，避免过多标签页造成资源压力；
/// - 未来演进：若升级为“多 Context/多 UserDataDir”，仅需替换这里的 Page 获取/回收策略。
/// </summary>
public sealed class BrowserContextPool : IBrowserContextPool, IAsyncDisposable
{
    private readonly IBrowserManager _manager;
    private readonly ILogger<BrowserContextPool>? _logger;
    private readonly XhsSettings.ConcurrencySection _cfg;
    private readonly ConcurrentBag<IPage> _available = new();
    private readonly SemaphoreSlim _leaseLimiter;
    private readonly Timer _healthTimer;
    private bool _disposed;

    public BrowserContextPool(IBrowserManager manager, IOptions<XhsSettings> options, ILogger<BrowserContextPool>? logger = null)
    {
        _manager = manager;
        _logger = logger;
        _cfg = options.Value?.Concurrency ?? new XhsSettings.ConcurrencySection();
        var maxPages = Math.Max(1, _cfg.Pool.MaxPages);
        _leaseLimiter = new SemaphoreSlim(maxPages, maxPages);
        _healthTimer = new Timer(HealthSweep, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    /// <summary>
    /// 获取一个页面租约：
    /// 1) 先尝试从可用池取 Page；
    /// 2) 取不到则在容量允许时创建新 Page；
    /// 3) 达到容量则等待直至有租约归还。
    /// </summary>
    public async Task<IContextLease> AcquireAsync(string accountId, string? purpose = null, CancellationToken ct = default)
    {
        await _leaseLimiter.WaitAsync(ct);
        try
        {
            var ctx = await _manager.GetBrowserContextAsync();
            if (_available.TryTake(out var page))
            {
                if (!page.IsClosed)
                {
                    return new ContextLease(ctx, page, this);
                }
                // 取到已关闭页面：丢弃并创建新页
            }
            var newPage = await ctx.NewPageAsync();
            return new ContextLease(ctx, newPage, this);
        }
        catch
        {
            // 获取失败时归还配额
            _leaseLimiter.Release();
            throw;
        }
    }

    private void HealthSweep(object? state)
    {
        try
        {
            var kept = new List<IPage>();
            while (_available.TryTake(out var p))
            {
                if (!p.IsClosed) kept.Add(p);
            }
            foreach (var k in kept) _available.Add(k);
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "BrowserContextPool 健康清理异常（忽略）");
        }
    }

    private void Return(IPage page)
    {
        try
        {
            if (!page.IsClosed)
            {
                _available.Add(page);
            }
        }
        catch { }
        finally
        {
            try { _leaseLimiter.Release(); } catch { }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        try { _healthTimer.Dispose(); } catch { }
        // 不关闭页面/上下文（由 BrowserManager 统一管理）
        await Task.CompletedTask;
    }

    private sealed class ContextLease : IContextLease
    {
        public IBrowserContext Context { get; }
        public IAutoPage Page { get; }
        private readonly IPage _page;
        private readonly BrowserContextPool _pool;
        private bool _disposed;

        public ContextLease(IBrowserContext ctx, IPage page, BrowserContextPool pool)
        {
            Context = ctx;
            _page = page;
            Page = PlaywrightAutoFactory.Wrap(page);
            _pool = pool;
        }

        public ValueTask DisposeAsync()
        {
            if (_disposed) return ValueTask.CompletedTask;
            _disposed = true;
            _pool.Return(_page);
            return ValueTask.CompletedTask;
        }
    }
}

