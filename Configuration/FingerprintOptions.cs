using System;
using System.Collections.Generic;

namespace HushOps.Servers.XiaoHongShu.Configuration;

/// <summary>
/// 中文：浏览器指纹配置，定义不同 profile 使用的模板。
/// English: Configuration describing fingerprint templates for each profile key.
/// </summary>
public sealed class FingerprintOptions
{
    public const string SectionName = "Fingerprint";

    public string DefaultTemplate { get; set; } = "default";

    public IDictionary<string, FingerprintTemplateOptions> Templates { get; set; }
        = CreateDefaultTemplates();

    private static IDictionary<string, FingerprintTemplateOptions> CreateDefaultTemplates()
        => new Dictionary<string, FingerprintTemplateOptions>(StringComparer.OrdinalIgnoreCase)
        {
            ["default"] = FingerprintTemplateOptions.CreateDefault(),
            ["mobile"] = FingerprintTemplateOptions.CreateMobile()
        };
}

/// <summary>
/// 中文：单个指纹模板的配置。
/// English: Options describing a single fingerprint template.
/// </summary>
public sealed class FingerprintTemplateOptions
{
    public string UserAgent { get; set; } = "Mozilla/5.0";
    public string Timezone { get; set; } = "Asia/Shanghai";
    public string Language { get; set; } = "zh-CN";
    public bool CanvasNoise { get; set; } = true;
    public bool WebglMask { get; set; } = true;
    public int ViewportWidth { get; set; } = 1440;
    public int ViewportHeight { get; set; } = 900;
    public double DeviceScaleFactor { get; set; } = 1.0;
    public bool IsMobile { get; set; }
        = false;
    public bool HasTouch { get; set; }
        = false;
    public IDictionary<string, string> ExtraHeaders { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public static FingerprintTemplateOptions CreateDefault() => new()
    {
        UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/117.0 Safari/537.36",
        Timezone = "Asia/Shanghai",
        Language = "zh-CN",
        CanvasNoise = true,
        WebglMask = true,
        ViewportWidth = 1440,
        ViewportHeight = 900,
        DeviceScaleFactor = 1.0,
        IsMobile = false,
        HasTouch = false,
        ExtraHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Accept-Language"] = "zh-CN,zh;q=0.9,en-US;q=0.8,en;q=0.7"
        }
    };

    public static FingerprintTemplateOptions CreateMobile() => new()
    {
        UserAgent = "Mozilla/5.0 (iPhone; CPU iPhone OS 16_4 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/16.4 Mobile/15E148 Safari/604.1",
        Timezone = "Asia/Shanghai",
        Language = "zh-CN",
        CanvasNoise = false,
        WebglMask = false,
        ViewportWidth = 390,
        ViewportHeight = 844,
        DeviceScaleFactor = 3.0,
        IsMobile = true,
        HasTouch = true,
        ExtraHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Accept-Language"] = "zh-CN,zh;q=0.9"
        }
    };
}
