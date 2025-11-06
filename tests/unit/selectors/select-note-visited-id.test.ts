import { describe, it, expect } from "vitest";

// 轻量测试：校验我们对锚点 id 的策略（优先 noteId，否则 href）是否能容纳通用模式
// 注意：不引入 Playwright，仅以正则匹配进行验证

const extractId = (href: string): string => {
	const m = href.match(/(?:explore|discovery\/item|search_result)\/(\w+)/);
	return m && m[1] ? m[1] : href || "";
};

describe("xhs: visited id extraction (anchor href)", () => {
	it("prefers explicit note id segments", () => {
		expect(extractId("/discovery/item/abc123?from=home")).toBe("abc123");
		expect(extractId("/search_result/xyz789")).toBe("xyz789");
		expect(extractId("/explore/def456")).toBe("def456");
	});
	it("falls back to href when no id present", () => {
		expect(extractId("/explore?some=param")).toBe("/explore?some=param");
	});
});
