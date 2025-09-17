namespace HushOps.Core.AntiDetection;

/// <summary>
/// 反检测只读快照（中文）：统一字段键，便于跨端基线与偏差检测；
/// - 严禁在采集阶段修改任何页面状态；
/// - 字段均为小写/驼峰风格，避免高基数复杂对象；
/// - 若目标环境不可用某些 API，允许返回 null（由策略决定降级或中断）。
/// </summary>
public sealed class AntiDetectionSnapshot
{
    /// <summary>采集时间（UTC ISO8601）。</summary>
    public string? CapturedAtUtc { get; init; }
    /// <summary>User-Agent 原文。</summary>
    public string? Ua { get; init; }
    /// <summary>navigator.webdriver 标志（true/false）。</summary>
    public bool? Webdriver { get; init; }
    /// <summary>首选语言（Accept-Language 首位）。</summary>
    public string? Language { get; init; }
    /// <summary>语言数组前三项。</summary>
    public string[]? Languages { get; init; }
    /// <summary>平台标识（navigator.platform）。</summary>
    public string? Platform { get; init; }
    /// <summary>时区 ID（如 Asia/Shanghai）。</summary>
    public string? TimeZone { get; init; }
    /// <summary>设备像素比。</summary>
    public int? DevicePixelRatio { get; init; }
    /// <summary>硬件并发（逻辑 CPU）。</summary>
    public int? HardwareConcurrency { get; init; }
    /// <summary>WebGL 供应商（若可用）。</summary>
    public string? WebglVendor { get; init; }
    /// <summary>WebGL 渲染器（若可用）。</summary>
    public string? WebglRenderer { get; init; }
    /// <summary>是否启用 Cookie。</summary>
    public bool? CookiesEnabled { get; init; }
    /// <summary>localStorage 键数量。</summary>
    public int? LocalStorageKeys { get; init; }
    /// <summary>sessionStorage 键数量。</summary>
    public int? SessionStorageKeys { get; init; }

    // 扩展：字体/权限/媒体设备/传感器
    /// <summary>检测到的已存在字体子集（低基数白名单）</summary>
    public string[]? Fonts { get; init; }
    /// <summary>权限状态映射，例如 { notifications: granted|denied|prompt }</summary>
    public Dictionary<string,string>? Permissions { get; init; }
    /// <summary>媒体输入输出设备计数</summary>
    public int? MediaVideoInputs { get; init; }
    public int? MediaAudioInputs { get; init; }
    public int? MediaAudioOutputs { get; init; }
    /// <summary>传感器能力支持标记（true/false）</summary>
    public Dictionary<string,bool>? Sensors { get; init; }
}
