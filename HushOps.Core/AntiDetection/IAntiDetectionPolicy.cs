using System;
using System.Collections.Generic;

namespace HushOps.Core.AntiDetection;

/// <summary>
/// 反检测策略契约（仅抽象，禁止直接依赖具体运行时）。
/// - 负责判定哪些“注入型动作”可被放行（默认全部禁止）；
/// - 提供只读 Evaluate 允许的路径标签白名单；
/// - 为快照校验提供白名单/基线结构（键名统一，便于跨端复用）。
/// 说明：实现放在 Adapters 层（例如 Playwright 适配器）。
/// </summary>
public interface IAntiDetectionPolicy
{
    /// <summary>是否启用反检测引擎（整体开关）。</summary>
    bool Enabled { get; }

    /// <summary>是否允许“隐藏 navigator.webdriver”注入（默认 false）。</summary>
    bool AllowNavigatorWebdriverPatch { get; }

    /// <summary>是否允许“UA/Language 清洗”注入（默认 false）。</summary>
    bool AllowUaLanguageScrub { get; }

    /// <summary>允许的只读 Evaluate 路径标签集合（如 element.tagName、element.html.sample）。</summary>
    IReadOnlySet<string> AllowedEvalPathLabels { get; }

    /// <summary>
    /// 是否允许“UI 注入兜底”（例如基于 dispatchEvent 的点击触发等）。
    /// - 默认禁止（false）；
    /// - 需通过受控的 AntiDetectionPipeline 执行，并产生审计记录；
    /// - 仅在确有反检测需要的场景下临时开启，完成后应关闭。
    /// 对应环境变量：XHS__AntiDetection__EnableJsInjectionFallback（true/false）。
    /// </summary>
    bool AllowUiInjectionFallback { get; }

    /// <summary>根据策略写入审计记录的帮助器（目录、对象）。实现方保证异常安全。</summary>
    void WriteAudit(string auditDirectory, string name, object payload);
}

/// <summary>
/// 反检测白名单/基线数据结构（跨端统一）。
/// - 仅包含用于判定的键；留空表示不校验该项。
/// - 由策略引擎或工具层执行 Validate 生成 violations 列表。
/// </summary>
public sealed class AntiDetectionWhitelist
{
    // 基础特征
    public string[]? AllowedPlatforms { get; set; }
    public string[]? AllowedTimeZones { get; set; }
    public bool[]? AllowedWebdrivers { get; set; }
    public string[]? UserAgentMustContain { get; set; }
    public string[]? UserAgentMustNotContain { get; set; }
    public string[]? LanguagesPrefixAny { get; set; }

    // WebGL
    public string[]? WebglVendors { get; set; }
    public string[]? WebglRendererRegex { get; set; }

    // 设备/显示
    public int? MinHardwareConcurrency { get; set; }
    public int? MinDevicePixelRatio { get; set; }

    // 存储与权限（只读统计）
    public int? MinLocalStorageKeys { get; set; }
    public int? MinSessionStorageKeys { get; set; }
    public bool? CookiesEnabled { get; set; }

    // 扩展键：字体/权限/媒体设备/传感器
    // 说明：所有键均为“低基数/离散集合”或“计数阈值”，避免高基数。
    public string[]? FontsMustContainAny { get; set; }
    public Dictionary<string, string[]>? PermissionStates { get; set; } // 例如 { "notifications": ["prompt","granted"] }
    public int? MinMediaVideoInputs { get; set; }
    public int? MinMediaAudioInputs { get; set; }
    public int? MinMediaAudioOutputs { get; set; }
    public string[]? RequiredSensorsAny { get; set; }
    public string[]? ForbiddenSensorsAny { get; set; }

    /// <summary>
    /// 允许的最大违反条目数（阈值）。为空或0表示零容忍；大于0时，<=该阈值视为通过（Degrade 可选）。
    /// </summary>
    public int? MaxViolations { get; set; }
}

