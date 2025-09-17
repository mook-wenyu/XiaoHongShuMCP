using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace XiaoHongShuMCP.Services;

/// <summary>
/// 并发治理器实现（按账户/操作种类限流）
/// - 写操作并发=1（默认），读操作并发=2（默认），通过 XhsSettings.Concurrency 配置覆盖。
/// - 返回 IOperationLease（IAsyncDisposable），保证租约作用域内持有并发名额，释放即归还。
/// - 线程安全：使用 ConcurrentDictionary + SemaphoreSlim。
/// </summary>
public sealed class ConcurrencyGovernor : IConcurrencyGovernor
{
    private readonly IAccountManager _accountManager;
    private readonly XhsSettings.ConcurrencySection _cfg;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _writeSemaphores = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _readSemaphores = new(StringComparer.Ordinal);

    public ConcurrencyGovernor(IAccountManager accountManager, IOptions<XhsSettings> options)
    {
        _accountManager = accountManager;
        _cfg = options.Value?.Concurrency ?? new XhsSettings.ConcurrencySection();
        if (_cfg.PerAccountWriteConcurrency <= 0) _cfg.PerAccountWriteConcurrency = 1;
        if (_cfg.PerAccountReadConcurrency <= 0) _cfg.PerAccountReadConcurrency = 2;
    }

    public async Task<IOperationLease> AcquireAsync(OperationKind kind, string resourceKey, CancellationToken ct = default)
    {
        var accountId = _accountManager.CurrentUser?.UserId;
        if (string.IsNullOrWhiteSpace(accountId)) accountId = "anonymous";
        var key = $"{accountId}:{kind}";

        SemaphoreSlim sem = kind == OperationKind.Write
            ? _writeSemaphores.GetOrAdd(key, _ => new SemaphoreSlim(_cfg.PerAccountWriteConcurrency, _cfg.PerAccountWriteConcurrency))
            : _readSemaphores.GetOrAdd(key, _ => new SemaphoreSlim(_cfg.PerAccountReadConcurrency, _cfg.PerAccountReadConcurrency));

        await sem.WaitAsync(ct);
        return new OperationLease(accountId, kind, resourceKey, sem);
    }

    /// <summary>
    /// 操作租约实现：释放时归还信号量。
    /// </summary>
    private sealed class OperationLease : IOperationLease
    {
        private readonly SemaphoreSlim _sem;
        private bool _disposed;
        public string AccountId { get; }
        public OperationKind Kind { get; }
        public string ResourceKey { get; }

        public OperationLease(string accountId, OperationKind kind, string resourceKey, SemaphoreSlim sem)
        {
            AccountId = accountId;
            Kind = kind;
            ResourceKey = resourceKey;
            _sem = sem;
        }

        public ValueTask DisposeAsync()
        {
            if (_disposed) return ValueTask.CompletedTask;
            _disposed = true;
            try { _sem.Release(); } catch { }
            return ValueTask.CompletedTask;
        }
    }
}
