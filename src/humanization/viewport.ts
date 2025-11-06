/* 中文注释：视口工具（确保元素可见、必要时居中） */
import type { Locator, Page } from "playwright";

/**
 * 确保元素在视口中且可见
 * - 优先使用 Playwright 内置的 scrollIntoViewIfNeeded
 * - 随后等待可见态，默认 10s 超时
 */
export async function ensureVisible(loc: Locator, timeoutMs = 10000) {
	try {
		await loc.scrollIntoViewIfNeeded();
	} catch {}
	await loc.waitFor({ state: "visible", timeout: timeoutMs });
}

/**
 * 将元素居中到视口附近（可选）
 * - 适合长列表中需要稳定点击的元素
 */
export async function centerInViewport(page: Page, loc: Locator) {
	const box = await loc.boundingBox();
	if (!box) return;
	const viewport = page.viewportSize?.();
	// 若无法获取视口尺寸，则退化为 ensureVisible
	if (!viewport) {
		await ensureVisible(loc);
		return;
	}
	const targetX = Math.max(0, box.x + box.width / 2 - viewport.width / 2);
	const targetY = Math.max(0, box.y + box.height / 2 - viewport.height / 2);
	try {
		await page.evaluate(([x, y]) => window.scrollTo(x, y), [targetX, targetY]);
	} catch {}
	await ensureVisible(loc);
}
