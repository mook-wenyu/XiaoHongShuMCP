using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using XiaoHongShuMCP.Services;

/// <summary>导出 DomElementManager 内置的选择器映射，生成 JSON 配置文件以便 Core 包加载。</summary>
internal static class Program
{
    /// <summary>程序入口：支持自定义输出路径，默认写入仓库内 HushOps.Core/Persistence/Data。</summary>
    private static async Task Main(string[] args)
    {
        // 默认输出目录相对编译产物回溯到仓库根目录
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var outputPath = args.Length > 0 ? args[0] : Path.Combine(repoRoot, "HushOps.Core", "Persistence", "Data", "locator-selectors.json");

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        // 直接实例化 DomElementManager 以复用现有别名映射
        var manager = new DomElementManager();

        var selectorsField = typeof(DomElementManager).GetField("_selectors", BindingFlags.Instance | BindingFlags.NonPublic)
                            ?? throw new InvalidOperationException("无法定位 _selectors 字段");
        var stateField = typeof(DomElementManager).GetField("_pageStateSelectors", BindingFlags.Instance | BindingFlags.NonPublic)
                        ?? throw new InvalidOperationException("无法定位 _pageStateSelectors 字段");

        var baseSelectors = (Dictionary<string, List<string>>)selectorsField.GetValue(manager)!;
        var stateSelectors = (Dictionary<PageState, Dictionary<string, List<string>>>)stateField.GetValue(manager)!;

        var aliasSet = new HashSet<string>(baseSelectors.Keys, StringComparer.OrdinalIgnoreCase);
        foreach (var kv in stateSelectors.Values)
        {
            foreach (var alias in kv.Keys)
            {
                aliasSet.Add(alias);
            }
        }

        var aliases = aliasSet.ToList();
        aliases.Sort(StringComparer.OrdinalIgnoreCase);

        var documents = new Dictionary<string, LocatorDocument>(StringComparer.OrdinalIgnoreCase);

        foreach (var alias in aliases)
        {
            // 复制基础选择器列表，避免后续修改影响原始集合
            var general = baseSelectors.TryGetValue(alias, out var list)
                ? new List<string>(list)
                : new List<string>();

            Dictionary<string, List<string>>? states = null;

            foreach (var (pageState, selectors) in stateSelectors)
            {
                if (!selectors.TryGetValue(alias, out var stateList) || stateList.Count == 0)
                {
                    continue;
                }

                states ??= new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                states[pageState.ToString()] = new List<string>(stateList);
            }

            documents[alias] = new LocatorDocument
            {
                Selectors = general,
                States = states
            };
        }

        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };

        await using var stream = File.Create(outputPath);
        await JsonSerializer.SerializeAsync(stream, documents, jsonOptions);

        Console.WriteLine($"已生成选择器配置：{outputPath}");
    }

    private sealed class LocatorDocument
    {
        [JsonPropertyName("selectors")]
        public required List<string> Selectors { get; init; }

        [JsonPropertyName("states")]
        public Dictionary<string, List<string>>? States { get; init; }
    }
}
