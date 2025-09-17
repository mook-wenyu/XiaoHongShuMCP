namespace HushOps.Core.Core.Selectors;

/// <summary>
/// 选择器注册表的存储选项。
/// </summary>
public sealed class SelectorRegistryOptions
{
    /// <summary>当前快照存储路径。</summary>
    public string RegistryPath { get; set; } = "selectors/registry.json";

    /// <summary>历史版本存储目录。</summary>
    public string HistoryDirectory { get; set; } = "selectors/history";

    /// <summary>清单文件路径，记录最新版本摘要。</summary>
    public string ManifestPath { get; set; } = "selectors/manifest.json";
}
