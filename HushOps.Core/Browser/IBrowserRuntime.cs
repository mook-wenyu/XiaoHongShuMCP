using System.Threading;
using System.Threading.Tasks;
using HushOps.Core.Automation.Abstractions;

namespace HushOps.Core.Browser;

/// <summary>
/// 中文：浏览器运行时门面接口（IBrowserRuntime）。
/// - 设计目标：在 Core 内部为具体运行时实现（如 Playwright）提供统一入口，
///   对外暴露稳定、可替换的契约，供 MCP 层与上层服务编排使用；
/// - 约束：禁止业务层直接依赖具体运行时 API（如 Microsoft.Playwright），只能经由本接口与抽象 IAuto* 交互；
/// - 默认实现：Core.Runtime.Playwright.PlaywrightBrowserDriver 实现本接口（也是 IBrowserDriver/IAutomationRuntime）。
/// </summary>
public interface IBrowserRuntime
{
    /// <summary>默认浏览器驱动（抽象），用于创建会话/页面。</summary>
    IBrowserDriver DefaultDriver { get; }

    /// <summary>
    /// 创建一个新会话（浏览器/上下文），返回抽象会话对象。
    /// </summary>
    Task<IAutoSession> CreateSessionAsync(Automation.Abstractions.BrowserLaunchOptions options, CancellationToken ct = default);
}

/// <summary>
/// 中文：基于 IBrowserRuntime 的便利扩展方法。
/// </summary>
public static class BrowserRuntimeExtensions
{
    /// <summary>快速创建页面（单会话单页短事务）。</summary>
    public static async Task<IAutoPage> NewPageAsync(this IBrowserRuntime runtime, Automation.Abstractions.BrowserLaunchOptions options, CancellationToken ct = default)
    {
        var session = await runtime.CreateSessionAsync(options, ct);
        return await session.NewPageAsync(ct);
    }
}

