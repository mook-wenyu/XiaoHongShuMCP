import { describe, it, expect } from "vitest";
import {
	easeInOutCubic,
	easeIn,
	easeOut,
	linear,
} from "../../../../src/humanization/core/timing.js";

describe("timing easing", () => {
	it("boundaries", () => {
		expect(easeInOutCubic(0)).toBe(0);
		expect(easeInOutCubic(1)).toBe(1);
		expect(linear(0.5)).toBe(0.5);
	});
	it("monotonic", () => {
		let prev = 0;
		for (let t = 0; t <= 1; t += 0.01) {
			const v = easeInOutCubic(t);
			expect(v).toBeGreaterThanOrEqual(prev);
			prev = v;
		}
	});
	it("shapes", () => {
		expect(easeIn(0.25)).toBeLessThan(0.25);
		expect(easeOut(0.75)).toBeGreaterThan(0.75);
		expect(easeInOutCubic(0.25)).toBeLessThan(0.25);
		expect(easeInOutCubic(0.75)).toBeGreaterThan(0.75);
	});
});
