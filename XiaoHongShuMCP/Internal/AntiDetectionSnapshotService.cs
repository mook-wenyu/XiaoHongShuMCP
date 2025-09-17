using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using HushOps.Core.Persistence;
using HushOps.Core.Runtime.Playwright.AntiDetection;
using HushOps.Core.AntiDetection;
using XiaoHongShuMCP.Services;

namespace XiaoHongShuMCP.Internal;

internal static class AntiDetectionSnapshotService
{
    public sealed record AntiDetectionSnapshotMcp(
        global::HushOps.Core.AntiDetection.AntiDetectionSnapshot Snapshot,
        bool Success,
        [property: JsonPropertyName("message")] string Message,
        string? AuditPath = null,
        List<string>? Violations = null,
        bool DegradeRecommended = false,
        [property: JsonPropertyName("errorCode")] string? ErrorCode = null,
        [property: JsonPropertyName("retriable")] bool? Retriable = null,
        [property: JsonPropertyName("requestId")] string? RequestId = null
    );

    public static async Task<AntiDetectionSnapshotMcp> GetAntiDetectionSnapshot(
        bool writeAudit = true,
        string? auditDirectory = null,
        string? whitelistPath = null,
        IServiceProvider serviceProvider = null!,
        string? requestId = null)
    {
        try
        {
            var browser = serviceProvider.GetRequiredService<IBrowserManager>();
            var pipeline = serviceProvider.GetRequiredService<IPlaywrightAntiDetectionPipeline>();
            var settings = serviceProvider.GetService<IOptions<HushOps.Core.Config.XhsSettings>>()?.Value;

            var page = await browser.GetPageAsync();
            var snapshot = await pipeline.CollectSnapshotAsync(page);

            string? auditPath = null;
            if (writeAudit)
            {
                var dir = !string.IsNullOrWhiteSpace(auditDirectory)
                    ? auditDirectory!
                    : (settings?.AntiDetection?.AuditDirectory ?? ".audit");
                var relativePath = Path.Combine(dir, $"antidetect-snapshot-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}.json");

                var store = serviceProvider.GetService<IJsonLocalStore>();
                if (store != null)
                {
                    try
                    {
                        var entry = await store.SaveAsync(relativePath, snapshot).ConfigureAwait(false);
                        auditPath = entry.FullPath;
                    }
                    catch
                    {
                        auditPath = await WriteAuditFallbackAsync(relativePath, snapshot).ConfigureAwait(false);
                    }
                }
                else
                {
                    auditPath = await WriteAuditFallbackAsync(relativePath, snapshot).ConfigureAwait(false);
                }
            }

            var violations = new List<string>();
            var ok = true;
            var degrade = false;
            if (!string.IsNullOrWhiteSpace(whitelistPath))
            {
                var wlFile = whitelistPath!;
                if (!File.Exists(wlFile))
                    return new AntiDetectionSnapshotMcp(snapshot, false, $"白名单文件不存在: {wlFile}", auditPath, new List<string> { "WHITELIST_FILE_NOT_FOUND" }, false, "WHITELIST_FILE_NOT_FOUND", false, requestId);

                var wlJson = await File.ReadAllTextAsync(wlFile);
                var whitelist = JsonSerializer.Deserialize<AntiDetectionWhitelist>(wlJson) ?? new AntiDetectionWhitelist();
                // 仅使用扩展校验（包含 UA/平台/时区/WebGL/存储/权限等键），避免误报
                foreach (var v in ValidateExtended(snapshot, whitelist)) violations.Add(v);
                var threshold = whitelist.MaxViolations.GetValueOrDefault(0);
                degrade = violations.Count > 0 && violations.Count <= threshold;
                ok = violations.Count <= threshold;
                // 为避免误报，零违反即视为通过
                if (violations.Count == 0) { ok = true; }
                if (ok) violations = null;
            }

            var msg = ok ? "OK: 抗检测快照采集成功" : (degrade ? "WARN: 存在轻微偏差，建议降级" : "FAIL: 抗检测快照存在白名单违反");
            return new AntiDetectionSnapshotMcp(snapshot, ok, msg, auditPath, violations, degrade, ok ? null : (degrade ? "ANTIDETECT_DEGRADE_RECOMMENDED" : "ANTIDETECT_WHITELIST_VIOLATED"), !ok && degrade, requestId);
        }
        catch (Exception ex)
        {
            var code = ex is TimeoutException or OperationCanceledException ? "ERR_TIMEOUT" : (ex is ArgumentException or InvalidOperationException ? "ERR_VALIDATION" : "ERR_UNEXPECTED");
            var retriable = ex is TimeoutException or OperationCanceledException;
            return new AntiDetectionSnapshotMcp(new global::HushOps.Core.AntiDetection.AntiDetectionSnapshot(), false, $"采集异常: {ex.Message}", null, new List<string> { "EXCEPTION" }, true, code, retriable, requestId);
        }
    }

