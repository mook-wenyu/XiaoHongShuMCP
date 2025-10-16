using System;
using System.Collections.Generic;

namespace HushOps.Servers.XiaoHongShu.Configuration;

/// <summary>
/// 中文：网络策略配置，包含代理、节流与重试等参数。
/// English: Configuration describing network strategy templates.
/// </summary>
public sealed class NetworkStrategyOptions
{
    public const string SectionName = "NetworkStrategy";

    public string DefaultTemplate { get; set; } = "default";

    public IDictionary<string, NetworkTemplateOptions> Templates { get; set; }
        = CreateDefaults();

    private static IDictionary<string, NetworkTemplateOptions> CreateDefaults()
        => new Dictionary<string, NetworkTemplateOptions>(StringComparer.OrdinalIgnoreCase)
        {
            ["default"] = NetworkTemplateOptions.CreateDefault(),
            ["residential"] = NetworkTemplateOptions.CreateResidential()
        };
}

/// <summary>
/// 中文：单个网络策略模板的详细参数。
/// English: Options describing throttling, retry and proxy preferences per template.
/// </summary>
public sealed class NetworkTemplateOptions
{
    public string ProxyPool { get; set; } = "default";
    public string? ProxyAddress { get; set; }
        = null;
    public string? ProxyUsername { get; set; }
        = null;
    public string? ProxyPassword { get; set; }
        = null;
    public DelayRangeOptions RequestDelay { get; set; } = new(120, 350);
    public int MaxRetryAttempts { get; set; } = 3;
    public int RetryBaseDelayMs { get; set; } = 200;
    public bool SimulateBandwidth { get; set; }
        = false;
    public int DownstreamKbps { get; set; } = 2000;
    public int UpstreamKbps { get; set; } = 800;

    public static NetworkTemplateOptions CreateDefault() => new();

    public static NetworkTemplateOptions CreateResidential() => new()
    {
        ProxyPool = "residential",
        ProxyAddress = "http://127.0.0.1:24000",
        RequestDelay = new DelayRangeOptions(260, 620),
        MaxRetryAttempts = 2,
        RetryBaseDelayMs = 350,
        SimulateBandwidth = true,
        DownstreamKbps = 1800,
        UpstreamKbps = 600
    };
}
