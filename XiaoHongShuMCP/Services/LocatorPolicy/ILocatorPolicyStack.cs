using HushOps.Core.Automation.Abstractions;
using HushOps.Core.Observability;

// 说明：命名空间迁移至 HushOps.Services。
namespace XiaoHongShuMCP.Services;

/// <summary>
/// 定位策略栈（统一“语义/组合/别名/文本/回退”）
/// - 目标：在不使用 JS 注入的前提下，以更贴近用户语义的方式定位目标控件。
/// - 顺序：A11y/语义 → 组合/相对（has/has-text）→ 别名候选 → 文本引擎 → 放弃。
/// </summary>
public interface ILocatorPolicyStack
{
    Task<LocatorAcquireResult> AcquireAsync(IAutoPage page, LocatorHint hint, CancellationToken ct = default);
}

/// <summary>
/// 定位提示（意图）
/// </summary>
public sealed class LocatorHint
{
    /// <summary>别名列表（DomElementManager 中的键）。</summary>
    public IReadOnlyList<string> Aliases { get; init; } = Array.Empty<string>();
    /// <summary>可访问角色（示例：button/link/textbox）。</summary>
    public string? Role { get; init; }
    /// <summary>可访问名称/可见文本/占位符关键词。</summary>
    public string? NameOrText { get; init; }
    /// <summary>容器别名（用于缩小范围）。</summary>
    public IReadOnlyList<string> ContainerAliases { get; init; } = Array.Empty<string>();
    /// <summary>是否仅返回可见元素（默认 true）。</summary>
    public bool VisibleOnly { get; init; } = true;
    /// <summary>每步尝试的超时（毫秒，默认 3000）。</summary>
    public int StepTimeoutMs { get; init; } = 3000;
}

/// <summary>
/// 定位结果（包含审计信息）。
/// </summary>
public sealed class LocatorAcquireResult
{
    public IAutoElement? Element { get; init; }
    public string Strategy { get; init; } = string.Empty; // 例如 a11y-role, css-has-text, alias, text-engine
}