/// <summary>
/// 反检测基线校验结果。
/// </summary>
public sealed class AntiDetectionValidationResult
{
    public required List<string> Violations { get; init; }
    public required int TotalViolations { get; init; }
    public bool DegradeRecommended { get; init; }
}

/// <summary>
/// 反检测基线校验器（纯函数，不做副作用）。
/// </summary>
public static class AntiDetectionBaselineValidator
{
    /// <summary>
    /// 对 <paramref name="snapshot"/> 按 <paramref name="whitelist"/> 校验，返回违反列表与是否建议降级。
    /// </summary>
    public static AntiDetectionValidationResult Validate(AntiDetectionSnapshot snapshot, AntiDetectionWhitelist whitelist)
    {
        var v = new List<string>();

        // webdriver：默认要求 false
        var webdriverAllowed = (whitelist.AllowedWebdrivers != null && whitelist.AllowedWebdrivers.Length > 0)
            ? (snapshot.Webdriver.HasValue && whitelist.AllowedWebdrivers.Contains(snapshot.Webdriver.Value))
            : (snapshot.Webdriver == false);
        if (!webdriverAllowed) v.Add("WEBDRIVER_NOT_ALLOWED");

        // platform
        if (whitelist.AllowedPlatforms is { Length: >0 })
        {
            if (string.IsNullOrWhiteSpace(snapshot.Platform) || !whitelist.AllowedPlatforms.Contains(snapshot.Platform))
                v.Add("PLATFORM_NOT_ALLOWED");
        }

        // timeZone
        if (whitelist.AllowedTimeZones is { Length: >0 })
        {
            if (string.IsNullOrWhiteSpace(snapshot.TimeZone) || !whitelist.AllowedTimeZones.Contains(snapshot.TimeZone))
                v.Add("TIMEZONE_NOT_ALLOWED");
        }

        // UA 包含/不包含
        var ua = snapshot.Ua ?? string.Empty;
        if (whitelist.UserAgentMustContain is { Length: >0 })
        {
            foreach (var t in whitelist.UserAgentMustContain)
                if (!string.IsNullOrEmpty(t) && !ua.Contains(t, StringComparison.OrdinalIgnoreCase))
                    v.Add($"UA_MUST_CONTAIN:{t}");
        }
        if (whitelist.UserAgentMustNotContain is { Length: >0 })
        {
            foreach (var t in whitelist.UserAgentMustNotContain)
                if (!string.IsNullOrEmpty(t) && ua.Contains(t, StringComparison.OrdinalIgnoreCase))
                    v.Add($"UA_MUST_NOT_CONTAIN:{t}");
        }

        // WebGL
        if (whitelist.WebglVendors is { Length: >0 })
        {
            if (string.IsNullOrWhiteSpace(snapshot.WebglVendor) || !whitelist.WebglVendors.Contains(snapshot.WebglVendor))
                v.Add("WEBGL_VENDOR_NOT_ALLOWED");
        }
        if (whitelist.WebglRendererRegex is { Length: >0 })
        {
            var ok = false;
            foreach (var re in whitelist.WebglRendererRegex)
            {
                try
                {
                    if (!string.IsNullOrEmpty(re) && System.Text.RegularExpressions.Regex.IsMatch(snapshot.WebglRenderer ?? string.Empty, re))
                    { ok = true; break; }
                }
                catch { }
            }
            if (!ok) v.Add("WEBGL_RENDERER_NOT_ALLOWED");
        }

        // 语言前缀
        if (whitelist.LanguagesPrefixAny is { Length: >0 })
        {
            var first = (snapshot.Languages != null && snapshot.Languages.Length > 0) ? snapshot.Languages[0] : (snapshot.Language ?? string.Empty);
            if (!whitelist.LanguagesPrefixAny.Any(p => !string.IsNullOrEmpty(p) && first.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
                v.Add("LANG_PREFIX_NOT_ALLOWED");
        }

        // 设备/显示
        if (whitelist.MinHardwareConcurrency.HasValue && snapshot.HardwareConcurrency.HasValue && snapshot.HardwareConcurrency.Value < whitelist.MinHardwareConcurrency.Value)
            v.Add("HARDWARE_CONCURRENCY_TOO_LOW");
        if (whitelist.MinDevicePixelRatio.HasValue && snapshot.DevicePixelRatio.HasValue && snapshot.DevicePixelRatio.Value < whitelist.MinDevicePixelRatio.Value)
            v.Add("DEVICE_PIXEL_RATIO_TOO_LOW");

        // 存储/权限（只读统计）
        if (whitelist.MinLocalStorageKeys.HasValue && snapshot.LocalStorageKeys.HasValue && snapshot.LocalStorageKeys.Value < whitelist.MinLocalStorageKeys.Value)
            v.Add("LOCAL_STORAGE_TOO_SMALL");
        if (whitelist.MinSessionStorageKeys.HasValue && snapshot.SessionStorageKeys.HasValue && snapshot.SessionStorageKeys.Value < whitelist.MinSessionStorageKeys.Value)
            v.Add("SESSION_STORAGE_TOO_SMALL");
        if (whitelist.CookiesEnabled.HasValue && snapshot.CookiesEnabled.HasValue && snapshot.CookiesEnabled.Value != whitelist.CookiesEnabled.Value)
            v.Add("COOKIES_ENABLED_MISMATCH");

        // 扩展：字体
        if (whitelist.FontsMustContainAny is { Length: >0 })
        {
            var fonts = snapshot.Fonts ?? Array.Empty<string>();
            if (!whitelist.FontsMustContainAny.Any(req => fonts.Any(f => string.Equals(f, req, StringComparison.OrdinalIgnoreCase))))
                v.Add("FONTS_MUST_CONTAIN_ANY_MISSING");
        }

        // 扩展：权限状态
        if (whitelist.PermissionStates is { Count: >0 })
        {
            var perms = snapshot.Permissions ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in whitelist.PermissionStates)
            {
                var name = kv.Key;
                var allowed = kv.Value ?? Array.Empty<string>();
                if (allowed.Length == 0) continue;
                if (!perms.TryGetValue(name, out var state) || !allowed.Contains(state, StringComparer.OrdinalIgnoreCase))
                    v.Add($"PERMISSION_STATE_DENIED:{name}");
            }
        }

        // 扩展：媒体设备最小计数
        if (whitelist.MinMediaVideoInputs.HasValue && (snapshot.MediaVideoInputs ?? 0) < whitelist.MinMediaVideoInputs.Value)
            v.Add("MEDIA_VIDEO_INPUTS_TOO_LOW");
        if (whitelist.MinMediaAudioInputs.HasValue && (snapshot.MediaAudioInputs ?? 0) < whitelist.MinMediaAudioInputs.Value)
            v.Add("MEDIA_AUDIO_INPUTS_TOO_LOW");
        if (whitelist.MinMediaAudioOutputs.HasValue && (snapshot.MediaAudioOutputs ?? 0) < whitelist.MinMediaAudioOutputs.Value)
            v.Add("MEDIA_AUDIO_OUTPUTS_TOO_LOW");

        // 扩展：传感器支持
        if (whitelist.RequiredSensorsAny is { Length: >0 })
        {
            var sns = snapshot.Sensors ?? new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            if (!whitelist.RequiredSensorsAny.Any(s => sns.TryGetValue(s, out var val) && val))
                v.Add("SENSOR_REQUIRED_MISSING");
        }
        if (whitelist.ForbiddenSensorsAny is { Length: >0 })
        {
            var sns = snapshot.Sensors ?? new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            if (whitelist.ForbiddenSensorsAny.Any(s => sns.TryGetValue(s, out var val) && val))
                v.Add("SENSOR_FORBIDDEN_PRESENT");
        }

        var total = v.Count;
        var threshold = whitelist.MaxViolations.GetValueOrDefault(0);
        var degrade = (total > 0 && total <= threshold);
        return new AntiDetectionValidationResult { Violations = v, TotalViolations = total, DegradeRecommended = degrade };
    }
}
