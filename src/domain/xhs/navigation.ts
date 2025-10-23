/* 中文注释：小红书导航/检测与关键词选择的页面级策略集合
 * 目标：
 * 1) 页面类型识别（主页/发现/搜索/笔记模态/未知）
 * 2) 保证处于发现页（不在则点击“发现”或直达 URL）
 * 3) 模态关闭（Esc->关闭按钮->遮罩/背景点击 三级兜底）
 * 4) 滚动+关键词匹配命中后点击打开笔记（模态）
 *
 * 设计说明：
 * - 复用现有工具：resolveLocatorAsync、clickHuman、scrollHuman
 * - 采用 URL + 轻量 DOM 探针，避免过于脆弱的选择器依赖
 * - 不实现任何“反检测/指纹”的逻辑，遵循模块边界
 */

import type { Page } from "playwright";
import { resolveLocatorResilient } from "../../selectors/index.js";
import { XhsSelectors } from "../../selectors/xhs.js";
import { clickHuman, scrollHuman } from "../../humanization/actions.js";
import { XHS_CONF } from "../../config/xhs.js";

export enum PageType {
	ExploreHome = "ExploreHome",
	Discover = "Discover",
	Search = "Search",
	NoteModal = "NoteModal",
	Unknown = "Unknown",
}

/** 检测页面类型（URL 优先，必要时以轻量 DOM 探针兜底） */
export async function detectPageType(page: Page): Promise<PageType> {
	const url = page.url();
	try {
		// 先判 URL
		if (url.startsWith("https://www.xiaohongshu.com/search_result?keyword=")) return PageType.Search;
		if (url.startsWith("https://www.xiaohongshu.com/explore?")) {
			if (url.includes("channel_id=homefeed_recommend")) return PageType.Discover;
			return PageType.ExploreHome;
		}
		if (url === "https://www.xiaohongshu.com/explore") return PageType.ExploreHome;

		// 兜底：模态探针（role=dialog/aria-modal 或小红书特征类名 note-detail-mask/note-container）
		const modalCount = await page.locator('[role="dialog"], [aria-modal="true"], .note-detail-mask, #noteContainer, .note-container').count();
		if (modalCount > 0) return PageType.NoteModal;
	} catch {}
	return PageType.Unknown;
}

/** 保证进入发现页：优先点击“发现”，失败则直达 URL */
export async function ensureDiscoverPage(page: Page): Promise<void> {
	const waitHomefeed = async (): Promise<{ ok: boolean; count?: number }> => {
		try {
			const resp = await page.waitForResponse(
				(r) => r.url().includes('/api/sns/web/v1/homefeed'),
				{ timeout: XHS_CONF.feed.waitApiMs }
			);
			const data: any = await resp.json().catch(() => undefined);
			const count = Array.isArray(data?.data?.items) ? data.data.items.length : undefined;
			return { ok: true, count };
		} catch { return { ok: false }; }
	};
	const type = await detectPageType(page);
	if (type === PageType.Discover) return;

	// 先确保在探索入口，再点击“发现”
	if (type !== PageType.ExploreHome) {
		await page.goto("https://www.xiaohongshu.com/explore", { waitUntil: "domcontentloaded" });
	}

	try {
		const candidates: any[] = [
			XhsSelectors.navDiscover(),
		];
		let clicked = false;
		for (const c of candidates) {
			try {
				const loc = await resolveLocatorResilient(page as any, c as any, {
					selectorId: "nav-discover",
					retryAttempts: 2,
					verifyTimeoutMs: 3000,
				});
				await clickHuman(page as any, loc);
				await page.waitForLoadState("domcontentloaded");
				clicked = true;
				break;
			} catch {}
		}
		if (clicked) { await waitHomefeed().catch(() => ({ ok: false })); }
	} catch {}

	// 再次判定，不成功则直达发现页 URL
	let after = await detectPageType(page);
	if (after !== PageType.Discover) {
		await page.goto("https://www.xiaohongshu.com/explore?channel_id=homefeed_recommend", { waitUntil: "domcontentloaded" });
		await waitHomefeed().catch(() => ({ ok: false }));
		after = await detectPageType(page);
	}
}

/**
 * 关闭笔记详情模态（若存在），按 Esc → 关闭按钮 → 遮罩 点击顺序兜底。
 * 返回是否执行了关闭动作。
 */
export async function closeModalIfOpen(page: Page): Promise<boolean> {
	const isOpen = async () => (await page.locator('[role="dialog"], [aria-modal="true"], .note-detail-mask, #noteContainer, .note-container').count()) > 0;
	if (!(await isOpen())) return false;

	// 1) Esc
	try {
		await page.keyboard.press("Escape");
		await page.waitForTimeout(200);
		if (!(await isOpen())) return true;
	} catch {}

	// 2) 找关闭按钮（aria-label / 文本 / data-testid / 常见 class / 小红书特征 close-* / 图标 use#close）
	const closeSelectors = [
		'button[aria-label*="关闭" i]',
		'button:has-text("关闭")',
		'[data-testid*="close" i]',
		'[class*="close" i]',
		'.close-mask-dark',
		'.close-box',
		'svg:has(use[xlink\\:href="#close"])'
	];
	for (const sel of closeSelectors) {
		try {
			const loc = page.locator(sel).first();
			if (await loc.isVisible()) { await loc.click({ timeout: 500 }); }
			await page.waitForTimeout(200);
			if (!(await isOpen())) return true;
		} catch {}
	}

	// 3) 点击遮罩/背景（包含 note-detail-mask）
	const backdropSelectors = [
		'.note-detail-mask',
		'[class*="mask" i]',
		'[class*="backdrop" i]',
		'[class*="overlay" i]'
	];
	for (const sel of backdropSelectors) {
		try {
			const bd = page.locator(sel).first();
			if (await bd.isVisible()) { await bd.click({ timeout: 500 }); }
			await page.waitForTimeout(200);
			if (!(await isOpen())) return true;
		} catch {}
	}

	// 4) 兜底：点击页面左上角空白（可能被拦截，失败忽略）
	try { await page.mouse.click(10, 10); await page.waitForTimeout(200); } catch {}
	return !(await isOpen());
}

