/* 中文注释：鼠标移动/点击计划（纯函数），便于单测与重用 */
import { makeCurvePath } from "../core/curves.js";
import { microJitterPoints } from "../core/randomization.js";
import type { Point, MouseMoveOptions } from "../types.js";

export function planMousePath(from: Point, to: Point, opts: MouseMoveOptions = {}): Point[] {
	const path = makeCurvePath(from, to, {
		steps: opts.steps ?? 30,
		randomness: opts.randomness ?? 0.2,
		overshoot: opts.overshoot ?? (true as any),
		overshootAmount: opts.overshootAmount ?? (10 as any),
	} as any);
	const amp = Math.max(0, opts.microJitterPx ?? 0.6);
	const cnt = Math.max(0, opts.microJitterCount ?? 4);
	if (amp > 0 && cnt > 0) {
		const tremors = microJitterPoints(to, amp, cnt, 0.85);
		return [...path, ...tremors];
	}
	return path;
}
