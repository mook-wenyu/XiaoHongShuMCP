import { describe, it, expect } from "vitest";
import { resolveLocator } from "../src/steps/selector.js";

describe("selector hints", () => {
	it("throws when no hints", async () => {
		// 仅验证函数健壮性（不实际创建 page），使用假对象触发错误分支
		const fake: any = {};
		expect(() => resolveLocator(fake, {} as any)).toThrowError();
	});
});
