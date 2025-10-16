using System;

namespace HushOps.Servers.XiaoHongShu.Services.Browser.Profile;

public enum WebRtcMode
{
    DefaultRouteOnly,
    ForceProxy
}

/// <summary>
/// 一次定型的浏览器 Profile 记录（长期固定，不在原记录上修改关键字段）。
/// </summary>
public sealed class ProfileRecord
{
    public string ProfileKey { get; set; } = string.Empty;
    public string Region { get; set; } = "CN-Shanghai";

    // 浏览器通道/运行
    public string BrowserChannel { get; set; } = "msedge";

    // 本地持久化上下文目录
    public string UserDataDir { get; set; } = string.Empty;

    // 区域与语言
    public string Locale { get; set; } = "zh-CN";
    public string TimezoneId { get; set; } = "Asia/Shanghai";
    public string AcceptLanguage { get; set; } = "zh-CN,zh;q=0.9,en;q=0.6";

    // UA 与 UA-CH（仅记录快照，不主动覆盖 UA-CH 传输行为）
    public string UserAgent { get; set; } = string.Empty;
    public string? UaChBrandsJson { get; set; }
        = null; // JSON 字符串（可选）
    public string? UaChPlatform { get; set; }
        = null;
    public bool UaChMobile { get; set; }
        = false;

    // 视口与 DPR
    public int ViewportWidth { get; set; } = 1920;
    public int ViewportHeight { get; set; } = 1080;
    public double DeviceScaleFactor { get; set; } = 1.25d;

    // 硬件与图形（稳定组合）
    public int HardwareConcurrency { get; set; } = 8;
    public string Platform { get; set; } = "Win32";
    public string Vendor { get; set; } = "Google Inc.";
    public string WebglVendor { get; set; } = "Intel Inc.";
    public string WebglRenderer { get; set; } = "ANGLE (Intel(R) UHD Graphics Direct3D11 vs_5_0 ps_5_0, D3D11)";

    // 指纹种子（固定）
    public double CanvasSeed { get; set; }
        = 834275.11;
    public double WebglSeed { get; set; }
        = 99301.77;

    // WebRTC
    public WebRtcMode WebRtc { get; set; } = WebRtcMode.DefaultRouteOnly;

    // 黏性代理（长期绑定），为空表示尚未分配
    public string? ProxyEndpoint { get; set; } = null;
    public int StickyTtlDays { get; set; } = 14;
    public DateTimeOffset ProxyAssignedAt { get; set; } = DateTimeOffset.MinValue;

    // 元数据
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset FrozenAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastUsedAt { get; set; }
        = null;

    public bool IsProxyExpired()
        => ProxyAssignedAt != DateTimeOffset.MinValue && (DateTimeOffset.UtcNow - ProxyAssignedAt).TotalDays > StickyTtlDays;
}