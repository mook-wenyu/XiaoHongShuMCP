using System;

namespace HushOps.Servers.XiaoHongShu.Services.Browser;

/// <summary>
/// 中文：浏览器连接模式，区分启动新实例与连接现有实例。
/// English: Browser connection mode distinguishing between launching new instances and connecting to existing ones.
/// </summary>
public enum BrowserConnectionMode
{
    /// <summary>
    /// 中文：自动模式 - 优先尝试 CDP 连接，失败则启动新实例。
    /// English: Auto mode - tries CDP connection first, falls back to launching new instance.
    /// </summary>
    Auto,

    /// <summary>
    /// 中文：启动模式 - 使用 LaunchPersistentContext 启动新浏览器实例。
    /// English: Launch mode - uses LaunchPersistentContext to start a new browser instance.
    /// </summary>
    Launch,

    /// <summary>
    /// 中文：CDP 连接模式 - 连接到已运行的带调试端口的浏览器。
    /// English: CDP mode - connects to an already running browser with remote debugging enabled.
    /// </summary>
    ConnectCdp
}
