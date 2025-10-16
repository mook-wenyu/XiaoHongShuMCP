using System;
using System.Collections.Generic;

namespace HushOps.Servers.XiaoHongShu.Services.Browser.Profile;

internal static class RegionMappings
{
    public sealed record RegionSpec(string Locale, string TimezoneId, string AcceptLanguage);

    private static readonly Dictionary<string, RegionSpec> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        ["CN-Shanghai"] = new("zh-CN", "Asia/Shanghai", "zh-CN,zh;q=0.9,en;q=0.6"),
        ["JP-Tokyo"] = new("ja-JP", "Asia/Tokyo", "ja-JP,ja;q=0.9,en;q=0.6"),
        ["US-LosAngeles"] = new("en-US", "America/Los_Angeles", "en-US,en;q=0.9"),
        ["US-NewYork"] = new("en-US", "America/New_York", "en-US,en;q=0.9"),
    };

    public static RegionSpec Resolve(string? region)
    {
        if (!string.IsNullOrWhiteSpace(region) && Map.TryGetValue(region!, out var spec))
        {
            return spec;
        }
        return Map["CN-Shanghai"]; // 默认
    }
}