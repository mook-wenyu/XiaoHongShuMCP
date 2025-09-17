using System;
using System.Threading;
using System.Threading.Tasks;
using HushOps.Core.Automation.Abstractions;
using HushOps.Core.Runtime.Playwright;

namespace XiaoHongShuMCP.Internal;

/// <summary>
/// 内部审计专用：HTML 采样服务（仅在显式开启审计与白名单放行时可用）。
/// - 采样标签：element.html.sample（已与默认白名单解耦）；
/// - 开关：XHS__InteractionPolicy__EnableHtmlSampleAudit=true；
/// - 白名单：XHS__InteractionPolicy__EvalAllowedPaths 包含 element.html.sample；
/// - 脱敏：简易替换敏感关键字；
/// - 截断：最大 KB（默认 16KB）。
/// </summary>
internal static class HtmlAuditSamplerService
{
    public static async Task<string?> TrySampleAsync(IAutoElement element, int maxKb = 16, CancellationToken ct = default)
    {
        if (!AuditEnabled()) return null;
        var handle = await PlaywrightAutoFactory.TryUnwrapAsync(element);
        if (handle is null) return null;
        try
        {
            // 内部审计：直接只读 Evaluate（严格受审计开关控制）。
            var html = await handle.EvaluateAsync<string>("el => (el.outerHTML||'').slice(0, 1024*1024)") ?? string.Empty;
            return SafeTruncate(Sanitize(html), maxKb);
        }
        catch { return null; }
    }

    private static bool AuditEnabled()
    {
        var v = Environment.GetEnvironmentVariable("XHS__InteractionPolicy__EnableHtmlSampleAudit");
        if (string.IsNullOrWhiteSpace(v)) return false;
        return v.Equals("true", StringComparison.OrdinalIgnoreCase) || v.Equals("1");
    }

    private static string Sanitize(string text)
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
