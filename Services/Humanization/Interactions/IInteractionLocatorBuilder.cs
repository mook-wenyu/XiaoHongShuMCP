using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace HushOps.Servers.XiaoHongShu.Services.Humanization.Interactions;

/// <summary>
/// 中文：定义定位器生成器，用于将 <see cref="ActionLocator"/> 转换为 Playwright 定位器。
/// English: Defines a builder that converts <see cref="ActionLocator"/> hints into Playwright locators.
/// </summary>
public interface IInteractionLocatorBuilder
{
    /// <summary>
    /// 中文：根据定位线索解析元素定位器，不存在则抛出异常。
    /// English: Resolves a locator for the specified hints or throws when the element cannot be found.
    /// </summary>
    /// <param name="page">Playwright 页面实例。</param>
    /// <param name="locator">定位线索集合。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>匹配的 <see cref="ILocator"/>。</returns>
    Task<ILocator> ResolveAsync(IPage page, ActionLocator locator, CancellationToken cancellationToken = default);
}
