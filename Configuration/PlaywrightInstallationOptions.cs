using System.Collections.Generic;

namespace HushOps.Servers.XiaoHongShu.Configuration;

/// <summary>
/// 中文：Playwright 浏览器安装配置，允许指定缓存目录与附加参数。
/// </summary>
public sealed class PlaywrightInstallationOptions
{
    public const string SectionName = "playwrightInstallation";

    /// <summary>
    /// 中文：Playwright 浏览器缓存目录，若为空则使用默认路径。
    /// </summary>
    public string? BrowsersPath { get; init; }

    /// <summary>
    /// 中文：需要安装的浏览器列表（如 chromium、firefox、webkit 等），空集合表示使用 Playwright 默认集合。
    /// </summary>
    public IReadOnlyList<string> Browsers { get; init; } = new List<string>();

    /// <summary>
    /// 中文：附加的安装参数，例如 --with-deps。
    /// </summary>
    public IReadOnlyList<string> Arguments { get; init; } = new List<string>();

    /// <summary>
    /// 中文：是否在安装失败时继续尝试（默认 false，遇到错误直接抛出）。
    /// </summary>
    public bool IgnoreFailures { get; init; }

    /// <summary>
    /// 中文：自定义浏览器下载镜像地址（对应 PLAYWRIGHT_DOWNLOAD_HOST），用于受限网络环境。
    /// </summary>
    public string? DownloadHost { get; init; }

    /// <summary>
    /// 中文：若目标浏览器已存在则跳过安装（默认 true）。
    /// </summary>
    public bool SkipIfBrowsersPresent { get; init; } = true;
}
