using Microsoft.Extensions.Options;
using HushOps.Core.Automation.Abstractions;
using HushOps.Core.Observability;

namespace XiaoHongShuMCP.Services.Utilities;

/// <summary>
/// JS 只读执行门控器：集中控制、计量与审计。
/// - 作用：为仍需读取页面状态的只读 JS 表达式提供唯一受控入口，便于削减与审计。
/// - 行为：当 XHS:InteractionPolicy:EnableJsReadEval=false 时直接拒绝；否则执行表达式并累计 ui_injection_total{type=eval,path=...}。
/// - 目标：在保证安全与可观测的前提下逐步压降只读 Evaluate 的依赖。
/// </summary>
public interface IReadOnlyJsExecutor
{
    /// <summary>在页面级执行只读 JS 表达式。</summary>
    Task<T> ExecuteOnPageAsync<T>(IAutoPage page, string expression, string pathLabel, CancellationToken ct = default);

    /// <summary>在元素级执行只读 JS 表达式。</summary>
    Task<T> ExecuteOnElementAsync<T>(IAutoElement element, string expression, string pathLabel, CancellationToken ct = default);
}

internal sealed class ReadOnlyJsExecutor : IReadOnlyJsExecutor
{
    private readonly bool _enabled;
    private readonly ICounter? _counter;

    public ReadOnlyJsExecutor(IOptions<XhsSettings> options, IMetrics? metrics)
    {
        _enabled = options.Value?.InteractionPolicy?.EnableJsReadEval ?? true;
        _counter = metrics?.CreateCounter("ui_injection_total", "UI 注入/评估使用计数（应为0，>0告警）");
    }

    public async Task<T> ExecuteOnPageAsync<T>(IAutoPage page, string expression, string pathLabel, CancellationToken ct = default)
    {
        if (!_enabled) throw new NotSupportedException("已禁用只读 Evaluate（XHS:InteractionPolicy:EnableJsReadEval=false）");
        _counter?.Add(1, LabelSet.From(("type", "eval"), ("path", pathLabel)));
        return (await page.EvaluateAsync<T>(expression, ct))!;
    }

    public async Task<T> ExecuteOnElementAsync<T>(IAutoElement element, string expression, string pathLabel, CancellationToken ct = default)
    {
        if (!_enabled) throw new NotSupportedException("已禁用只读 Evaluate（XHS:InteractionPolicy:EnableJsReadEval=false）");
        _counter?.Add(1, LabelSet.From(("type", "eval"), ("path", pathLabel)));
        return (await element.EvaluateAsync<T>(expression, ct))!;
    }
}
