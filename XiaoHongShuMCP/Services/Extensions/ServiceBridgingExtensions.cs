using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HushOps.Core.Automation.Abstractions;
using HushOps.Core.Runtime.Playwright;
using Microsoft.Playwright;

namespace XiaoHongShuMCP.Services
{
    /// <summary>
    /// 服务层桥接扩展（强类型统一适配）：
    /// - 目的：在不修改既有业务代码的前提下，确保所有对外依赖统一走 IAuto* 强类型通道，
    ///   消除对原生 Playwright 类型（IPage/IElementHandle）的直接耦合，符合“禁注入、强类型、可审计”的反检测基线。
    /// - 原理：提供与既有调用等价的扩展重载，内部使用 PlaywrightAutoFactory.Wrap(...) 将 IPage 封装为 IAutoPage。
    /// - 适用范围：仅用于过渡期桥接 XiaoHongShuService 等服务层遗留 IPage 调用点；
    ///   后续将继续小步迁移至直接传递 IAuto*（彻底移除 IPage 重载）。
    /// </summary>
    public static class ServiceBridgingExtensions
    {
        /// <summary>
        /// IUniversalApiMonitor.SetupMonitor 的 IPage 适配重载（内部统一转 IAutoPage）。
        /// </summary>
        /// <param name="monitor">通用 API 监听器</param>
        /// <param name="page">Playwright IPage（将被封装为 IAutoPage）</param>
        /// <param name="endpointsToMonitor">需要监听的端点集合</param>
        /// <returns>设置是否成功</returns>
        public static bool SetupMonitor(this IUniversalApiMonitor monitor, IPage page, HashSet<ApiEndpointType> endpointsToMonitor)
        {
            if (monitor is null) throw new ArgumentNullException(nameof(monitor));
            if (page is null) throw new ArgumentNullException(nameof(page));
            var auto = PlaywrightAutoFactory.Wrap(page);
            return monitor.SetupMonitor(auto, endpointsToMonitor);
        }

        /// <summary>
        /// IPageStateGuard.EnsureOnDiscoverOrSearchAsync 的 IPage 适配重载（内部统一转 IAutoPage）。
        /// </summary>
        public static Task<bool> EnsureOnDiscoverOrSearchAsync(this IPageStateGuard guard, IPage page)
        {
            if (guard is null) throw new ArgumentNullException(nameof(guard));
            if (page is null) throw new ArgumentNullException(nameof(page));
            var auto = PlaywrightAutoFactory.Wrap(page);
            return guard.EnsureOnDiscoverOrSearchAsync(auto);
        }

        /// <summary>
        /// IPageStateGuard.EnsureExitNoteDetailIfPresentAsync 的 IPage 适配重载。
        /// </summary>
        public static Task<bool> EnsureExitNoteDetailIfPresentAsync(this IPageStateGuard guard, IPage page)
        {
            if (guard is null) throw new ArgumentNullException(nameof(guard));
            if (page is null) throw new ArgumentNullException(nameof(page));
            var auto = PlaywrightAutoFactory.Wrap(page);
            return guard.EnsureExitNoteDetailIfPresentAsync(auto);
        }

        /// <summary>
        /// IPageLoadWaitService.WaitForPageLoadAsync 的 IPage 适配重载（默认策略）。
        /// </summary>
        public static Task<PageLoadWaitResult> WaitForPageLoadAsync(this IPageLoadWaitService svc, IPage page, CancellationToken cancellationToken = default)
        {
            if (svc is null) throw new ArgumentNullException(nameof(svc));
            if (page is null) throw new ArgumentNullException(nameof(page));
            var auto = PlaywrightAutoFactory.Wrap(page);
            return svc.WaitForPageLoadAsync(auto, cancellationToken);
        }

        /// <summary>
        /// IPageLoadWaitService.WaitForPageLoadAsync 的 IPage 适配重载（显式策略与超时）。
        /// </summary>
        public static Task<PageLoadWaitResult> WaitForPageLoadAsync(this IPageLoadWaitService svc, IPage page, PageLoadStrategy strategy, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            if (svc is null) throw new ArgumentNullException(nameof(svc));
            if (page is null) throw new ArgumentNullException(nameof(page));
            var auto = PlaywrightAutoFactory.Wrap(page);
            return svc.WaitForPageLoadAsync(auto, strategy, timeout, cancellationToken);
        }
    }
}
