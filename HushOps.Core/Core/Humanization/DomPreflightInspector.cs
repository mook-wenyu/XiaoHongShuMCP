using HushOps.Core.Automation.Abstractions;
using HushOps.Core.Observability;

namespace HushOps.Core.Humanization;

/// <summary>
/// DOM 预检器（Core 实现）
/// - 点击前进行语义级就绪性检查：禁用/加载/可聚焦等；
/// - 仅依赖抽象元素与探针接口（不使用 JS Evaluate）；
/// - 返回低基数的结构化报告，outerHTML 采样会进行基础脱敏与KB截断。
/// </summary>
public sealed class DomPreflightInspector : IDomPreflightInspector
{
    private readonly IMetrics? _metrics;
    private readonly ICounter? _cReady;
    private readonly ICounter? _cBusy;
    private readonly ICounter? _cDisabled;

    public DomPreflightInspector(IMetrics? metrics = null)
    {
        _metrics = metrics;
        if (_metrics != null)
        {
            _cReady = _metrics.CreateCounter("preflight_ready_total", "DOM 预检就绪计数");
            _cBusy = _metrics.CreateCounter("preflight_busy_total", "DOM 预检忙碌/加载计数");
            _cDisabled = _metrics.CreateCounter("preflight_disabled_total", "DOM 预检禁用计数");
        }
    }

    public async Task<DomPreflightReport> InspectAsync(IAutoElement element, CancellationToken ct = default)
    {
        var report = new DomPreflightReport();
        try
        {
            // outerHTML 采样已与默认路径解耦，移至 Internal 审计服务；此处不再采集。

            // 角色/标签
            var role = (await element.GetAttributeAsync("role", ct))?.ToLowerInvariant();
            var tag = (await element.GetTagNameAsync(ct))?.ToLowerInvariant();
            report.Role = string.IsNullOrEmpty(role) ? tag : role;

            // 禁用：aria-disabled=true 或 disabled 属性存在；（无需 :disabled 伪类）
            var ariaDisabled = (await element.GetAttributeAsync("aria-disabled", ct))?.ToLowerInvariant() == "true";
            var hasDisabled = (await element.GetAttributeAsync("disabled", ct)) != null;
            report.IsDisabled = ariaDisabled || hasDisabled;

            // 忙碌：aria-busy/data-loading/子孙存在 spinner/loading 类或 [aria-busy=true]
            var ariaBusy = (await element.GetAttributeAsync("aria-busy", ct))?.ToLowerInvariant() == "true";
            var dataLoading = (await element.GetAttributeAsync("data-loading", ct))?.ToLowerInvariant() == "true";
            var spinnerDesc = await element.QuerySelectorAsync(".spinner,.loading,[aria-busy=\"true\"],.is-loading,.btn-loading", 150, ct) != null;
            report.IsBusy = ariaBusy || dataLoading || spinnerDesc;

            // 可聚焦：tabindex>=0 或标签属于常见可聚焦元素
            var tabindexRaw = await element.GetAttributeAsync("tabindex", ct);
            var focusableTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "a","button","input","textarea","select" };
            var tabindex = int.TryParse(tabindexRaw, out var ti) ? ti : -1;
            report.IsFocusable = tabindex >= 0 || (tag != null && focusableTags.Contains(tag));

            if (report.IsDisabled)
            {
                report.IsReady = false;
                report.Reason = "元素禁用（disabled/aria-disabled）";
                try { _cDisabled?.Add(1, LabelSet.From(("role", report.Role ?? ""))); } catch { }
            }
            else if (report.IsBusy)
            {
                report.IsReady = false;
                report.Reason = "元素处于加载/忙碌态（spinner/aria-busy/data-loading）";
                try { _cBusy?.Add(1, LabelSet.From(("role", report.Role ?? ""))); } catch { }
            }
            else
            {
                report.IsReady = true;
                report.Reason = "就绪";
                try { _cReady?.Add(1, LabelSet.From(("role", report.Role ?? ""))); } catch { }
            }
        }
        catch
        {
            report.IsReady = true; // 预检失败不阻断点击策略
            report.Reason = "预检异常（已放行）";
        }
        return report;
    }

    // 基础脱敏与安全截断（与应用层保持一致策略，但在Core内自包含实现）。
    private static string SanitizeForLogging(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        try
        {
            return text
                .Replace("set-cookie", "sc", StringComparison.OrdinalIgnoreCase)
                .Replace("authorization", "auth", StringComparison.OrdinalIgnoreCase)
                .Replace("cookie", "ck", StringComparison.OrdinalIgnoreCase);
        }
        catch { return text; }
    }

    private static string SafeTruncate(string text, int maxKb)
    {
        if (string.IsNullOrEmpty(text)) return text;
        var max = Math.Max(1, maxKb) * 1024;
        return text.Length <= max ? text : text[..max];
    }
}

/// <summary>
/// 仅用于内部只读评估的 DTO，低基数字段。
/// </summary>
public sealed class DomPreflightFlags
{
    public string? role { get; set; }
    public bool disabled { get; set; }
    public bool busy { get; set; }
    public bool focusable { get; set; }
}
