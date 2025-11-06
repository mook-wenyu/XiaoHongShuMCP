import { describe, it, expect, vi } from "vitest";
import { scrollHumanized } from "../../../../src/humanization/actions/scroll.js";

function makeFakePage() {
	return {
		mouse: { wheel: vi.fn(async () => {}), move: vi.fn(async () => {}) },
	};
}

describe("scroll actions", () => {
	it("scrollHumanized wheels multiple segments", async () => {
		const page: any = makeFakePage();
		await scrollHumanized(page, 500, { segments: 5, jitterPx: 0, perSegmentMs: 0 });
		expect(page.mouse.wheel).toHaveBeenCalledTimes(5);
	});
});
