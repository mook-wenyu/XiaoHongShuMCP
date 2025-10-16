using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace HushOps.Servers.XiaoHongShu.Configuration;

/// <summary>
/// 中文：小红书服务器配置模型（保留默认关键词、画像与人性化节奏设置）。
/// </summary>
public sealed class XiaoHongShuOptions
{
    public const string SectionName = "xhs";

    /// <summary>
    /// 中文：是否使用无头模式（主要用于自动化测试）。用户持久化会话仍默认有头。
    /// </summary>
    public bool Headless { get; init; } = false;

    /// <summary>
    /// 中文：默认关键词，画像缺失或关键字为空时回退。
    /// </summary>
    public string? DefaultKeyword { get; init; }

    /// <summary>
    /// 中文：画像配置集合，包含标签与补充元数据。
    /// </summary>
    public List<PortraitOptions> Portraits { get; init; } = new();

    /// <summary>
    /// 中文：人性化节奏配置。
    /// </summary>
    public HumanizedOptions Humanized { get; init; } = new();

    public sealed class HumanizedOptions
    {
        [Range(0, int.MaxValue)]
        public int MinDelayMs { get; init; } = 800;

        [Range(0, int.MaxValue)]
        public int MaxDelayMs { get; init; } = 2600;

        [Range(0, 1)]
        public double Jitter { get; init; } = 0.2;
    }

    public sealed class PortraitOptions
    {
        [Required]
        public string Id { get; init; } = string.Empty;

        public List<string> Tags { get; init; } = new();

        public Dictionary<string, string> Metadata { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
