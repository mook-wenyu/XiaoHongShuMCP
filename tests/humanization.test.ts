import { describe, it, expect } from "vitest";
import { makeCurvePath, makeLinearPath } from "../src/humanization/curves.js";
import { charDelayByWPM, jitter } from "../src/humanization/delays.js";

describe("humanization", () => {
	it("curve path returns many points and starts/ends near endpoints", () => {
		const p = makeCurvePath({ x: 0, y: 0 }, { x: 100, y: 0 }, { steps: 30, randomness: 0.2 });
		expect(p.length).toBeGreaterThan(10);
		expect(Math.abs(p[0].x - 0)).toBeLessThan(5);
		expect(Math.abs(p.at(-1)!.x - 100)).toBeLessThan(5);
	});
	it("linear path has constant step count", () => {
		const p = makeLinearPath({ x: 0, y: 0 }, { x: 10, y: 10 }, 10);
		expect(p.length).toBe(11);
	});
	it("char delay scales with wpm", () => {
		const fast = charDelayByWPM(240, 0);
		const slow = charDelayByWPM(120, 0);
		expect(slow("a")).toBeGreaterThan(fast("a"));
		expect(jitter(100)).toBeGreaterThan(0);
	});
});
