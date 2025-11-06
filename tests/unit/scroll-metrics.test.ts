import { describe, it, expect } from "vitest";
import { computeRetention } from "../../src/domain/xhs/scroll-metrics";

describe("scroll metrics computeRetention", () => {
	const mk = (id: string, y: number) => ({ index: y, noteId: id, title: "", text: "", y }) as any;
	it("computes retention as shared / prev", () => {
		const prev = [mk("a", 0), mk("b", 1), mk("c", 2)];
		const curr = [mk("b", 0), mk("c", 1), mk("d", 2)];
		const r = computeRetention(prev, curr);
		expect(r.prevCount).toBe(3);
		expect(r.currCount).toBe(3);
		expect(r.shared).toBe(2);
		expect(r.retention).toBeCloseTo(2 / 3);
	});
	it("falls back to text hash when id missing", () => {
		const prev = [
			{ index: 0, title: "Hi", text: "Hello World", y: 0 },
			{ index: 1, title: "X", text: "Y", y: 10 },
		] as any;
		const curr = [
			{ index: 0, title: "hi", text: "hello  world", y: 5 },
			{ index: 1, title: "Z", text: "Q", y: 10 },
		] as any;
		const r = computeRetention(prev, curr);
		expect(r.shared).toBe(1);
		expect(r.retention).toBeCloseTo(0.5);
	});
});
