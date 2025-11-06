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
	const { healthMonitor } = await import("../../selectors/health.js");
	const isTest = String(process.env.NODE_ENV).toLowerCase() === "test";
	const inputCandidates: any[] = [
		XhsSelectors.searchInput(),
		{ role: "textbox" },
		{ selector: "#search-input.search-input" },
		{ selector: ".input-box input.search-input" },
	];
	let input: any | undefined;
	let t0 = Date.now();
	for (let idx = 0; idx < inputCandidates.length; idx++) {
		const c = inputCandidates[idx];
		try {
			const loc = await resolveLocatorResilient(page as any, c as any, {
				selectorId: "search-input",
				retryAttempts: isTest ? 1 : 2,
				verifyTimeoutMs: isTest ? 80 : 500,
				skipHealthMonitor: true, // 由本函数统一记录一次健康度
			});
			input = loc;
			break;
		} catch {}
	}
	// 统一记录一次健康度
	try { healthMonitor.record("search-input", !!input, Date.now() - t0); } catch {}

	const submitCandidates: any[] = [
		XhsSelectors.searchSubmit(),
		{ role: "button" },
		{ selector: ".input-box .input-button" },
		{ selector: ".input-box .search-icon" },
	];
	let submit: any | undefined;
	t0 = Date.now();
	for (let idx = 0; idx < submitCandidates.length; idx++) {
		const c = submitCandidates[idx];
		try {
			const loc = await resolveLocatorResilient(page as any, c as any, {
				selectorId: "search-submit",
				retryAttempts: isTest ? 1 : 2,
				verifyTimeoutMs: isTest ? 80 : 500,
				skipHealthMonitor: true,
			});
			submit = loc;
			break;
		} catch {}
	}
	try { healthMonitor.record("search-submit", !!submit, Date.now() - t0); } catch {}
	return { input, submit };
}

// 旧版调试函数（dumpHtml）已移除，避免未使用警告与不必要的磁盘写入

export async function searchKeyword(page: Page, keyword: string): Promise<{ ok: boolean; url?: string; verified?: boolean; matchedCount?: number }> {
	// 不强制跳转：先尝试在当前页搜索，找不到再回到“发现”页
	try { await closeModalIfOpen(page); } catch {}

	const isTest = String(process.env.NODE_ENV).toLowerCase() === "test";
	let { input, submit } = await ensureSearchLocators(page);
	if (!input) {
		if (isTest) return { ok: false };
		const { ensureDiscoverPage } = await import("./navigation.js");
		await ensureDiscoverPage(page);
		({ input, submit } = await ensureSearchLocators(page));
		if (!input) {
			try { await page.goto("https://www.xiaohongshu.com/", { waitUntil: "domcontentloaded" }); } catch {}
			({ input, submit } = await ensureSearchLocators(page));
			if (!input) return { ok: false };
		}
	}

	// 点击输入框并清空已有内容（拟人化）
	await clickHuman(page as any, input);
	try {
		const { clearInputHuman } = await import("../../humanization/actions.js");
		await clearInputHuman(page as any, input);
	} catch {}

	// 输入关键词（拟人化节律）
	await typeHuman(input, keyword, { wpm: 220 });

	const waitSearchUrl = async () => {
		try { await page.waitForURL(/\/search_result\?keyword=/, { timeout: XHS_CONF.search.waitUrlMs }); return true; } catch { return false; }
	};
	// 统一监听：先挂 search.notes，再触发提交；与 URL 变化并行等待
	const waitSearchApiSoft = async (): Promise<{ ok: boolean; count?: number }> => {
		const { waitSearchNotes } = await import("./netwatch.js");
		const w = waitSearchNotes(page, XHS_CONF.search.waitApiMs);
		return w.promise.then(r => ({ ok: r.ok, count: Array.isArray((r as any).data?.items) ? (r as any).data.items.length : undefined })).catch(() => ({ ok: false }));
	};

	for (let i = 0; i < 3; i++) {
		const apiP = waitSearchApiSoft();
		if (submit && (i === 0 || i === 2)) {
			try { await clickHuman(page as any, submit); } catch {}
		} else {
			try { await page.keyboard.press("Enter"); } catch {}
		}
		const [urlOk, apiOk] = await Promise.all([waitSearchUrl(), apiP]);
		if (urlOk || apiOk.ok) {
			const matchedCount = apiOk.count ?? 0; // 确保返回字段存在
			return { ok: true, url: page.url(), verified: apiOk.ok, matchedCount };
		}
		await page.waitForTimeout(300);
	}
	return { ok: false };
}
