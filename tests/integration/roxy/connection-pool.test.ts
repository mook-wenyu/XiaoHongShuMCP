/**
 * 连接池管理测试
 *
 * 验证 ConnectionManager 的核心功能：
 * - 连接复用
 * - TTL 自动清理
 * - 健康检查
 * - 预热功能
 */

import { describe, it, expect, beforeAll, afterAll } from "vitest";
import { roxySupportsOpen } from "../../helpers/roxy.js";
const ready = await roxySupportsOpen();
const describeIf = ready ? describe : (describe.skip as typeof describe);
import { ConfigProvider } from "../../../src/config/ConfigProvider.js";
import { ServiceContainer } from "../../../src/core/container.js";
import { ConnectionManager } from "../../../src/services/connectionManager.js";
import type { IPlaywrightConnector } from "../../../src/contracts/IPlaywrightConnector.js";
import { setTimeout as delay } from "node:timers/promises";

describeIf("连接池管理测试", () => {
	let connector: IPlaywrightConnector;
	let connectionManager: ConnectionManager;
	let testDirIds: string[];
	const SHORT_TTL = 3000; // 3秒 TTL 用于快速测试

	beforeAll(() => {
		const configProvider = ConfigProvider.load();
		const config = configProvider.getConfig();
		const container = new ServiceContainer(config);

		connector = container.createPlaywrightConnector();
		// 创建短 TTL 的连接管理器用于测试
		connectionManager = new ConnectionManager(
			connector,
			SHORT_TTL,
			container.createLogger({ module: "connection-pool-test" })
		);

		testDirIds = [
			`test_pool_a_${Date.now()}`,
			`test_pool_b_${Date.now() + 1}`,
			`test_pool_c_${Date.now() + 2}`,
		];

		console.log(`使用测试 dirIds: ${testDirIds.join(", ")}`);
		console.log(`TTL 设置: ${SHORT_TTL}ms`);
	});

	afterAll(async () => {
		console.log("清理连接池...");
		await connectionManager.closeAll();
		console.log("✅ 连接池已清理");
	});

	it("应该能成功创建和获取连接", async () => {
		console.log("🧪 测试连接创建");

		const dirId = testDirIds[0];
		const conn = await connectionManager.get(dirId);

		expect(conn).toBeDefined();
		expect(conn.browser).toBeDefined();
		expect(conn.context).toBeDefined();
		expect(conn.browser.isConnected()).toBe(true);

		console.log(`✅ 连接创建成功 (${dirId})`);
	}, 30000);

	it("应该能复用已存在的连接", async () => {
		console.log("🧪 测试连接复用");

		const dirId = testDirIds[0];

		// 第一次获取
		const conn1 = await connectionManager.get(dirId);
		const browser1 = conn1.browser;

		// 第二次获取相同 dirId
		const conn2 = await connectionManager.get(dirId);
		const browser2 = conn2.browser;

		// 应该是同一个浏览器实例
		expect(browser1).toBe(browser2);
		expect(conn1.context).toBe(conn2.context);

		console.log("✅ 连接复用验证通过（两次获取返回同一实例）");
	}, 30000);

	it("应该能预热多个连接", async () => {
		console.log("🧪 测试连接池预热");

		// 预热 3 个连接
		const warmedIds = await connectionManager.warmup(testDirIds);

		expect(warmedIds.length).toBe(testDirIds.length);
		console.log(`✅ 成功预热 ${warmedIds.length} 个连接`);

		// 验证所有连接都已创建
		for (const dirId of testDirIds) {
			const hasConn = connectionManager.has(dirId);
			expect(hasConn).toBe(true);
			console.log(`  ✓ ${dirId}: 连接已存在`);
		}

		// 列出所有连接
		const allConnections = connectionManager.list();
		expect(allConnections.length).toBeGreaterThanOrEqual(testDirIds.length);
		console.log(`✅ 连接池中共有 ${allConnections.length} 个连接`);
	}, 60000);

	it("应该能执行健康检查", async () => {
		console.log("🧪 测试连接健康检查");

		const dirId = testDirIds[0];

		// 确保连接存在
		await connectionManager.get(dirId);

		// 执行健康检查
		const isHealthy = await connectionManager.healthCheck(dirId);
		expect(isHealthy).toBe(true);

		console.log(`✅ 健康检查通过 (${dirId})`);
	}, 30000);

	it("应该在 TTL 超时后自动清理连接", async () => {
		console.log(`🧪 测试 TTL 自动清理 (等待 ${SHORT_TTL}ms + 清理周期)`);

		const dirId = `test_ttl_${Date.now()}`;

		// 创建连接
		await connectionManager.get(dirId);
		expect(connectionManager.has(dirId)).toBe(true);
		console.log(`  ✓ 连接已创建 (${dirId})`);

		// 等待超过 TTL 时间
		const waitTime = SHORT_TTL + 2000; // TTL + 2秒（确保清理任务执行）
		console.log(`  ⏳ 等待 ${waitTime}ms 让 TTL 过期...`);
		await delay(waitTime);

		// 验证连接已被清理
		const stillExists = connectionManager.has(dirId);
		expect(stillExists).toBe(false);

		console.log("✅ TTL 自动清理验证通过（连接已被清除）");
	}, 90000);

	it("应该能关闭单个连接", async () => {
		console.log("🧪 测试关闭单个连接");

		const dirId = `test_close_${Date.now()}`;

		// 创建连接
		await connectionManager.get(dirId);
		expect(connectionManager.has(dirId)).toBe(true);

		// 关闭连接
		await connectionManager.close(dirId);

		// 验证已关闭
		expect(connectionManager.has(dirId)).toBe(false);

		console.log(`✅ 连接关闭成功 (${dirId})`);
	}, 30000);

	it("应该能关闭所有连接", async () => {
		console.log("🧪 测试关闭所有连接");

		// 预热一些连接
		const ids = [`test_close_all_1_${Date.now()}`, `test_close_all_2_${Date.now()}`];
		await connectionManager.warmup(ids);

		const beforeCount = connectionManager.list().length;
		expect(beforeCount).toBeGreaterThan(0);
		console.log(`  ✓ 当前连接数: ${beforeCount}`);

		// 关闭所有连接
		await connectionManager.closeAll();

		const afterCount = connectionManager.list().length;
		expect(afterCount).toBe(0);

		console.log("✅ 所有连接已关闭");
	}, 60000);

	it("应该在连接失效后健康检查失败", async () => {
		console.log("🧪 测试失效连接的健康检查");

		const dirId = `test_unhealthy_${Date.now()}`;

		// 创建连接
		const conn = await connectionManager.get(dirId);

		// 手动关闭浏览器（模拟连接失效）
		await conn.browser.close();

		// 健康检查应该失败
		const isHealthy = await connectionManager.healthCheck(dirId);
		expect(isHealthy).toBe(false);

		console.log("✅ 失效连接健康检查正确返回 false");

		// 清理
		await connectionManager.close(dirId);
	}, 30000);
});
