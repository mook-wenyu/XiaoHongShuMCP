/**
 * 数据隔离验证测试
 *
 * 验证多个账号（dirId）之间的数据完全隔离：
 * - Cookies 隔离
 * - LocalStorage 隔离
 * - SessionStorage 隔离
 * - 会话状态隔离
 */

import { describe, it, expect, beforeAll, afterAll } from "vitest";
import { roxySupportsOpen } from "../../helpers/roxy.js";
const ready = await roxySupportsOpen();
const describeIf = ready ? describe : (describe.skip as typeof describe);
import { chromium, type Browser } from "playwright";
import { ConfigProvider } from "../../../src/config/ConfigProvider.js";
import { ServiceContainer } from "../../../src/core/container.js";
import type { IRoxyClient } from "../../../src/contracts/IRoxyClient.js";

describeIf("数据隔离验证测试", () => {
	let roxyClient: IRoxyClient;
	const testDirIds = [
		`test_isolation_a_${Date.now()}`,
		`test_isolation_b_${Date.now() + 1}`,
		`test_isolation_c_${Date.now() + 2}`,
	];
	const browsers: Browser[] = [];

	beforeAll(() => {
		const configProvider = ConfigProvider.load();
		const config = configProvider.getConfig();
		const container = new ServiceContainer(config);
		roxyClient = container.createRoxyClient();
		console.log(`使用测试 dirIds: ${testDirIds.join(", ")}`);
	});

	afterAll(async () => {
		console.log("清理测试资源...");
		try {
			// 关闭所有浏览器连接
			for (const browser of browsers) {
				await browser.close();
			}
			// 关闭所有窗口
			for (const dirId of testDirIds) {
				await roxyClient.close(dirId);
			}
			console.log("✅ 所有测试资源已清理");
		} catch (e) {
			console.warn("清理资源失败:", e);
		}
	});

	it("应该验证多账号 Cookies 完全隔离", async () => {
		console.log("🧪 测试 Cookies 隔离性");

		// 连接到 3 个不同的账号
		const connections = await Promise.all(
			testDirIds.map(async (dirId) => {
				const opened = await roxyClient.ensureOpen(dirId);
				const browser = await chromium.connectOverCDP(opened.ws!);
				browsers.push(browser);
				const context = browser.contexts()[0];
				const page = await context.newPage();
				return { dirId, browser, context, page };
			}),
		);

		// 为每个账号设置不同的 Cookie
		for (let i = 0; i < connections.length; i++) {
			const { context, page, dirId } = connections[i];
			await page.goto("https://example.com");

			// 设置唯一的 Cookie
			await context.addCookies([
				{
					name: "test_account_id",
					value: `account_${i + 1}`,
					domain: "example.com",
					path: "/",
				},
				{
					name: "test_session",
					value: `session_${dirId}`,
					domain: "example.com",
					path: "/",
				},
			]);

			console.log(`  ✓ 账号 ${i + 1} (${dirId}): 设置 Cookie account_${i + 1}`);
		}

		// 验证每个账号的 Cookie 都是独立的
		for (let i = 0; i < connections.length; i++) {
			const { context, dirId } = connections[i];
			const cookies = await context.cookies("https://example.com");

			const accountCookie = cookies.find((c) => c.name === "test_account_id");
			expect(accountCookie).toBeDefined();
			expect(accountCookie?.value).toBe(`account_${i + 1}`);

			const sessionCookie = cookies.find((c) => c.name === "test_session");
			expect(sessionCookie).toBeDefined();
			expect(sessionCookie?.value).toBe(`session_${dirId}`);

			console.log(`  ✅ 账号 ${i + 1}: Cookies 隔离验证通过`);
		}

		// 清理页面
		for (const conn of connections) {
			await conn.page.close();
		}

		console.log("✅ Cookies 隔离性验证通过！");
	}, 60000);

	it("应该验证多账号 LocalStorage 完全隔离", async () => {
		console.log("🧪 测试 LocalStorage 隔离性");

		// 重新使用已连接的浏览器
		const connections = browsers.map((browser, i) => ({
			dirId: testDirIds[i],
			browser,
			context: browser.contexts()[0],
		}));

		// 为每个账号设置不同的 LocalStorage
		for (let i = 0; i < connections.length; i++) {
			const { context, dirId } = connections[i];
			const page = await context.newPage();
			await page.goto("https://example.com");

			// 设置唯一的 LocalStorage
			await page.evaluate(
				(data) => {
					localStorage.setItem("account_id", data.accountId);
					localStorage.setItem("user_name", data.userName);
					localStorage.setItem("session_token", data.sessionToken);
				},
				{
					accountId: `account_${i + 1}`,
					userName: `user_${i + 1}`,
					sessionToken: `token_${dirId}`,
				},
			);

			console.log(`  ✓ 账号 ${i + 1}: 设置 LocalStorage`);
			await page.close();
		}

		// 验证每个账号的 LocalStorage 都是独立的
		for (let i = 0; i < connections.length; i++) {
			const { context, dirId } = connections[i];
			const page = await context.newPage();
			await page.goto("https://example.com");

			const storageData = await page.evaluate(() => {
				return {
					accountId: localStorage.getItem("account_id"),
					userName: localStorage.getItem("user_name"),
					sessionToken: localStorage.getItem("session_token"),
				};
			});

			expect(storageData.accountId).toBe(`account_${i + 1}`);
			expect(storageData.userName).toBe(`user_${i + 1}`);
			expect(storageData.sessionToken).toBe(`token_${dirId}`);

			console.log(`  ✅ 账号 ${i + 1}: LocalStorage 隔离验证通过 (${JSON.stringify(storageData)})`);

			await page.close();
		}

		console.log("✅ LocalStorage 隔离性验证通过！");
	}, 60000);

	it("应该验证多账号 SessionStorage 完全隔离", async () => {
		console.log("🧪 测试 SessionStorage 隔离性");

		const connections = browsers.map((browser, i) => ({
			dirId: testDirIds[i],
			browser,
			context: browser.contexts()[0],
		}));

		// 为每个账号设置不同的 SessionStorage
		for (let i = 0; i < connections.length; i++) {
			const { context, dirId } = connections[i];
			const page = await context.newPage();
			await page.goto("https://example.com");

			await page.evaluate(
				(data) => {
					sessionStorage.setItem("temp_session", data.sessionId);
					sessionStorage.setItem("temp_token", data.token);
				},
				{
					sessionId: `session_${i + 1}`,
					token: `temp_${dirId}`,
				},
			);

			console.log(`  ✓ 账号 ${i + 1}: 设置 SessionStorage`);
			await page.close();
		}

		// 验证隔离性
		for (let i = 0; i < connections.length; i++) {
			const { context, dirId } = connections[i];
			const page = await context.newPage();
			await page.goto("https://example.com");

			const sessionData = await page.evaluate(() => {
				return {
					sessionId: sessionStorage.getItem("temp_session"),
					token: sessionStorage.getItem("temp_token"),
				};
			});

			expect(sessionData.sessionId).toBe(`session_${i + 1}`);
			expect(sessionData.token).toBe(`temp_${dirId}`);

			console.log(`  ✅ 账号 ${i + 1}: SessionStorage 隔离验证通过`);
			await page.close();
		}

		console.log("✅ SessionStorage 隔离性验证通过！");
	}, 60000);

	it("应该验证多账号之间不会相互影响", async () => {
		console.log("🧪 综合隔离性测试");

		const connections = browsers.map((browser, i) => ({
			dirId: testDirIds[i],
			browser,
			context: browser.contexts()[0],
		}));

		// 同时在所有账号中读取数据
		const results = await Promise.all(
			connections.map(async ({ context, dirId }, i) => {
				const page = await context.newPage();
				await page.goto("https://example.com");

				const data = await page.evaluate(() => {
					return {
						cookies: document.cookie,
						localStorage: {
							accountId: localStorage.getItem("account_id"),
							userName: localStorage.getItem("user_name"),
						},
						sessionStorage: {
							sessionId: sessionStorage.getItem("temp_session"),
						},
					};
				});

				await page.close();
				return { dirId, accountIndex: i + 1, data };
			}),
		);

		// 验证每个账号的数据都是独立的
		for (let i = 0; i < results.length; i++) {
			const result = results[i];
			expect(result.data.localStorage.accountId).toBe(`account_${i + 1}`);
			expect(result.data.localStorage.userName).toBe(`user_${i + 1}`);
			expect(result.data.sessionStorage.sessionId).toBe(`session_${i + 1}`);

			console.log(`  ✅ 账号 ${result.accountIndex}: 所有数据隔离正常 (${result.dirId})`);
		}

		console.log("✅ 综合隔离性测试通过！所有账号数据完全隔离！");
	}, 60000);
});
