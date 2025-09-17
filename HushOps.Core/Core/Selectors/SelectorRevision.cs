using System;
using System.Collections.Generic;

namespace HushOps.Core.Core.Selectors;

/// <summary>
/// 选择器版本发布参数。
/// </summary>
public sealed class SelectorRevision
{
    /// <summary>选择器别名（唯一键）。</summary>
    public string Alias { get; init; } = string.Empty;

    /// <summary>所属工作流。</summary>
    public string Workflow { get; init; } = string.Empty;

    /// <summary>版本号（如 20250917.1）。</summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>发布时间（UTC）。</summary>
    public DateTimeOffset PublishedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>维护人或发布人。</summary>
    public string Author { get; init; } = string.Empty;

    /// <summary>来源（自动、人工、回滚等）。</summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>调整前的选择器列表（仅用于审计）。</summary>
    public IReadOnlyList<string> Before { get; init; } = Array.Empty<string>();

    /// <summary>调整后的优先级列表。</summary>
    public IReadOnlyList<string> After { get; init; } = Array.Empty<string>();

    /// <summary>被降级或废弃的选择器集合。</summary>
    public IReadOnlyList<string> Demoted { get; init; } = Array.Empty<string>();

    /// <summary>附加标签。</summary>
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();

    /// <summary>备注说明。</summary>
    public string? Notes { get; init; }
        = null;

    /// <summary>历史成功率（0~1）。</summary>
    public double? SuccessRate { get; init; }
        = null;

    /// <summary>历史失败率（0~1）。</summary>
    public double? FailureRate { get; init; }
        = null;
}
