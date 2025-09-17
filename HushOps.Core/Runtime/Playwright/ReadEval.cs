using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace HushOps.Core.Runtime.Playwright;

/// <summary>
/// 公开的只读 Evaluate 桥接器：封装门控与计量（最终委托给内部 PlaywrightAdapterTelemetry）。
/// 注意：仅用于只读路径，不得修改页面状态；需配合白名单与开关使用。
/// </summary>
public static class ReadEval
{
    public static Task<T?> EvalAsync<T>(IElementHandle handle, string script, string label, CancellationToken ct = default)
        => PlaywrightAdapterTelemetry.EvalAsync<T>(handle, script, label, ct);

    public static Task<T?> EvalAsync<T>(IPage page, string script, string label, CancellationToken ct = default)
        => PlaywrightAdapterTelemetry.EvalAsync<T>(page, script, label, ct);
}

