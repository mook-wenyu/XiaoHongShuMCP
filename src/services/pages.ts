/* 中文注释：同一 Context 下的多页管理工具（轻量，基于 pages() 快照）
 * 说明：我们按 index 对页面进行选择与操作；index 来自 context.pages() 的顺序（打开顺序）。
 * 由于每次 action 都是短连接附着到同一 Roxy 窗口，index 在大多数情况下可稳定代表页面。
 */
import type { BrowserContext, Page } from "playwright";

export interface PageSelector {
	pageIndex?: number;
}

export function listPages(ctx: BrowserContext) {
	return ctx.pages().map((p, i) => ({ index: i, url: p.url(), isClosed: p.isClosed() }));
}

export async function ensurePage(ctx: BrowserContext, sel?: PageSelector): Promise<Page> {
	const pages = ctx.pages();
	if (pages.length === 0) return ctx.newPage();
	// 优先选择未关闭的页面
	const alive = pages.filter((p) => !p.isClosed());
	if (alive.length === 0) return ctx.newPage();
	if (sel?.pageIndex != null) {
		const idx = Math.max(0, Math.min(alive.length - 1, sel.pageIndex));
		return alive[idx];
	}
	// 默认取最后一个“未关闭”的页面（通常为最近使用的）
	return alive[alive.length - 1];
}

export async function newPage(ctx: BrowserContext, url?: string) {
	const p = await ctx.newPage();
	if (url) await p.goto(url, { waitUntil: "domcontentloaded" });
	return p;
}

export async function closePage(ctx: BrowserContext, pageIndex?: number) {
	const pages = ctx.pages();
	if (pages.length === 0) return false;
	const idx =
		pageIndex != null ? Math.max(0, Math.min(pages.length - 1, pageIndex)) : pages.length - 1;
	await pages[idx].close();
	return true;
}
