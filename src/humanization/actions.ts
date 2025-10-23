/* 中文注释：基于 Playwright 的拟人化动作适配器（不含特定站点选择器） */
import type { Page, Locator } from "playwright";
import { charDelayByWPM } from "./delays.js";
import { ensureVisible } from "./viewport.js";
import { moveMouseCubic, hoverHuman as hoverCubic, clickHuman as clickCubic } from "./actions/mouse.js";
import { scrollHumanized } from "./actions/scroll.js";

// 扩展移动参数：支持过冲（overshoot）与幅度（overshootAmount）
export interface MoveOptions { steps?: number; randomness?: number; overshoot?: boolean; overshootAmount?: number; microJitterPx?: number; microJitterCount?: number }
export async function moveMouseTo(page: Page, loc: Locator, opts: MoveOptions = {}) {
	// 兼容旧签名：映射到 cubic 实现
	await moveMouseCubic(page, loc, { steps: opts.steps, randomness: opts.randomness, overshoot: opts.overshoot, overshootAmount: opts.overshootAmount, microJitterPx: opts.microJitterPx, microJitterCount: opts.microJitterCount });
}

export interface TypeOptions { wpm?: number }
export async function typeHuman(loc: Locator, text: string, opts: TypeOptions = {}) {
	const perChar = charDelayByWPM(opts.wpm ?? 180);
	for (const ch of text.split("") ) { await loc.type(ch, { delay: perChar(ch) }); }
}

export interface ScrollOptions {
  segments?: number;
  jitterPx?: number;
  perSegmentMs?: number;
  // 透传高级参数给滚动计划（保持默认策略不变）
  easing?: any;
  microPauseChance?: number;
  microPauseMinMs?: number;
  microPauseMaxMs?: number;
  macroPauseEvery?: number;
  macroPauseMinMs?: number;
  macroPauseMaxMs?: number;
}
export async function scrollHuman(page: Page, deltaY = 800, opts: ScrollOptions = {}) {
	await scrollHumanized(page, deltaY, opts as any);
}

export async function hoverHuman(page: Page, loc: Locator) { await hoverCubic(page, loc); }
export async function clickHuman(page: Page, loc: Locator) { await clickCubic(page, loc); }
