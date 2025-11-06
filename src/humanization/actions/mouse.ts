/* 中文注释：鼠标动作（基于三次贝塞尔 + 轻度过冲） */
import type { Page, Locator } from "playwright";
import { ensureVisible } from "../viewport.js";
import { jitter } from "../core/randomization.js";
import { sleep } from "../delays.js";
import { planMousePath } from "../plans/mousePlan.js";

export interface MoveOptions {
	steps?: number;
	randomness?: number;
	overshoot?: boolean;
	overshootAmount?: number;
	microJitterPx?: number;
	microJitterCount?: number;
}
export interface ClickOptions {
	button?: "left" | "right" | "middle";
	clickCount?: number;
	delay?: number;
	microJitterPx?: number;
	microJitterCount?: number;
}

export async function moveMouseCubic(page: Page, loc: Locator, opts: MoveOptions = {}) {
	await ensureVisible(loc);
	const box = await loc.boundingBox();
	if (!box) return;
	const to = { x: box.x + box.width / 2, y: box.y + box.height / 2 };
	const from = {
		x: Math.max(0, to.x - 60 + Math.random() * 20),
		y: Math.max(0, to.y - 40 + Math.random() * 20),
	};
	const path = planMousePath(from, to, opts);
	for (const p of path) {
		await page.mouse.move(p.x, p.y, { steps: 1 });
		await sleep(4 + Math.random() * 10);
	}
}

export async function hoverHuman(page: Page, loc: Locator, opts?: MoveOptions) {
	await moveMouseCubic(page, loc, opts);
	await loc.hover();
}

export async function clickHuman(page: Page, loc: Locator, opts: ClickOptions = {}) {
	// 若提供微抖动参数，则在点击前做一次轻微抖动（停留在目标附近）
	await moveMouseCubic(page, loc, {
		microJitterPx: opts.microJitterPx,
		microJitterCount: opts.microJitterCount,
	});
	const delay = jitter(opts.delay ?? 50, 20, 10, 200);
	await loc.click({ button: opts.button ?? "left", clickCount: opts.clickCount ?? 1, delay });
}
