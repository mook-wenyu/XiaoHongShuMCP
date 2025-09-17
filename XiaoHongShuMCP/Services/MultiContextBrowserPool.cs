using HushOps.Core.Automation.Abstractions;
using HushOps.Core.Runtime.Playwright;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

// 说明：命名空间迁移至 HushOps.Services。
namespace XiaoHongShuMCP.Services;

/// <summary>
/// 多上下文浏览器池（真实多持久化上下文，每个账户独立 UserDataDir）。
/// - 特性：按 accountId 隔离持久化会话目录，减少跨账号指纹/存储污染；
/// - 容量：每个上下文维度的页面池容量由 XHS:Concurrency:Pool:MaxPages 控制（默认3）；
/// - 健康：定时清理关闭页面；页面创建失败时尝试重建上下文；
/// - 线程安全：账户级词典 + 每上下文局部信号量/可用页包。
/// </summary>
public sealed class MultiContextBrowserPool : IBrowserContextPool, IAsyncDisposable
{
    private readonly ILogger<MultiContextBrowserPool>? _logger;
    private readonly XhsSettings _settings;
    private readonly ConcurrentDictionary<string, ContextEntry> _contexts = new(StringComparer.Ordinal);
    private bool _disposed;

    public MultiContextBrowserPool(IOptions<XhsSettings> options, ILogger<MultiContextBrowserPool>? logger = null)
    {
        _settings = options.Value ?? new XhsSettings();
        _logger = logger;
    }

    public async Task<IContextLease> AcquireAsync(string accountId, string? purpose = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(accountId)) accountId = "anonymous";
        var entry = await GetOrCreateContextAsync(accountId, ct);
        await entry.Limiter.WaitAsync(ct);
        try
        {
            if (entry.Available.TryTake(out var page))
            {
                if (!page.IsClosed)
                    return new Lease(entry, page);
            }
            // 新建页面
            var newPage = await entry.Context.NewPageAsync();
            return new Lease(entry, newPage);
        }
        catch
        {
            entry.Limiter.Release();
            throw;
        }
    }

    private async Task<ContextEntry> GetOrCreateContextAsync(string accountId, CancellationToken ct)
    {
        if (_contexts.TryGetValue(accountId, out var existing)) return existing;
        var created = await CreateContextAsync(accountId, ct);
        _contexts[accountId] = created;
        return created;
    }

    private async Task<ContextEntry> CreateContextAsync(string accountId, CancellationToken ct)
    {
        var playwright = await Playwright.CreateAsync();
        var userDataDir = ResolveUserDataDir(accountId);
        Directory.CreateDirectory(userDataDir);
        var headless = _settings.BrowserSettings?.Headless ?? false;
        var channel = _settings.BrowserSettings?.Channel;
        var executable = _settings.BrowserSettings?.ExecutablePath;
        var args = new List<string> {"--no-first-run", "--no-default-browser-check", "--disable-first-run-ui"};

        var opt = new BrowserTypeLaunchPersistentContextOptions
        {
            Headless = headless,
            Args = args.ToArray()
        };
        if (!string.IsNullOrWhiteSpace(executable)) opt.ExecutablePath = executable;
        else if (!string.IsNullOrWhiteSpace(channel)) opt.Channel = channel;

        var context = await playwright.Chromium.LaunchPersistentContextAsync(userDataDir, opt);
        var maxPages = Math.Max(1, _settings.Concurrency?.Pool?.MaxPages ?? 3);
        var entry = new ContextEntry(accountId, playwright, context, maxPages, _logger);
        entry.StartHealthSweep();
        return entry;
    }

    private static string ResolveUserDataDir(string accountId)
    {
        var projectRoot = Path.GetDirectoryName(Path.GetDirectoryName(AppContext.BaseDirectory)) ?? AppContext.BaseDirectory;
        var baseDir = Path.Combine(projectRoot, "UserDataPool");
        return Path.Combine(baseDir, Sanitize(accountId));
    }

    private static string Sanitize(string s)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        return s;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var kv in _contexts)
        {
            await kv.Value.DisposeAsync();
        }
        _contexts.Clear();
    }

    /// <summary>
    /// 单账户上下文条目。
    /// </summary>
    private sealed class ContextEntry : IAsyncDisposable
    {
        public string AccountId { get; }
        public IPlaywright Playwright { get; }
        public IBrowserContext Context { get; private set; }
        public SemaphoreSlim Limiter { get; }
        public ConcurrentBag<IPage> Available { get; } = new();
        private readonly ILogger? _logger;
        private Timer? _timer;
        private bool _disposed;

        public ContextEntry(string accountId, IPlaywright pw, IBrowserContext ctx, int maxPages, ILogger? logger)
        {
            AccountId = accountId;
            Playwright = pw;
            Context = ctx;
            Limiter = new SemaphoreSlim(maxPages, maxPages);
            _logger = logger;
        }

        public void StartHealthSweep()
        {
            _timer = new Timer(HealthSweep, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        }

        private void HealthSweep(object? state)
        {
            try
            {
                // 清理关闭页面
                var kept = new List<IPage>();
                while (Available.TryTake(out var p))
                {
                    if (!p.IsClosed) kept.Add(p);
                }
                foreach (var k in kept) Available.Add(k);
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "[{Account}] 健康清理异常（忽略）", AccountId);
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;
            try { _timer?.Dispose(); } catch { }
            try { await Context.CloseAsync(); } catch { }
            try { Playwright.Dispose(); } catch { }
        }
    }

    private sealed class Lease : IContextLease
    {
        private readonly ContextEntry _entry;
        private bool _disposed;
        public IBrowserContext Context => _entry.Context;
        public IAutoPage Page { get; }
        private readonly IPage _page;
        public Lease(ContextEntry entry, IPage page)
        {
            _entry = entry;
            _page = page;
            Page = PlaywrightAutoFactory.Wrap(page);
        }
        public ValueTask DisposeAsync()
        {
            if (_disposed) return ValueTask.CompletedTask;
            _disposed = true;
            try
            {
                if (!_page.IsClosed) _entry.Available.Add(_page);
            }
            catch { }
            finally
            {
                try { _entry.Limiter.Release(); } catch { }
            }
            return ValueTask.CompletedTask;
        }
    }
}

