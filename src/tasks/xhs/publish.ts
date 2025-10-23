/* 中文注释：发布草稿（通用步骤实现，不内置站点选择器）。
 * payload: {
 *   url?: string,
 *   images?: string[],
 *   title?: string,
 *   content?: string,
 *   selectorMap?: { upload?: any; title?: any; content?: any; submit?: any },
 *   submit?: boolean
 * }
 */
import type { BrowserContext } from "playwright";
import { resolveLocatorAsync } from "../../selectors/index.js";
import { clickHuman, typeHuman } from "../../humanization/actions.js";

export async function publishDraft(ctx: BrowserContext, dirId: string, payload: any) {
	const page = await ctx.newPage();
	try {
		if (payload?.url) await page.goto(payload.url, { waitUntil: "domcontentloaded" });
		const sm = payload?.selectorMap || {};
		if (sm.upload && Array.isArray(payload?.images) && payload.images.length) {
			const up = await resolveLocatorAsync(page, sm.upload);
			await up.setInputFiles(payload.images);
		}
		if (sm.title && payload?.title) {
			const loc = await resolveLocatorAsync(page, sm.title);
			await clickHuman(page, loc);
			await typeHuman(loc, payload.title);
		}
		if (sm.content && payload?.content) {
			const loc = await resolveLocatorAsync(page, sm.content);
			await clickHuman(page, loc);
			await typeHuman(loc, payload.content);
		}
		if (payload?.submit && sm.submit) {
			const btn = await resolveLocatorAsync(page, sm.submit);
			await clickHuman(page, btn);
		}
		return { ok: true };
	} finally {
		await page.close();
	}
}
