/* 中文注释：基于 Playwright 的拟人化动作适配器（不含特定站点选择器） */
import type { Page, Locator } from "playwright";
import { charDelayByWPM } from "./delays.js";
import { moveMouseCubic, hoverHuman as hoverCubic, clickHuman as clickCubic } from "./actions/mouse.js";
import { scrollHumanized } from "./actions/scroll.js";
import { pressHuman } from "./actions/keyboard.js";

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

// 拟人化清空输入：优先 Select-All + Backspace，必要时三击选中 + Delete；
// 仍不空则退化为多次 Backspace（次数基于当前值长度，含节律抖动）。
export async function clearInputHuman(page: Page, loc: Locator) {
  // 确保 focus
  try { await loc.click({ delay: 30 + Math.random() * 60 }); } catch {}
  const read = async () => {
    try { return await loc.inputValue(); } catch { return ""; }
  };
  let value = await read();
  if (!value || value.length === 0) return;

  // 策略 1：Ctrl/Meta + A，Backspace
  try {
    await pressHuman(page, 'Control+A', 60);
    await pressHuman(page, 'Backspace', 60);
  } catch {}
  await page.waitForTimeout(40 + Math.random() * 80);
  value = await read();
  if (!value || value.length === 0) return;

  // 兼容 macOS：Meta+A
  try {
    await pressHuman(page, 'Meta+A', 60);
    await pressHuman(page, 'Backspace', 60);
  } catch {}
  await page.waitForTimeout(30 + Math.random() * 60);
  value = await read();
  if (!value || value.length === 0) return;

  // 策略 2：三击选中 → Delete
  try {
    await loc.click({ clickCount: 3, delay: 20 + Math.random() * 40 });
    await pressHuman(page, 'Delete', 60);
  } catch {}
  await page.waitForTimeout(30 + Math.random() * 60);
  value = await read();
  if (!value || value.length === 0) return;

  // 策略 3：逐字符 Backspace（上限 80 次，避免长文本超时）
  const n = Math.min(80, value.length);
  for (let i = 0; i < n; i++) {
    await pressHuman(page, 'Backspace', 40 + Math.random() * 40);
    if (i % 6 === 0) await page.waitForTimeout(20 + Math.random() * 40);
  }
}
