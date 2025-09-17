using System.Threading;
using System.Threading.Tasks;
using HushOps.Core.Automation.Abstractions;

namespace HushOps.Core.Humanization;

/// <summary>
/// 可点性检测器接口（Core 抽象）。
/// - 输入：抽象元素；
/// - 输出：可点性报告（可见性/尺寸/视口/遮挡/pointer-events 等启发式）。
/// - 仅做轻量启发式评估，不阻断策略尝试。
/// </summary>
public interface IClickabilityDetector
{
    Task<ClickabilityReport> AssessAsync(IAutoElement element, CancellationToken ct = default);
}

/// <summary>
/// 可点性检测报告（低基数字段，仅结构化审计）。
/// </summary>
public sealed class ClickabilityReport
{
    public bool IsClickable { get; set; }
    public bool IsVisible { get; set; }
    public bool HasBox { get; set; }
    public bool IsInViewport { get; set; }
    public bool PointerEventsEnabled { get; set; }
    public bool PossiblyOccluded { get; set; }
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// 默认可点性检测器（基于适配器聚合探针）。
/// - 说明：实现无业务注入；探针通过适配器只读 API 提供。
/// </summary>
public sealed class ClickabilityDetector : IClickabilityDetector
{
    public async Task<ClickabilityReport> AssessAsync(IAutoElement element, CancellationToken ct = default)
    {
        var report = new ClickabilityReport();
        try
        {
            var p = await element.GetClickabilityProbeAsync(ct);
            report.HasBox = p.HasBox;
            report.IsInViewport = p.InViewport;
            report.IsVisible = p.VisibleByStyle;
            report.PointerEventsEnabled = p.PointerEventsEnabled;
            report.PossiblyOccluded = p.CenterOccluded;
            report.IsClickable = p.Clickable;

            if (!report.HasBox) report.Reason = "元素无有效尺寸";
            else if (!report.IsInViewport) report.Reason = "元素不在视口内";
            else if (!report.IsVisible) report.Reason = "元素不可见或透明";
            else if (!report.PointerEventsEnabled) report.Reason = "pointer-events 禁用";
            else if (report.PossiblyOccluded) report.Reason = "中心点可能被遮挡";
            else report.Reason = "可点击";
        }
        catch
        {
            report.IsClickable = false;
            report.Reason = "评估异常";
        }
        return report;
    }
}

