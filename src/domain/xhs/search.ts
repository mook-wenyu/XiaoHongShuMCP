/* 中文注释：小红书搜索流程封装（关闭模态→点击搜索栏→输入→点击搜索图标） */
import type { Page } from "playwright";
import { resolveLocatorResilient } from "../../selectors/index.js";
import { XhsSelectors } from "../../selectors/xhs.js";
import { clickHuman, typeHuman } from "../../humanization/actions.js";
import { closeModalIfOpen } from "./navigation.js";
import { XHS_CONF } from "../../config/xhs.js";

/**
 * 定位搜索输入与提交按钮
 * 优先顺序：
 * - 输入：#search-input.search-input → placeholder="搜索小红书" → .input-box .search-input
 * - 提交：.input-box .input-button → .input-box .search-icon → 按回车兜底
 */
export async function ensureSearchLocators(page: Page): Promise<{ input?: any; submit?: any }> {
	const inputCandidates: any[] = [
		XhsSelectors.searchInput(),
	];
	let input: any | undefined;
	for (const c of inputCandidates) {
		try {
			const loc = await resolveLocatorResilient(page as any, c as any, {
				selectorId: "search-input",
				retryAttempts: 2,
				verifyTimeoutMs: 500,
			});
			input = loc;
			break;
		} catch {}
	}
	const submitCandidates: any[] = [
		XhsSelectors.searchSubmit(),
	];
	let submit: any | undefined;
	for (const c of submitCandidates) {
		try {
			const loc = await resolveLocatorResilient(page as any, c as any, {
				selectorId: "search-submit",
				retryAttempts: 2,
				verifyTimeoutMs: 500,
			});
			submit = loc;
			break;
		} catch {}
	}
	return { input, submit };
}

/**
 * 在小红书首页执行搜索
 * - 关闭模态（若有）
 * - 点击输入框并输入关键词
 * - 点击提交按钮（或回车）
 * - 等待导航到 search_result 页面
 */
async function dumpHtml(page: Page, dirId: string | undefined, tag: string): Promise<string | undefined> {
	try {
		if (!dirId) return undefined;
		const { ensureDir } = await import("../../services/artifacts.js");
		const { join } = await import("node:path");
		const { writeFile } = await import("node:fs/promises");
		const base = join("artifacts", dirId, "html");
		await ensureDir(base);
		const p = join(base, `${tag}-${Date.now()}.html`);
		const html = await page.content();
		await writeFile(p, html, "utf-8");
		return p;
	} catch { return undefined; }
}

export async function searchKeyword(page: Page, keyword: string): Promise<{ ok: boolean; url?: string; verified?: boolean; matchedCount?: number }> {
	// 不强制跳转：先尝试在当前页搜索，找不到再回到“发现”页
	try { await closeModalIfOpen(page); } catch {}

	let { input, submit } = await ensureSearchLocators(page);
	if (!input) {
		const { ensureDiscoverPage } = await import("./navigation.js");
		await ensureDiscoverPage(page);
		({ input, submit } = await ensureSearchLocators(page));
		if (!input) {
			try { await page.goto("https://www.xiaohongshu.com/", { waitUntil: "domcontentloaded" }); } catch {}
			({ input, submit } = await ensureSearchLocators(page));
			if (!input) return { ok: false };
		}
	}

	await clickHuman(page as any, input);
	await typeHuman(input, keyword, { wpm: 220 });

	const waitSearchUrl = async () => {
		try { await page.waitForURL(/\/search_result\?keyword=/, { timeout: XHS_CONF.search.waitUrlMs }); return true; } catch { return false; }
	};
	const waitSearchApi = async (): Promise<{ ok: boolean; count?: number }> => {
		try {
			const resp = await page.waitForResponse((r) => {
				const u = r.url();
				if (!u.includes('/api/sns/web/v1/search/notes')) return false;
				const method = r.request().method();
				if (method === 'GET') return u.includes('keyword=');
				try { const b = r.request().postData() || ''; return b.includes('keyword') || b.includes('%E5%85%B3%E9%94%AE%E8%AF%8D'); } catch { return true; }
			}, { timeout: XHS_CONF.search.waitApiMs });
			const data: any = await resp.json().catch(() => undefined);
			const count = Array.isArray(data?.data?.items) ? data.data.items.length : undefined;
			return { ok: true, count };
		} catch { return { ok: false }; }
	};

	for (let i = 0; i < 3; i++) {
		if (submit && (i === 0 || i === 2)) {
			try { await clickHuman(page as any, submit); } catch {}
		} else {
			try { await page.keyboard.press("Enter"); } catch {}
		}
		const [urlOk, apiOk] = await Promise.all([waitSearchUrl(), waitSearchApi()]);
		if (urlOk || apiOk.ok) return { ok: true, url: page.url(), verified: apiOk.ok, matchedCount: apiOk.count };
		await page.waitForTimeout(300);
	}
	return { ok: false };
}
