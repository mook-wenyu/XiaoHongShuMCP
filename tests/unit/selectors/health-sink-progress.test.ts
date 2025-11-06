import { describe, it, expect } from "vitest";

import { appendNavProgress } from "../../../src/selectors/health-sink";

// 该用例仅校验函数可调用（写入会落地到 artifacts/selector-health.ndjson，不影响主流程）

describe("health sink: appendNavProgress", () => {
	it("should be callable with minimal fields", async () => {
		await appendNavProgress({
			url: "https://www.xiaohongshu.com/explore",
			slug: "xiaohongshu",
			round: 0,
			anchors: 0,
			visited: 0,
			progressed: false,
		});
		expect(true).toBe(true);
	});
});