    private static IEnumerable<string> ValidateExtended(global::HushOps.Core.AntiDetection.AntiDetectionSnapshot s, AntiDetectionWhitelist w)
    {
        var violations = new List<string>();
        // UA 包含/不包含
        var ua = s.Ua ?? string.Empty;
        if (w.UserAgentMustContain != null)
            foreach (var t in w.UserAgentMustContain)
                if (!string.IsNullOrEmpty(t) && !ua.Contains(t, StringComparison.OrdinalIgnoreCase))
                    violations.Add($"UA_MUST_CONTAIN:{t}");
        if (w.UserAgentMustNotContain != null)
            foreach (var t in w.UserAgentMustNotContain)
                if (!string.IsNullOrEmpty(t) && ua.Contains(t, StringComparison.OrdinalIgnoreCase))
                    violations.Add($"UA_MUST_NOT_CONTAIN:{t}");
        // 语言前缀
        if (w.LanguagesPrefixAny != null && w.LanguagesPrefixAny.Length > 0)
        {
            var first = (s.Languages != null && s.Languages.Length > 0) ? s.Languages[0] : (s.Language ?? string.Empty);
            if (!w.LanguagesPrefixAny.Any(p => !string.IsNullOrEmpty(p) && first.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
                violations.Add("LANG_PREFIX_NOT_ALLOWED");
        }
        // WebGL 匹配
        if (w.WebglVendors != null && w.WebglVendors.Length > 0)
            if (string.IsNullOrWhiteSpace(s.WebglVendor) || !w.WebglVendors.Contains(s.WebglVendor))
                violations.Add("WEBGL_VENDOR_NOT_ALLOWED");
        if (w.WebglRendererRegex != null && w.WebglRendererRegex.Length > 0)
        {
            var ok = false;
            foreach (var re in w.WebglRendererRegex)
            {
                try
                {
                    if (!string.IsNullOrEmpty(re) && System.Text.RegularExpressions.Regex.IsMatch(s.WebglRenderer ?? string.Empty, re)) { ok = true; break; }
                }
                catch { }
            }
            if (!ok) violations.Add("WEBGL_RENDERER_NOT_ALLOWED");
        }
        // 硬件/显示/存储/权限
        if (w.MinHardwareConcurrency.HasValue && s.HardwareConcurrency.HasValue && s.HardwareConcurrency.Value < w.MinHardwareConcurrency.Value)
            violations.Add("HARDWARE_CONCURRENCY_TOO_LOW");
        if (w.MinDevicePixelRatio.HasValue && s.DevicePixelRatio.HasValue && s.DevicePixelRatio.Value < w.MinDevicePixelRatio.Value)
            violations.Add("DEVICE_PIXEL_RATIO_TOO_LOW");
        if (w.MinLocalStorageKeys.HasValue && s.LocalStorageKeys.HasValue && s.LocalStorageKeys.Value < w.MinLocalStorageKeys.Value)
            violations.Add("LOCAL_STORAGE_TOO_SMALL");
        if (w.MinSessionStorageKeys.HasValue && s.SessionStorageKeys.HasValue && s.SessionStorageKeys.Value < w.MinSessionStorageKeys.Value)
            violations.Add("SESSION_STORAGE_TOO_SMALL");
        if (w.CookiesEnabled.HasValue && s.CookiesEnabled.HasValue && s.CookiesEnabled.Value != w.CookiesEnabled.Value)
            violations.Add("COOKIES_ENABLED_MISMATCH");
        return violations;
    }

    private static async Task<string> WriteAuditFallbackAsync(string relativePath, AntiDetectionSnapshot snapshot)
    {
        var fullPath = Path.GetFullPath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(fullPath, json);
        return fullPath;
    }
}
