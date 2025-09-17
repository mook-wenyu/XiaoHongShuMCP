using System;
using System.Collections.Generic;

namespace HushOps.Core.Core.Selectors;

/// <summary>
/// 选择器注册表中的有效条目。
/// </summary>
public sealed class SelectorRegistryItem
{
    /// <summary>选择器别名。</summary>
    public string Alias { get; set; } = string.Empty;

    /// <summary>工作流。</summary>
    public string Workflow { get; set; } = string.Empty;

    /// <summary>版本号。</summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>发布时间。</summary>
    public DateTimeOffset PublishedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>发布人。</summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>来源说明。</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>发布时的原始列表。</summary>
    public List<string> Before { get; set; } = new();

    /// <summary>最新优先级列表。</summary>
    public List<string> After { get; set; } = new();

    /// <summary>降级或废弃列表。</summary>
    public List<string> Demoted { get; set; } = new();

    /// <summary>标签。</summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>备注。</summary>
    public string? Notes { get; set; }
        = null;

    /// <summary>成功率。</summary>
    public double? SuccessRate { get; set; }
        = null;

    /// <summary>失败率。</summary>
    public double? FailureRate { get; set; }
        = null;
}
