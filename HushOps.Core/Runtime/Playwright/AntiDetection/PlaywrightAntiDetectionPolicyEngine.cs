using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using HushOps.Core.AntiDetection;

namespace HushOps.Core.Runtime.Playwright.AntiDetection;

/// <summary>
/// Playwright 运行时的反检测策略引擎实现。
/// - 默认仅允许“只读 Evaluate”，禁止任何注入；
/// - 通过环境变量控制少量受限注入能力（需显式开启）：
///   - XHS__AntiDetection__Enabled（默认 true）
///   - XHS__AntiDetection__PatchNavigatorWebdriver（默认 false）
///   - XHS__AntiDetection__UaLanguageScrub（默认 false）
///   - XHS__InteractionPolicy__EvalAllowedPaths（逗号分隔白名单标签，可选）
/// - 提供审计写入能力，所有放行/拒绝都会落盘（最佳努力）。
/// </summary>
public sealed class PlaywrightAntiDetectionPolicyEngine : IAntiDetectionPolicy
{
    private readonly HashSet<string> _allowedEvalLabels;
    private readonly bool _allowUiInjectionFallback;

    /// <summary>构造函数：从环境变量读取策略。</summary>
    public PlaywrightAntiDetectionPolicyEngine()
    {
        Enabled = ReadFlag("XHS__AntiDetection__Enabled", true);
        AllowNavigatorWebdriverPatch = ReadFlag("XHS__AntiDetection__PatchNavigatorWebdriver", false);
        AllowUaLanguageScrub = ReadFlag("XHS__AntiDetection__UaLanguageScrub", false);
        _allowUiInjectionFallback = ReadFlag("XHS__AntiDetection__EnableJsInjectionFallback", false);

        var defaults = new[]
        {
            // 元素级读取：不改变状态（默认不含 html.sample）
            "element.tagName", "element.computedStyle", "element.textProbe",
            "element.clickability", "element.probeVisibility",
            // 页面级读取：细化标签
            "page.eval.read",
            // 反检测快照专用
            "antidetect.snapshot"
        };

        var env = Environment.GetEnvironmentVariable("XHS__InteractionPolicy__EvalAllowedPaths");
        if (string.IsNullOrWhiteSpace(env))
            _allowedEvalLabels = new HashSet<string>(defaults, StringComparer.Ordinal);
        else
            _allowedEvalLabels = new HashSet<string>(env.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), StringComparer.Ordinal);
    }

    public bool Enabled { get; }
    public bool AllowNavigatorWebdriverPatch { get; }
    public bool AllowUaLanguageScrub { get; }
    public IReadOnlySet<string> AllowedEvalPathLabels => _allowedEvalLabels;
    public bool AllowUiInjectionFallback => _allowUiInjectionFallback;

    public void WriteAudit(string auditDirectory, string name, object payload)
    {
        try
        {
            Directory.CreateDirectory(auditDirectory);
            var path = Path.Combine(auditDirectory, $"{name}-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}.json");
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
        catch { /* 审计失败不影响主流程 */ }
    }

    private static bool ReadFlag(string key, bool def)
    {
        var v = Environment.GetEnvironmentVariable(key);
        if (string.IsNullOrWhiteSpace(v)) return def;
        return string.Equals(v, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(v, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(v, "yes", StringComparison.OrdinalIgnoreCase);
    }
}
