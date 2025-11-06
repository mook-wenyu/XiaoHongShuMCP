import { describe, it, expect } from "vitest";

// 这里不跑真浏览器；仅验证“无进展→短超时→推进”分支的参数读取与分支选择逻辑。
// 做法：通过伪造 XHS_CONF，调用内部纯函数（借助模块拦截）或验证配置值存在与默认值合理性。

import { XHS_CONF } from "../../../src/config/xhs";

describe("xhs: scroll-after strategy config", () => {
	it("should expose sane defaults for smart batch confirmation", () => {
		expect(XHS_CONF.scroll.useApiAfterScroll).toBeTypeOf("boolean");
		expect(XHS_CONF.scroll.shortFeedWaitMs).toBeGreaterThan(0);
		expect(XHS_CONF.scroll.shortSearchWaitMs).toBeGreaterThan(0);
		expect(XHS_CONF.scroll.microScrollOnNoProgressPx).toBeGreaterThanOrEqual(60);
		expect(XHS_CONF.scroll.noProgressRoundsForBoost).toBeGreaterThanOrEqual(1);
		expect(XHS_CONF.scroll.boostScrollMinPx).toBeGreaterThanOrEqual(800);
	});
});
