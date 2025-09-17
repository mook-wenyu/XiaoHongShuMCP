namespace HushOps.Core.Config;

/// <summary>
/// 中文：配置加载器，仅接受白名单键（XHS__ 前缀）。
/// </summary>
public static class XhsConfiguration
{
    private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        "XHS__BrowserSettings__Headless",
        "XHS__BrowserSettings__UserDataDir",
        "XHS__BrowserSettings__Locale",
        "XHS__BrowserSettings__TimezoneId",
        "XHS__AntiDetection__Enabled",
        "XHS__AntiDetection__AuditDirectory",
        "XHS__AntiDetection__EnableJsInjectionFallback",
        "XHS__AntiDetection__EnableJsReadEval",
        "XHS__AntiDetection__PatchNavigatorWebdriver",
        "XHS__Metrics__Enabled",
        "XHS__Metrics__MeterName",
        "XHS__Metrics__AllowedLabels",
        "XHS__Metrics__UnknownRatioThreshold",
        "XHS__InteractionPolicy__EnableJsInjectionFallback",
        "XHS__InteractionPolicy__EnableJsReadEval",
        "XHS__InteractionPolicy__EnableHtmlSampleAudit",
        "XHS__InteractionPolicy__EvalAllowedPaths",
        "XHS__InteractionPolicy__PacingMultiplier",
        // MCP 配置
        "XHS__Mcp__PolicyJson",
    };

    public static XhsSettings LoadFromEnvironment()
    {
        var env = Environment.GetEnvironmentVariables();
        var unknownKeys = new List<string>();

        bool b(string key, bool def) => bool.TryParse(Environment.GetEnvironmentVariable(key), out var v) ? v : def;
        string? s(string key, string? def = null) => Environment.GetEnvironmentVariable(key) ?? def;
        double d(string key, double def) => double.TryParse(Environment.GetEnvironmentVariable(key), out var v) ? v : def;

        foreach (var k in env.Keys)
        {
            var ks = k?.ToString();
            if (ks is null) continue;
            if (ks.StartsWith("XHS__", StringComparison.OrdinalIgnoreCase) && !Allowed.Contains(ks))
                unknownKeys.Add(ks);
        }
        if (unknownKeys.Count > 0)
            Console.Error.WriteLine($"[配置警告] 存在未在白名单中的 XHS__ 键：{string.Join(",", unknownKeys)}");

        var evalRaw = s("XHS__InteractionPolicy__EvalAllowedPaths");
        var evalPaths = string.IsNullOrWhiteSpace(evalRaw)
            ? Array.Empty<string>()
            : evalRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var settings = new XhsSettings
        {
            BrowserSettings = new XhsSettings.BrowserOptions
            {
                Headless = b("XHS__BrowserSettings__Headless", true),
                UserDataDir = s("XHS__BrowserSettings__UserDataDir"),
                Locale = s("XHS__BrowserSettings__Locale", "zh-CN")!,
                TimezoneId = s("XHS__BrowserSettings__TimezoneId", "Asia/Shanghai")!,
            },
            AntiDetection = new XhsSettings.AntiDetectionOptions
            {
                Enabled = b("XHS__AntiDetection__Enabled", true),
                AuditDirectory = s("XHS__AntiDetection__AuditDirectory", ".audit")!,
                EnableJsInjectionFallback = b("XHS__AntiDetection__EnableJsInjectionFallback", false),
                EnableJsReadEval = b("XHS__AntiDetection__EnableJsReadEval", false),
                PatchNavigatorWebdriver = b("XHS__AntiDetection__PatchNavigatorWebdriver", false),
            },
            Metrics = new XhsSettings.MetricsOptions
            {
                Enabled = b("XHS__Metrics__Enabled", true),
                MeterName = s("XHS__Metrics__MeterName", "XHS.Metrics")!,
                AllowedLabelsCsv = s("XHS__Metrics__AllowedLabels") ?? string.Join(',', HushOps.Observability.InProcessMetrics.DefaultAllowedLabels),
                UnknownRatioThreshold = d("XHS__Metrics__UnknownRatioThreshold", 0.30),
            },
            InteractionPolicy = new XhsSettings.InteractionPolicyOptions
            {
                EnableJsInjectionFallback = b("XHS__InteractionPolicy__EnableJsInjectionFallback", false),
                EnableJsReadEval = b("XHS__InteractionPolicy__EnableJsReadEval", false),
                EnableHtmlSampleAudit = b("XHS__InteractionPolicy__EnableHtmlSampleAudit", false),
                EvalAllowedPaths = evalPaths,
                PacingMultiplier = d("XHS__InteractionPolicy__PacingMultiplier", 1.0),
            },
            Mcp = new XhsSettings.McpOptions
            {
                PolicyJson = s("XHS__Mcp__PolicyJson")
            }
        };
        return settings;
    }

    public static IReadOnlyList<string> DetectUnknownXhsKeys()
    {
        var env = Environment.GetEnvironmentVariables();
        var unknownKeys = new List<string>();
        foreach (var k in env.Keys)
        {
            var ks = k?.ToString();
            if (ks is null) continue;
            if (ks.StartsWith("XHS__", StringComparison.OrdinalIgnoreCase) && !Allowed.Contains(ks))
                unknownKeys.Add(ks);
        }
        return unknownKeys;
    }
}