export interface FindOpenOptions {
	maxScrolls?: number; // 最大滚动轮次
	scrollStep?: number; // 每次滚动像素
	settleMs?: number; // 滚动后等待渲染时间
	preferApiAnchors?: boolean; // 搜索页优先用 API 返回的 note id 构造锚点
	useApiAfterScroll?: boolean; // 每次滚动后等待对应 API（search/notes 或 homefeed）
}

/**
 * 在发现页按关键词查找并打开第一条匹配的笔记（点击卡片触发模态）。
 * 策略：可视区域扫描 → 不命中则滚动 → 重试，直到命中或达到轮次数。
 */
export async function findAndOpenNoteByKeywords(
	page: Page,
	keywords: string[],
	opts: FindOpenOptions = {}
): Promise<{ ok: boolean; matched?: string }> { 
	const waitSearchNotes = async (): Promise<{ ok: boolean; items?: Array<{ id?: string; title?: string }> }> => {
		try {
			const resp = await page.waitForResponse(
				(r) => r.url().includes('/api/sns/web/v1/search/notes'),
				{ timeout: XHS_CONF.search.waitApiMs }
			);
			const data: any = await resp.json().catch(() => undefined);
			const raw = Array.isArray(data?.data?.items) ? data.data.items : [];
			const items = raw.map((it: any) => ({ id: it?.id, title: it?.note_card?.display_title })).filter((x: any) => x.id || x.title);
			return { ok: true, items };
		} catch { return { ok: false }; }
	};
	const maxScrolls = Math.max(1, opts.maxScrolls ?? XHS_CONF.scroll.maxScrolls);
	const step = Math.max(200, opts.scrollStep ?? XHS_CONF.scroll.step);
	const settle = Math.max(50, opts.settleMs ?? XHS_CONF.scroll.settleMs);
	const useApiAfterScroll = opts.useApiAfterScroll ?? false;

	// 搜索页优先匹配 note 详情锚点；其他页面再放宽
	const pageType = await detectPageType(page);
	// 使用集中映射的锚点选择器（避免在此处硬编码 URL 片段）
	const anchorSel = (XhsSelectors.noteAnchor().alternatives ?? [])
		.map(h => h.selector)
		.filter(Boolean)
		.join(", ") || 'a[href]';

	// 搜索页：若允许，先用 API 返回的 note id 精确定位
	if (pageType === PageType.Search && opts.preferApiAnchors) {
		const r = await waitSearchNotes();
		if (r.ok && r.items && r.items.length) {
			for (const item of r.items.slice(0, 12)) {
				if (!item?.id && !item?.title) continue;
				let clicked = false;
				// 1) 以 note id 定位锚点
				if (item.id) {
					try {
						const byId = page.locator(`a[href*="/discovery/item/${item.id}"], a[href*="/explore/${item.id}"]`).first();
						if (await byId.count() > 0) { await clickHuman(page as any, byId); clicked = true; }
					} catch {}
				}
				// 2) 退化：以标题文本匹配
				if (!clicked && item.title) {
					try {
						const byTitle = page.locator(anchorSel).filter({ hasText: item.title }).first();
						if (await byTitle.count() > 0) { await clickHuman(page as any, byTitle); clicked = true; }
					} catch {}
				}
				if (clicked) {
					try { await page.waitForSelector('[role="dialog"], [aria-modal="true"], .note-detail-mask, #noteContainer, .note-container', { timeout: 3000 }); } catch {}
					return { ok: true, matched: item.title || item.id };
				}
			}
		}
	}

	for (let i = 0; i < maxScrolls; i++) {
		for (const k of keywords) {
			// 1) 优先锚点
			let clicked = false;
			try {
				const loc = page.locator(anchorSel).filter({ hasText: k }).first();
				if (await loc.count() > 0) { await clickHuman(page as any, loc); clicked = true; }
			} catch {}
			// 2) 退化到容器（article/div），有文本就尝试点击
			if (!clicked) {
				try {
					const box = page.locator('article, div').filter({ hasText: k }).first();
					if (await box.count() > 0) { await clickHuman(page as any, box); clicked = true; }
				} catch {}
			}
			if (clicked) {
				try {
					await page.waitForSelector('[role="dialog"], [aria-modal="true"], .note-detail-mask, #noteContainer, .note-container', { timeout: 3000 });
				} catch { await page.waitForLoadState('domcontentloaded').catch(() => {}); }
				return { ok: true, matched: k };
			}
		}
		await scrollHuman(page as any, step);
		// 滚动后按页面类型监听对应 API，确保新批次到达
		if (useApiAfterScroll) {
			if (pageType === PageType.Discover || pageType === PageType.ExploreHome) {
				try { await page.waitForResponse((r) => r.url().includes('/api/sns/web/v1/homefeed'), { timeout: XHS_CONF.feed.waitApiMs }); } catch {}
			} else if (pageType === PageType.Search) {
				try { await page.waitForResponse((r) => r.url().includes('/api/sns/web/v1/search/notes'), { timeout: XHS_CONF.search.waitApiMs }); } catch {}
			}
		}
		await page.waitForTimeout(settle);
	}
	return { ok: false };
}
