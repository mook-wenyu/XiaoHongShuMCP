using System;

namespace HushOps.Servers.XiaoHongShu.Services.Browser.Playwright;

/// <summary>
/// 中文：Playwright 浏览器安装相关的异常类型，用于附加指引信息。
/// </summary>
public sealed class PlaywrightInstallationException : Exception
{
    public PlaywrightInstallationException(string message)
        : base(message)
    {
    }

    public PlaywrightInstallationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
