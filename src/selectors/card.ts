import type { Page } from "playwright";
// 外部 JSON 配置已移除，此处直接使用内置容器/锚点默认规则

export type CardInfo = {
	index: number;
	noteId?: string;
	href?: string;
	/** 用于匹配的最终标题：优先可见标题，其次封面图片 alt，最后用整卡文本截断 */
	title: string;
	/** 原始可见标题（未兜底，仅诊断/测试使用） */
	titleRaw?: string;
	/** 封面图片的 alt 文本（若存在，仅诊断/测试使用） */
	coverAlt?: string;
	/** 标题来源：text | alt | fallback（仅诊断/测试使用） */
	titleSource?: "text" | "alt" | "fallback";
	text: string;
	y: number;
	h?: number;
	y2?: number;
};

export async function resolveContainerSelector(_page: Page): Promise<string> {
	// 统一默认：常见卡片容器选择器集合（可按需要扩展）
	const sel = "section.note-item, .note-item, .List-item, article, .Card";
	return sel;
}

// 已弃用：ID 精确锚点解析。人类点击仅允许封面或标题，不再直接使用 ID 锚点。
// 保留空实现以防外部误用导出（若仍有导入，则抛出错误以尽早暴露问题）。
export async function resolveAnchorSelectorForId(_page: Page, _id: string): Promise<string> {
	throw new Error(
		"resolveAnchorSelectorForId is removed: use cover/title clickable within card container",
	);
}

export async function collectVisibleCards(page: Page, containerSel: string): Promise<CardInfo[]> {
	try {
		const results = await page.locator(containerSel).evaluateAll((els: Element[]) => {
			const h = window.innerHeight || 0;
			const out: Array<{
				index: number;
				noteId?: string;
				href?: string;
				title: string;
				titleRaw?: string;
				coverAlt?: string;
				titleSource?: "text" | "alt" | "fallback";
				text: string;
				y: number;
				h?: number;
				y2?: number;
			}> = [];
			els.forEach((el, index) => {
				const rect = (el as HTMLElement).getBoundingClientRect?.();
				const visible =
					!!rect &&
					rect.height > 30 &&
					rect.bottom > 0 &&
					rect.top < h * 0.98 &&
					(el as HTMLElement).offsetParent !== null;
				if (!visible) return;
				const container = el as HTMLElement;
				const full = (container.textContent || "").trim().replace(/\s+/g, " ");
				// 标题提取：先取可见标题，再取封面图片的 alt 兜底，最后退回整卡文本截断
				let titleText = "";
				const titleEl = container.querySelector("a.title, .footer .title, .title, h1, h2");
				if (titleEl) titleText = (titleEl.textContent || "").trim();

				// 提取封面图片 alt（多层级优先级）
				let coverAlt = "";
				const findAlt = (...sels: string[]): string => {
					for (const s of sels) {
						const img = container.querySelector(s) as HTMLImageElement | null;
						const altAttr =
							img && typeof (img as any).getAttribute === "function"
								? (img as any).getAttribute("alt")
								: "";
						const alt = (altAttr || "").trim();
						if (alt) return alt;
					}
					return "";
				};
				coverAlt =
					findAlt("a.cover img[alt]") ||
					findAlt(".cover img[alt]") ||
					// 优先在指向详情的链接内部寻找图片 alt
					findAlt(
						"a[href*='/explore/'] img[alt]",
						"a[href*='/search_result/'] img[alt]",
						"a[href*='/discovery/item/'] img[alt]",
						"a[href*='/question/'] img[alt]",
						"a[href*='/p/'] img[alt]",
						"a[href*='/zvideo/'] img[alt]",
					) ||
					// 最后在容器内任意 img 上兜底
					findAlt("img[alt]");

				let titleSource: "text" | "alt" | "fallback" = "fallback";
				let title = titleText;
				if (title && title.length > 0) {
					titleSource = "text";
				} else if (coverAlt && coverAlt.length > 0) {
					title = coverAlt;
					titleSource = "alt";
				} else {
					title = full.slice(0, 120);
					titleSource = "fallback";
				}

				let noteId: string | undefined;
				const hrefEl = container.querySelector(
					"a[href*='/search_result/'], a[href*='/discovery/item/'], a[href*='/explore/'], a[href*='/question/'], a[href*='/p/'], a[href*='/zvideo/']",
				) as HTMLAnchorElement | null;
				const href = hrefEl?.getAttribute("href") || "";
				const m = href.match(/(?:explore|discovery\/item|search_result|question|p|zvideo)\/(\w+)/);
				if (m && m[1]) noteId = m[1];
				out.push({
					index,
					noteId,
					href: href || undefined,
					title,
					titleRaw: titleText || undefined,
					coverAlt: coverAlt || undefined,
					titleSource,
					text: full,
					y: rect.top,
					h: rect.height,
					y2: rect.bottom,
				});
			});
			out.sort((a, b) => a.y - b.y);
			return out;
		});
		return Array.isArray(results) ? results : [];
	} catch {
		return [];
	}
}
