import { describe, it, expect, vi } from "vitest";
import {
	moveMouseCubic,
	hoverHuman,
	clickHuman,
} from "../../../../src/humanization/actions/mouse.js";

function makeFakeLocator() {
	return {
		scrollIntoViewIfNeeded: vi.fn(async () => {}),
		waitFor: vi.fn(async (_: any) => {}),
		boundingBox: vi.fn(async () => ({ x: 100, y: 200, width: 50, height: 40 })),
		hover: vi.fn(async () => {}),
		click: vi.fn(async () => {}),
	};
}

function makeFakePage() {
	return {
		mouse: { move: vi.fn(async () => {}), wheel: vi.fn(async () => {}) },
	};
}

describe("mouse actions", () => {
	it("moveMouseCubic moves along path", async () => {
		const loc: any = makeFakeLocator();
		const page: any = makeFakePage();
		await moveMouseCubic(page, loc);
		expect(page.mouse.move).toHaveBeenCalled();
	});

	it("hoverHuman moves then hovers", async () => {
		const loc: any = makeFakeLocator();
		const page: any = makeFakePage();
		await hoverHuman(page, loc);
		expect(loc.hover).toHaveBeenCalled();
	});

	it("clickHuman moves then clicks", async () => {
		const loc: any = makeFakeLocator();
		const page: any = makeFakePage();
		await clickHuman(page, loc);
		expect(loc.click).toHaveBeenCalled();
	});
});
