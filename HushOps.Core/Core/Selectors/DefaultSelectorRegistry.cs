using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Immutable;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using HushOps.Core.Persistence;

namespace HushOps.Core.Core.Selectors;

/// <summary>
/// 默认选择器注册表实现，提供版本化、审计与快速回滚能力。
/// </summary>
public sealed class DefaultSelectorRegistry : ISelectorRegistry
{
    private static readonly Regex VersionPattern = new("^\\d{8}\\.\\d+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly IJsonLocalStore store;
    private readonly SelectorRegistryOptions options;
    private readonly ILogger<DefaultSelectorRegistry>? logger;
    private readonly SemaphoreSlim globalLock = new(1, 1);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> aliasLocks = new(StringComparer.OrdinalIgnoreCase);

    public DefaultSelectorRegistry(
        IJsonLocalStore store,
        IOptions<SelectorRegistryOptions>? options,
        ILogger<DefaultSelectorRegistry>? logger = null)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        this.options = options?.Value ?? new SelectorRegistryOptions();
        this.logger = logger;
    }

    /// <inheritdoc />
    public async Task<SelectorRegistryItem> PublishAsync(SelectorRevision revision, CancellationToken ct = default)
    {
        if (revision is null)
        {
            throw new ArgumentNullException(nameof(revision));
        }

        ValidateRevision(revision);
        var safeAlias = Sanitize(revision.Alias);
        var gate = aliasLocks.GetOrAdd(safeAlias, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await globalLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var snapshot = await LoadSnapshotAsync(ct).ConfigureAwait(false);
                var item = ToRegistryItem(revision);
                snapshot.RegistryVersion = revision.Version;
                snapshot.GeneratedAtUtc = DateTimeOffset.UtcNow;
                snapshot.Items[revision.Alias] = item;

                await SaveSnapshotAsync(snapshot, ct).ConfigureAwait(false);
                await SaveHistoryAsync(safeAlias, item, ct).ConfigureAwait(false);
                await UpdateManifestAsync(item, ct).ConfigureAwait(false);

                logger?.LogInformation("[SelectorRegistry] 发布 {Alias} 版本 {Version}", item.Alias, item.Version);
                return item;
            }
            finally
            {
                globalLock.Release();
            }
        }
        finally
        {
            gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<SelectorRegistryItem> RollbackAsync(string alias, string version, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(alias))
        {
            throw new ArgumentException("别名不能为空", nameof(alias));
        }

        if (string.IsNullOrWhiteSpace(version))
        {
            throw new ArgumentException("版本号不能为空", nameof(version));
        }

        var safeAlias = Sanitize(alias);
        var gate = aliasLocks.GetOrAdd(safeAlias, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await globalLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var historyPath = GetHistoryPath(safeAlias, version);
                var item = await store.LoadAsync<SelectorRegistryItem>(historyPath, ct).ConfigureAwait(false)
                           ?? throw new InvalidOperationException($"未找到历史版本: {alias}@{version}");

                var snapshot = await LoadSnapshotAsync(ct).ConfigureAwait(false);
                snapshot.RegistryVersion = item.Version;
                snapshot.GeneratedAtUtc = DateTimeOffset.UtcNow;
                snapshot.Items[alias] = item;

                await SaveSnapshotAsync(snapshot, ct).ConfigureAwait(false);
                await UpdateManifestAsync(item, ct).ConfigureAwait(false);

                logger?.LogWarning("[SelectorRegistry] 已回滚 {Alias} 至 {Version}", alias, version);
                return item;
            }
            finally
            {
                globalLock.Release();
            }
        }
        finally
        {
            gate.Release();
        }
    }

    /// <inheritdoc />
    public Task<SelectorRegistrySnapshot> GetSnapshotAsync(CancellationToken ct = default)
        => LoadSnapshotAsync(ct);

    /// <inheritdoc />
    public async Task<SelectorRegistryItem?> GetActiveAsync(string alias, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(alias))
        {
            throw new ArgumentException("别名不能为空", nameof(alias));
        }

        var snapshot = await LoadSnapshotAsync(ct).ConfigureAwait(false);
        return snapshot.Items.TryGetValue(alias, out var item) ? item : null;
    }

    /// <inheritdoc />
    public async Task<HushOps.Core.Selectors.WeakSelectorPlan> BuildPlanAsync(string workflow, CancellationToken ct = default)
    {
        var snapshot = await LoadSnapshotAsync(ct).ConfigureAwait(false);
        var comparer = StringComparer.OrdinalIgnoreCase;
        var items = snapshot.Items.Values
            .Where(item => string.IsNullOrWhiteSpace(workflow) || comparer.Equals(item.Workflow, workflow))
            .Where(item => item.After.Count > 0)
            .Select(item => new HushOps.Core.Selectors.WeakSelectorPlanItem(
                item.Alias,
                item.Before.ToImmutableArray(),
                item.After.ToImmutableArray(),
                item.Demoted.ToImmutableArray()))
            .ToImmutableArray();

        return new HushOps.Core.Selectors.WeakSelectorPlan(items);
    }

    private async Task<SelectorRegistrySnapshot> LoadSnapshotAsync(CancellationToken ct)
    {
        var snapshot = await store.LoadAsync<SelectorRegistrySnapshot>(options.RegistryPath, ct).ConfigureAwait(false);
        if (snapshot is null)
        {
            snapshot = new SelectorRegistrySnapshot
            {
                RegistryVersion = "0",
                GeneratedAtUtc = DateTimeOffset.UtcNow,
                Items = new Dictionary<string, SelectorRegistryItem>(StringComparer.OrdinalIgnoreCase)
            };
        }
        else if (snapshot.Items is null)
        {
            snapshot.Items = new Dictionary<string, SelectorRegistryItem>(StringComparer.OrdinalIgnoreCase);
        }

        return snapshot;
    }

    private Task SaveSnapshotAsync(SelectorRegistrySnapshot snapshot, CancellationToken ct)
        => store.SaveAsync(options.RegistryPath, snapshot, ct);

    private Task SaveHistoryAsync(string safeAlias, SelectorRegistryItem item, CancellationToken ct)
    {
        var path = GetHistoryPath(safeAlias, item.Version);
        return store.SaveAsync(path, item, ct);
    }

    private async Task UpdateManifestAsync(SelectorRegistryItem item, CancellationToken ct)
    {
        var manifestPath = options.ManifestPath;
        var manifest = await store.LoadAsync<SelectorRegistryManifest>(manifestPath, ct).ConfigureAwait(false)
                       ?? new SelectorRegistryManifest();
        manifest.LatestVersion = item.Version;
        manifest.UpdatedAtUtc = DateTimeOffset.UtcNow;
        manifest.Entries ??= new List<SelectorRegistryManifestEntry>();
        manifest.Entries.RemoveAll(e => string.Equals(e.Alias, item.Alias, StringComparison.OrdinalIgnoreCase));
        manifest.Entries.Add(new SelectorRegistryManifestEntry
        {
            Alias = item.Alias,
            Workflow = item.Workflow,
            Version = item.Version,
            Author = item.Author,
            PublishedAtUtc = item.PublishedAtUtc,
            FileName = $"{Sanitize(item.Alias)}/{item.Version}.json"
        });
        manifest.Entries = manifest.Entries
            .OrderBy(e => e.Alias, StringComparer.OrdinalIgnoreCase)
            .ToList();
        await store.SaveAsync(manifestPath, manifest, ct).ConfigureAwait(false);
    }

    private string GetHistoryPath(string safeAlias, string version)
    {
        var historyDir = options.HistoryDirectory.TrimEnd('/');
        return $"{historyDir}/{safeAlias}/{version}.json";
    }

    private static string Sanitize(string input)
    {
        var builder = new StringBuilder(input.Length);
        foreach (var ch in input)
        {
            builder.Append(char.IsLetterOrDigit(ch) ? ch : '_');
        }
        return builder.ToString();
    }

    private static SelectorRegistryItem ToRegistryItem(SelectorRevision revision)
    {
        var item = new SelectorRegistryItem
        {
            Alias = revision.Alias,
            Workflow = revision.Workflow,
            Version = revision.Version,
            PublishedAtUtc = revision.PublishedAtUtc,
            Author = revision.Author,
            Source = revision.Source,
            Before = revision.Before.ToList(),
            After = revision.After.ToList(),
            Demoted = revision.Demoted.ToList(),
            Tags = revision.Tags.ToList(),
            Notes = revision.Notes,
            SuccessRate = revision.SuccessRate,
            FailureRate = revision.FailureRate
        };

        if (item.Before.Count == 0)
        {
            item.Before = item.After.ToList();
        }

        return item;
    }

    private static void ValidateRevision(SelectorRevision revision)
    {
        if (string.IsNullOrWhiteSpace(revision.Alias))
        {
            throw new ArgumentException("别名不能为空", nameof(revision.Alias));
        }

        if (string.IsNullOrWhiteSpace(revision.Workflow))
        {
            throw new ArgumentException("工作流不能为空", nameof(revision.Workflow));
        }

        if (!VersionPattern.IsMatch(revision.Version))
        {
            throw new ArgumentException("版本号需满足 YYYYMMDD.N 格式", nameof(revision.Version));
        }

        if (revision.After.Count == 0)
        {
            throw new ArgumentException("After 列表不能为空", nameof(revision.After));
        }
    }

    private sealed class SelectorRegistryManifest
    {
        public string LatestVersion { get; set; } = string.Empty;
        public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
        public List<SelectorRegistryManifestEntry>? Entries { get; set; }
            = new();
    }

    private sealed class SelectorRegistryManifestEntry
    {
        public string Alias { get; set; } = string.Empty;
        public string Workflow { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public DateTimeOffset PublishedAtUtc { get; set; } = DateTimeOffset.UtcNow;
        public string FileName { get; set; } = string.Empty;
    }
}
