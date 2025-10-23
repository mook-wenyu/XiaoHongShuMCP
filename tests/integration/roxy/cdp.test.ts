/**
 * CDP 连接集成测试
 *
 * 测试通过 CDP 协议连接 RoxyBrowser 的功能：
 * - 建立 CDP 连接
 * - 获取 BrowserContext
 * - 页面导航和截图
 * - 基本浏览器操作
 */

import { describe, it, expect, beforeAll, afterAll } from "vitest";
const hasToken = !!process.env.ROXY_API_TOKEN;
const describeIf = hasToken ? describe : (describe.skip as typeof describe);
import { chromium, type Browser, type BrowserContext, type Page } from "playwright";
import { ConfigProvider } from "../../../src/config/ConfigProvider.js";
import { ServiceContainer } from "../../../src/core/container.js";
import type { IRoxyClient } from "../../../src/contracts/IRoxyClient.js";
import { setTimeout as delay } from "node:timers/promises";

describeIf("CDP 连接集成测试", () => {
	let roxyClient: IRoxyClient;
	let testDirId: string;
	let browser: Browser | undefined;

	beforeAll(() => {
		const configProvider = ConfigProvider.load();
		const config = configProvider.getConfig();
		const container = new ServiceContainer(config);
		roxyClient = container.createRoxyClient();
		// 使用测试专用 dirId
		testDirId = `test_cdp_${Date.now()}`;
		console.log(`使用测试 dirId: ${testDirId}`);
	});

	afterAll(async () => {
		try {
			if (browser) {
				await browser.close();
			}
			await roxyClient.close(testDirId);
			console.log(`✅ 已清理测试资源 (${testDirId})`);
		} catch (e) {
			console.warn("清理资源失败:", e);
		}
	});

	it("应该能通过 CDP 连接到 RoxyBrowser", async () => {
		// 1. 打开浏览器窗口
		console.log(`1️⃣ 打开浏览器窗口 (dirId: ${testDirId})`);
		const opened = await roxyClient.ensureOpen(testDirId);

		expect(opened).toBeDefined();
		expect(opened.ws).toBeDefined();
		expect(typeof opened.ws).toBe("string");
		console.log(`✅ 获取到 CDP Endpoint: ${opened.ws}`);

		// 2. 通过 CDP 连接
		console.log("2️⃣ 通过 CDP 连接到远程浏览器");
		browser = await chromium.connectOverCDP(opened.ws!);

		expect(browser).toBeDefined();
		expect(browser.isConnected()).toBe(true);
		console.log("✅ CDP 连接建立成功");

		// 3. 获取默认上下文
		console.log("3️⃣ 获取默认 BrowserContext");
		const contexts = browser.contexts();
		expect(contexts.length).toBeGreaterThan(0);

		const context = contexts[0];
		expect(context).toBeDefined();
		console.log(`✅ 获取到默认上下文 (共 ${contexts.length} 个上下文)`);

		// 4. 创建页面并导航
		console.log("4️⃣ 创建页面并导航到测试 URL");
		const page = await context.newPage();
		await page.goto("https://example.com", { waitUntil: "domcontentloaded" });

		const title = await page.title();
		expect(title).toBeTruthy();
		console.log(`✅ 页面加载成功，标题: ${title}`);

		// 5. 截图验证
		console.log("5️⃣ 截图验证浏览器状态");
		const screenshot = await page.screenshot({ type: "png" });
		expect(screenshot).toBeDefined();
		expect(screenshot.length).toBeGreaterThan(0);
		console.log(`✅ 截图成功 (大小: ${screenshot.length} bytes)`);

		await page.close();
	}, 30000);

	it("应该能在同一个 Context 中打开多个 Page", async () => {
		console.log("测试同一 Context 多 Page 场景");

		// 连接到浏览器
		const opened = await roxyClient.ensureOpen(testDirId);
		const browser = await chromium.connectOverCDP(opened.ws!);
		const context = browser.contexts()[0];

		// 创建 3 个页面
		const pages: Page[] = [];
		for (let i = 0; i < 3; i++) {
			console.log(`创建第 ${i + 1} 个页面`);
			const page = await context.newPage();
			await page.goto(`https://example.com?page=${i}`, { waitUntil: "domcontentloaded" });
			pages.push(page);
		}

		expect(pages.length).toBe(3);
		console.log(`✅ 成功在同一 Context 中打开 ${pages.length} 个页面`);

		// 清理页面
		for (const page of pages) {
			await page.close();
		}

		await browser.close();
	}, 30000);
});