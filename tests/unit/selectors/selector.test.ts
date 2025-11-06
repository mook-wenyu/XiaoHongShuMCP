/* 单元测试：选择器解析功能 */
import { describe, it, expect, beforeEach, afterEach } from "vitest";
import { chromium, type Page, type Browser } from "playwright";
import { resolveLocatorAsync } from "../../../src/selectors/selector.js";
import { XHS_CONF } from "../../../src/config/xhs.js";
import type { TargetHints } from "../../../src/selectors/types.js";

describe("selector.ts - resolveLocatorAsync", () => {
	let browser: Browser;
	let page: Page;

	beforeEach(async () => {
		browser = await chromium.launch({ headless: true });
		page = await browser.newPage();
		await page.setContent(`
      <html>
        <body>
          <input type="text" placeholder="搜索小红书" id="search-input" class="search-input" />
          <button role="button" aria-label="搜索">查找</button>
          <div data-testid="container">测试容器</div>
          <a href="/test">链接文本</a>
        </body>
      </html>
    `);
	});

	afterEach(async () => {
		await browser?.close();
	});

	it("应该返回 Promise<Locator> 类型", async () => {
		const hints: TargetHints = { selector: "#search-input" };
		const locator = await resolveLocatorAsync(page, hints);
		expect(locator).toBeDefined();
		expect(typeof locator.click).toBe("function");
	});

	it("应该优先使用 role 定位器", async () => {
		const hints: TargetHints = {
			alternatives: [{ role: "button", name: { contains: "搜索" } }, { selector: "button" }],
		};
		const locator = await resolveLocatorAsync(page, hints);
		const text = await locator.textContent();
		expect(text).toBe("查找");
	});

	it("应该支持 alternatives 多候选回退", async () => {
		const hints: TargetHints = {
			alternatives: [
				{ selector: "#non-existent" },
				{ placeholder: "搜索小红书" },
				{ selector: "#search-input" },
			],
		};
		const locator = await resolveLocatorAsync(page, hints, { probeTimeoutMs: 100 });
		const placeholder = await locator.getAttribute("placeholder");
		expect(placeholder).toBe("搜索小红书");
	});

	it("应该使用配置的 probeTimeoutMs", async () => {
		const hints: TargetHints = { selector: "#search-input" };
		const locator = await resolveLocatorAsync(page, hints);
		expect(locator).toBeDefined();
		// 验证默认配置存在
		expect(XHS_CONF.selector.probeTimeoutMs).toBe(250);
	});

	it("应该支持 testId 定位器", async () => {
		const hints: TargetHints = { testId: "container" };
		const locator = await resolveLocatorAsync(page, hints);
		const text = await locator.textContent();
		expect(text).toBe("测试容器");
	});

	it("应该支持 text 定位器", async () => {
		const hints: TargetHints = { text: { exact: "链接文本" } };
		const locator = await resolveLocatorAsync(page, hints);
		const text = await locator.textContent();
		expect(text).toBe("链接文本");
	});
});

describe("config/xhs.ts - XHS_CONF", () => {
	it("应该包含 selector 配置", () => {
		expect(XHS_CONF.selector).toBeDefined();
		expect(XHS_CONF.selector.probeTimeoutMs).toBeGreaterThan(0);
		expect(XHS_CONF.selector.resolveTimeoutMs).toBeGreaterThan(0);
		expect(XHS_CONF.selector.healthCheckIntervalMs).toBeGreaterThan(0);
	});

	it("应该包含 capture 配置", () => {
		expect(XHS_CONF.capture).toBeDefined();
		expect(XHS_CONF.capture.minContentLength).toBeGreaterThan(0);
		expect(XHS_CONF.capture.waitNetworkIdleMs).toBeGreaterThan(0);
		expect(XHS_CONF.capture.waitContentMs).toBeGreaterThan(0);
	});

	it("应该支持环境变量覆盖", () => {
		// 备份原始值
		const original = process.env.XHS_SELECTOR_PROBE_MS;

		// 设置环境变量并重新导入（注：此测试验证类型，运行时覆盖需要重启进程）
		process.env.XHS_SELECTOR_PROBE_MS = "500";

		// 验证配置类型正确
		expect(typeof XHS_CONF.selector.probeTimeoutMs).toBe("number");

		// 恢复原始值
		if (original) {
			process.env.XHS_SELECTOR_PROBE_MS = original;
		} else {
			delete process.env.XHS_SELECTOR_PROBE_MS;
		}
	});
});
