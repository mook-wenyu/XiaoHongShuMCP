using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace HushOps.Core.Persistence;

/// <summary>Locator 选择器目录，从 JSON 文件中载入别名与页面状态的选择器映射。</summary>
public sealed class LocatorSelectorsCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private readonly IReadOnlyDictionary<string, LocatorSelectorEntry> _entries;

    /// <summary>初始化目录，可指定自定义路径；若未指定则在运行目录下自动查找。</summary>
    public LocatorSelectorsCatalog(string? filePath = null, ILogger<LocatorSelectorsCatalog>? logger = null)
    {
        var resolvedPath = ResolvePath(filePath);
        if (!File.Exists(resolvedPath))
        {
            throw new FileNotFoundException($"未找到定位器配置文件：{resolvedPath}");
        }

        using var stream = File.OpenRead(resolvedPath);
        var document = JsonSerializer.Deserialize<Dictionary<string, LocatorSelectorDocument>>(stream, JsonOptions)
                       ?? throw new InvalidDataException($"定位器配置为空：{resolvedPath}");

        var comparer = StringComparer.OrdinalIgnoreCase;
        var builder = new Dictionary<string, LocatorSelectorEntry>(comparer);

        foreach (var (aliasRaw, rawDocument) in document)
        {
            if (string.IsNullOrWhiteSpace(aliasRaw))
            {
                logger?.LogWarning("跳过空别名的定位器配置。");
                continue;
            }

            var alias = aliasRaw.Trim();
            var selectors = NormalizeList(rawDocument?.Selectors);
            var states = NormalizeStates(rawDocument?.States);

            builder[alias] = new LocatorSelectorEntry(selectors, states);
        }

        _entries = new ReadOnlyDictionary<string, LocatorSelectorEntry>(builder);
    }

    /// <summary>获取全部别名与配置项。</summary>
    public IReadOnlyDictionary<string, LocatorSelectorEntry> Entries => _entries;

    /// <summary>按别名返回配置，若不存在抛出异常。</summary>
    public LocatorSelectorEntry GetRequired(string alias)
    {
        if (string.IsNullOrWhiteSpace(alias))
        {
            throw new ArgumentException("别名不能为空", nameof(alias));
        }

        if (!_entries.TryGetValue(alias, out var entry))
        {
            throw new KeyNotFoundException($"定位器别名未配置：{alias}");
        }

        return entry;
    }

    private static List<string> NormalizeList(IEnumerable<string>? source)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        if (source is null)
        {
            return result;
        }

        foreach (var raw in source)
        {
            var trimmed = raw?.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                continue;
            }

            if (seen.Add(trimmed))
            {
                result.Add(trimmed);
            }
        }

        return result;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> NormalizeStates(Dictionary<string, List<string>>? states)
    {
        if (states is null || states.Count == 0)
        {
            return CreateEmptyStateMap();
        }

        var comparer = StringComparer.OrdinalIgnoreCase;
        var builder = new Dictionary<string, IReadOnlyList<string>>(comparer);

        foreach (var (stateRaw, selectors) in states)
        {
            if (string.IsNullOrWhiteSpace(stateRaw))
            {
                continue;
            }

            var normalized = NormalizeList(selectors);
            if (normalized.Count == 0)
            {
                continue;
            }

            builder[stateRaw.Trim()] = normalized;
        }

        if (builder.Count == 0)
        {
            return CreateEmptyStateMap();
        }

        return new ReadOnlyDictionary<string, IReadOnlyList<string>>(builder);
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> CreateEmptyStateMap()
    {
        return new ReadOnlyDictionary<string, IReadOnlyList<string>>(new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase));
    }

    private static string ResolvePath(string? filePath)
    {
        if (!string.IsNullOrWhiteSpace(filePath))
        {
            return Path.GetFullPath(filePath);
        }

        // 优先读取输出目录根部的 locator-selectors.json，其次尝试 Data 子目录
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "locator-selectors.json"),
            Path.Combine(AppContext.BaseDirectory, "Data", "locator-selectors.json"),
            Path.Combine(AppContext.BaseDirectory, "Persistence", "Data", "locator-selectors.json")
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        // 回退：仓库根目录惯例位置
        var fallback = Path.Combine(AppContext.BaseDirectory, "..", "..", "locator-selectors.json");
        return Path.GetFullPath(fallback);
    }

    private sealed class LocatorSelectorDocument
    {
        [JsonPropertyName("selectors")]
        public List<string>? Selectors { get; init; }

        [JsonPropertyName("states")]
        public Dictionary<string, List<string>>? States { get; init; }
    }
}

/// <summary>封装单个别名的选择器配置，包含通用序列与页面状态特定序列。</summary>
public sealed record LocatorSelectorEntry(IReadOnlyList<string> Selectors,
                                          IReadOnlyDictionary<string, IReadOnlyList<string>> States);
