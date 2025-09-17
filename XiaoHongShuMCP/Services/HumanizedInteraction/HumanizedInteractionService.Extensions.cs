using System.Threading;
using System.Threading.Tasks;
using HushOps.Core.Automation.Abstractions;
using HushOps.Core.Runtime.Playwright;
using Microsoft.Playwright;

namespace XiaoHongShuMCP.Services
{
    /// <summary>
    /// IHumanizedInteractionService 的 Playwright 适配扩展：
    /// 允许以 IPage/IElementHandle 形式调用接口方法，内部自动封装为 IAuto*。
    /// </summary>
    public static class HumanizedInteractionServiceExtensions
    {
        public static async Task HumanClickAsync(this IHumanizedInteractionService svc, IElementHandle element)
        {
            var autoEl = PlaywrightAutoFactory.Wrap(element);
            await svc.HumanClickAsync(autoEl);
        }

        public static async Task HumanHoverAsync(this IHumanizedInteractionService svc, IElementHandle element)
        {
            var autoEl = PlaywrightAutoFactory.Wrap(element);
            await svc.HumanHoverAsync(autoEl);
        }

        public static async Task HumanTypeAsync(this IHumanizedInteractionService svc, IPage page, string selectorAlias, string text)
        {
            var autoPage = PlaywrightAutoFactory.Wrap(page);
            await svc.HumanTypeAsync(autoPage, selectorAlias, text);
        }

        public static async Task HumanScrollAsync(this IHumanizedInteractionService svc, IPage page, CancellationToken cancellationToken = default)
        {
            var autoPage = PlaywrightAutoFactory.Wrap(page);
            await svc.HumanScrollAsync(autoPage, cancellationToken);
        }

        public static async Task HumanScrollAsync(this IHumanizedInteractionService svc, IPage page, int targetDistance, bool waitForLoad = true, CancellationToken cancellationToken = default)
        {
            var autoPage = PlaywrightAutoFactory.Wrap(page);
            await svc.HumanScrollAsync(autoPage, targetDistance, waitForLoad, cancellationToken);
        }

        public static async Task<IAutoElement?> FindElementAsync(this IHumanizedInteractionService svc, IPage page, string selectorAlias, int retries = 3, int timeout = 3000)
        {
            var autoPage = PlaywrightAutoFactory.Wrap(page);
            return await svc.FindElementAsync(autoPage, selectorAlias, retries, timeout);
        }

        public static async Task<IAutoElement?> FindElementAsync(this IHumanizedInteractionService svc, IPage page, string selectorAlias, PageState pageState, int retries = 3, int timeout = 3000, CancellationToken cancellationToken = default)
        {
            var autoPage = PlaywrightAutoFactory.Wrap(page);
            return await svc.FindElementAsync(autoPage, selectorAlias, pageState, retries, timeout, cancellationToken);
        }

        public static async Task<InteractionResult> HumanUnlikeAsync(this IHumanizedInteractionService svc, IPage page)
        {
            var autoPage = PlaywrightAutoFactory.Wrap(page);
            return await svc.HumanUnlikeAsync(autoPage);
        }

        public static async Task<InteractionResult> HumanFavoriteAsync(this IHumanizedInteractionService svc, IPage page)
        {
            var autoPage = PlaywrightAutoFactory.Wrap(page);
            return await svc.HumanFavoriteAsync(autoPage);
        }

        public static async Task<InteractionResult> HumanUnfavoriteAsync(this IHumanizedInteractionService svc, IPage page)
        {
            var autoPage = PlaywrightAutoFactory.Wrap(page);
            return await svc.HumanUnfavoriteAsync(autoPage);
        }
    }
}
