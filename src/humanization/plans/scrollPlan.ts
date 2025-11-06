/* 中文注释：滚动计划（纯函数）生成器，将停顿与抖动、节律固化为序列 */
import type { ScrollPlanOptions, ScrollSegment } from "../types.js";
import { easeIn, easeOut, easeInOutQuad, easeInOutCubic, linear } from "../core/timing.js";

function easingByName(name?: ScrollPlanOptions["easingName"]) {
	switch (name) {
		case "easeIn":
			return easeIn;
		case "easeOut":
			return easeOut;
		case "easeInOutQuad":
			return easeInOutQuad;
		case "easeInOutCubic":
			return easeInOutCubic;
		default:
			return linear;
	}
}

export function planScroll(deltaY: number, opts: ScrollPlanOptions = {}): ScrollSegment[] {
	const segments = Math.max(1, Math.min(30, Math.floor(opts.segments ?? 6)));
	const easing = easingByName(opts.easingName ?? "easeOut");
	const jitterPx = opts.jitterPx ?? 20;

	const microChance = Math.max(0, Math.min(1, opts.microPauseChance ?? 0.25));
	const microMin = Math.max(0, opts.microPauseMinMs ?? 60);
	const microMax = Math.max(microMin, opts.microPauseMaxMs ?? 160);
	const macroEvery = Math.max(0, Math.floor(opts.macroPauseEvery ?? 4));
	const macroMin = Math.max(0, opts.macroPauseMinMs ?? 120);
	const macroMax = Math.max(macroMin, opts.macroPauseMaxMs ?? 260);

	const segs: ScrollSegment[] = [];
	for (let i = 0; i < segments; i++) {
		const t = segments <= 1 ? 1 : i / (segments - 1);
		const factor = easing(t);
		const base = deltaY / segments;
		const jitter = (Math.random() * 2 - 1) * jitterPx;
		const delta = base + jitter * (0.5 + 0.5 * factor);

		const baseWaitRaw = opts.perSegmentMs ?? 80 + Math.random() * 120;
		const baseWait = Math.max(40, baseWaitRaw * (0.9 + Math.random() * 0.2));
		let wait = baseWait;

		if (microChance > 0 && microMax > 0 && Math.random() < microChance) {
			const micro = microMin + Math.random() * (microMax - microMin);
			wait += micro;
		}
		const atBoundary = (i + 1) % (macroEvery || Infinity) === 0;
		if (macroEvery > 0 && atBoundary) {
			const macro = macroMin + Math.random() * (macroMax - macroMin);
			wait += macro;
		}
		segs.push({ delta, waitMs: wait });
	}
	return segs;
}
