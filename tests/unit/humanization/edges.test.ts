import { describe, it, expect } from "vitest";
import { planScroll } from "../../../src/humanization/plans/scrollPlan.js";
import { planMousePath } from "../../../src/humanization/plans/mousePlan.js";

// 仅测试纯函数计划层，避免浏览器依赖

describe("humanization edges", () => {
	it("scroll: segments=1 and 30, micro/macro pauses off/on", () => {
		const s1 = planScroll(800, { segments: 1, microPauseChance: 0, macroPauseEvery: 0 });
		expect(s1.length).toBe(1);
		// 关闭微/宏停顿后等待仍应有基础等待（>0）
		expect(s1[0].waitMs).toBeGreaterThan(0);

		const s30 = planScroll(3000, {
			segments: 30,
			microPauseChance: 1,
			microPauseMinMs: 60,
			microPauseMaxMs: 60,
			macroPauseEvery: 4,
			macroPauseMinMs: 150,
			macroPauseMaxMs: 150,
		});
		expect(s30.length).toBe(30);
		// 至少应有一些段体现微停顿或宏停顿（>= 基础等待 + 60 或 +150）
		const hasMicro = s30.some((s) => s.waitMs >= 100); // 基础~80-200，叠加后更大
		expect(hasMicro).toBe(true);
	});

	it("scroll: extreme deltaY handled", () => {
		const sNeg = planScroll(-5000, { segments: 5 });
		expect(sNeg.length).toBe(5);
		const sZero = planScroll(0, { segments: 3 });
		expect(sZero.length).toBe(3);
	});

	it("mouse: micro jitter default on; can be disabled via counts/px", () => {
		const withJitter = planMousePath({ x: 0, y: 0 }, { x: 200, y: 0 }, {});
		const noJitter = planMousePath(
			{ x: 0, y: 0 },
			{ x: 200, y: 0 },
			{ microJitterPx: 0, microJitterCount: 0 },
		);
		expect(withJitter.length).toBeGreaterThan(noJitter.length);
	});

	it("mouse: overshoot on/off affects tail points", () => {
		const base = planMousePath({ x: 0, y: 0 }, { x: 100, y: 0 }, { overshoot: false });
		const over = planMousePath(
			{ x: 0, y: 0 },
			{ x: 100, y: 0 },
			{ overshoot: true, overshootAmount: 12 },
		);
		expect(over.length).toBeGreaterThanOrEqual(base.length);
	});
});
