using System.Threading;
using System.Threading.Tasks;
using HushOps.Core.Automation.Abstractions;

namespace HushOps.Core.Humanization;

/// <summary>
/// DOM 预检器接口（Core 抽象）。
/// - 点击前进行“语义就绪”验证（禁用/加载/角色/可聚焦等）；
/// - 任何异常不抛出，报告中体现，避免影响点击主流程。
/// </summary>
public interface IDomPreflightInspector
{
    Task<DomPreflightReport> InspectAsync(IAutoElement element, CancellationToken ct = default);
}

/// <summary>
/// DOM 预检报告（低基数字段，便于审计与调优）。
/// </summary>
public sealed class DomPreflightReport
{
    public bool IsReady { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Role { get; set; }
    public bool IsDisabled { get; set; }
    public bool IsBusy { get; set; }
    public bool IsFocusable { get; set; }
    public string? OuterHtmlSample { get; set; }
}

