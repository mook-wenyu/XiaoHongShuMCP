using System;
using System.Collections.Generic;

namespace HushOps.Core.Core.Selectors;

/// <summary>
/// 选择器注册表快照。
/// </summary>
public sealed class SelectorRegistrySnapshot
{
    /// <summary>快照版本号（通常取最后一次发布的版本）。</summary>
    public string RegistryVersion { get; set; } = string.Empty;

    /// <summary>快照生成时间。</summary>
    public DateTimeOffset GeneratedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>条目集合（按别名索引）。</summary>
    public Dictionary<string, SelectorRegistryItem> Items { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
