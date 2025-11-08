import type { Page, Locator } from "playwright";

// 在卡片容器内解析“最可点击”的元素（优先带 href 的封面/标题锚点）
// - 当提供 noteId 时，优先在 href 中包含该 id 的锚点
// - 否则按允许前缀的 href 优先级降序
export async function resolveClickableInCard(
	page: Page,
	card: Locator,
	opts?: { id?: string },
): Promise<Locator> {
	const id = opts?.id?.trim();
	const allow = ["/search_result/", "/explore/", "/discovery/item/"];

	const buildIdSelector = (prefix: string) =>
		`a[href*="${prefix}${id}"]:visible, a[class*="cover" i][href*="${prefix}${id}"]:visible`;

	const generalSelector = allow
		.map((p) => `a[href*="${p}"]:visible, a[class*="cover" i][href*="${p}"]:visible`)
		.join(", ");

	// 1) 按 noteId 精确命中
	if (id) {
		for (const p of allow) {
			const loc = card.locator(buildIdSelector(p)).first();
			if ((await loc.count()) > 0) return loc;
		}
	}

	// 2) 无 id：按 href 前缀优先
	const locHref = card.locator(generalSelector).first();
	if ((await locHref.count()) > 0) return locHref;

	// 3) 退化：标题或封面可见元素
	return card
		.locator(
			"a.title:visible, .footer a.title:visible, a.cover:visible, a[class*=\"cover\" i]:visible",
		)
		.first();
}
