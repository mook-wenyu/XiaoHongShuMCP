import { describe, it, expect } from "vitest";
import { planScroll } from "../../src/humanization/plans/scrollPlan.js";
import { planMousePath } from "../../src/humanization/plans/mousePlan.js";

describe("plans", () => {
	it("scroll plan length matches segments and includes pauses", () => {
		const segs = planScroll(1200, {
			segments: 6,
			microPauseChance: 1,
			microPauseMinMs: 50,
			microPauseMaxMs: 50,
			macroPauseEvery: 3,
			macroPauseMinMs: 100,
			macroPauseMaxMs: 100,
		});
		expect(segs.length).toBe(6);
		// 所有 waitMs 都应 >= 基础等待
		expect(segs.every((s) => s.waitMs >= 40)).toBe(true);
		// 确保有宏停顿叠加痕迹（第3、第6段）
		expect(segs[2].waitMs).toBeGreaterThanOrEqual(140);
	});
	it("mouse plan appends micro tremor by default", () => {
		const p = planMousePath({ x: 0, y: 0 }, { x: 100, y: 0 }, {});
		expect(p.length).toBeGreaterThan(30); // 贝塞尔 30+ 微抖点
	});
});
