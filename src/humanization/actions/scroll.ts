/* 中文注释：滚动动作（分段 + 抖动 + 可选缓动节律） */
import type { Page } from "playwright"
import { sleep } from "../delays.js"
import type { EasingFunction } from "../core/timing.js"
import { easeOut } from "../core/timing.js"
import { planScroll } from "../plans/scrollPlan.js"
// 扩展：默认引入人类“停顿式”滚动（微停顿/宏停顿 + 非线性节律）
export interface ScrollOptions {
  segments?: number;            // 分段数（1-30）
  jitterPx?: number;            // 像素级抖动（每段增量的微扰）
  perSegmentMs?: number;        // 每段基础等待（若未给则使用带随机的经验值）
  easing?: EasingFunction;      // 节律（默认 easeOut）
  microPauseChance?: number;    // 每段结束后进行微停顿的概率（0..1，默认≈0.25）
  microPauseMinMs?: number;     // 微停顿最小毫秒数（默认≈60）
  microPauseMaxMs?: number;     // 微停顿最大毫秒数（默认≈160）
  macroPauseEvery?: number;     // 每隔 N 段插入一次“宏停顿”（默认 4；=0 关闭）
  macroPauseMinMs?: number;     // 宏停顿最小毫秒数（默认≈120）
  macroPauseMaxMs?: number;     // 宏停顿最大毫秒数（默认≈260）
}

export async function scrollHumanized(page: Page, deltaY = 800, opts: ScrollOptions = {}) {
  const segments = Math.max(1, Math.min(30, Math.floor(opts.segments ?? 6)))
  const easing = opts.easing ?? easeOut

  // 默认参数：微/宏停顿均开启；可通过将概率或最大值设为 0 显式关闭
  const microChance = Math.max(0, Math.min(1, opts.microPauseChance ?? 0.25))
  const microMin = Math.max(0, opts.microPauseMinMs ?? 60)
  const microMax = Math.max(microMin, opts.microPauseMaxMs ?? 160)
  const macroEvery = Math.max(0, Math.floor(opts.macroPauseEvery ?? 4))
  const macroMin = Math.max(0, opts.macroPauseMinMs ?? 120)
  const macroMax = Math.max(macroMin, opts.macroPauseMaxMs ?? 260)

  const plan = planScroll(deltaY, {
    segments: segments,
    jitterPx: opts.jitterPx,
    perSegmentMs: opts.perSegmentMs,
    easingName: undefined, // 由 planScroll 内部默认 easeOut；保持行为一致
    microPauseChance: opts.microPauseChance,
    microPauseMinMs: opts.microPauseMinMs,
    microPauseMaxMs: opts.microPauseMaxMs,
    macroPauseEvery: opts.macroPauseEvery,
    macroPauseMinMs: opts.macroPauseMinMs,
    macroPauseMaxMs: opts.macroPauseMaxMs,
  })
  for (const seg of plan) {
    await page.mouse.wheel(0, seg.delta)
    await sleep(seg.waitMs)
  }
}
