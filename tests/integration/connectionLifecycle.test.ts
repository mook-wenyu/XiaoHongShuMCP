/* 中文注释：连接生命周期测试（预热→复用→清理） */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { ConnectionManager, type ManagedConn } from "../../src/services/connectionManager.js";
import type { IPlaywrightConnector } from "../../src/contracts/IPlaywrightConnector.js";
import type { ILogger } from "../../src/contracts/ILogger.js";
import type { Browser, BrowserContext } from "playwright";

describe("连接生命周期测试", () => {
	let connector: IPlaywrightConnector;
	let logger: ILogger;
	let connectionManager: ConnectionManager;
	let mockBrowser: Browser;
	let mockContext: BrowserContext;

	beforeEach(() => {
		// Mock logger
		logger = {
			debug: vi.fn(),
			info: vi.fn(),
			warn: vi.fn(),
			error: vi.fn(),
			child: vi.fn().mockReturnThis()
		} as unknown as ILogger;

		// Mock browser and context
		mockBrowser = {
			close: vi.fn().mockResolvedValue(undefined),
			isConnected: vi.fn().mockReturnValue(true)
		} as unknown as Browser;

		mockContext = {
			newPage: vi.fn().mockResolvedValue({
				goto: vi.fn(),
				close: vi.fn()
			}),
			pages: vi.fn().mockReturnValue([]),
			close: vi.fn().mockResolvedValue(undefined)
		} as unknown as BrowserContext;

		// Mock connector
		connector = {
			connect: vi.fn().mockResolvedValue({
				browser: mockBrowser,
				context: mockContext
			}),
			withContext: vi.fn(),
			newPage: vi.fn()
		} as unknown as IPlaywrightConnector;

		// 创建连接管理器（TTL 1 秒用于测试）
		connectionManager = new ConnectionManager(connector, 1000, logger);
	});

	afterEach(async () => {
		await connectionManager.closeAll();
		vi.clearAllMocks();
	});

	describe("预热功能", () => {
		it("应该成功预热多个连接", async () => {
			const dirIds = ["dir1", "dir2", "dir3"];

			const warmed = await connectionManager.warmup(dirIds);

			expect(warmed).toEqual(dirIds);
			expect(connector.connect).toHaveBeenCalledTimes(3);
			expect(connectionManager.list()).toEqual(dirIds);
		});

		it("应该部分成功预热（部分失败）", async () => {
			const dirIds = ["dir1", "dir2", "dir3"];

			// dir2 连接失败
			(connector.connect as any).mockImplementation((dirId: string) => {
				if (dirId === "dir2") {
					return Promise.reject(new Error("Connection failed"));
				}
				return Promise.resolve({ browser: mockBrowser, context: mockContext });
			});

			const warmed = await connectionManager.warmup(dirIds);

			expect(warmed).toEqual(["dir1", "dir3"]);
			expect(connectionManager.list()).toEqual(["dir1", "dir3"]);
			expect(logger.warn).toHaveBeenCalledWith(
				expect.objectContaining({ dirId: "dir2" }),
				"预热连接失败"
			);
		});

		it("应该记录预热统计信息", async () => {
			const dirIds = ["dir1", "dir2"];

			await connectionManager.warmup(dirIds);

			expect(logger.info).toHaveBeenCalledWith(
				expect.objectContaining({ count: 2 }),
				"开始预热连接池"
			);
			expect(logger.info).toHaveBeenCalledWith(
				expect.objectContaining({ succeeded: 2, failed: 0, total: 2 }),
				"连接池预热完成"
			);
		});
	});

	describe("连接复用", () => {
		it("应该复用已存在的连接", async () => {
			const conn1 = await connectionManager.get("dir1");
			const conn2 = await connectionManager.get("dir1");

			expect(conn1).toBe(conn2);
			expect(connector.connect).toHaveBeenCalledTimes(1);
			expect(logger.debug).toHaveBeenCalledWith(
				{ dirId: "dir1" },
				"复用已存在的连接"
			);
		});

		it("应该更新连接的最后使用时间", async () => {
			const conn1 = await connectionManager.get("dir1") as ManagedConn;
			const firstUsed = conn1.lastUsed;

			// 等待 10ms
			await new Promise(resolve => setTimeout(resolve, 10));

			const conn2 = await connectionManager.get("dir1") as ManagedConn;

			expect(conn2.lastUsed).toBeGreaterThan(firstUsed);
		});

		it("应该为不同 dirId 创建不同连接", async () => {
			await connectionManager.get("dir1");
			await connectionManager.get("dir2");

			expect(connectionManager.list()).toEqual(["dir1", "dir2"]);
			expect(connector.connect).toHaveBeenCalledTimes(2);
		});
	});

	describe("健康检查", () => {
		it("应该检测健康的连接", async () => {
			await connectionManager.get("dir1");

			const healthy = await connectionManager.healthCheck("dir1");

			expect(healthy).toBe(true);
			expect(mockContext.newPage).toHaveBeenCalled();
		});

		it("应该检测不存在的连接", async () => {
			const healthy = await connectionManager.healthCheck("non-existent");

			expect(healthy).toBe(false);
			expect(logger.debug).toHaveBeenCalledWith(
				{ dirId: "non-existent" },
				"连接不存在，健康检查失败"
			);
		});

		it("应该检测不健康的连接", async () => {
			await connectionManager.get("dir1");

			// 模拟连接失败
			(mockContext.newPage as any).mockRejectedValue(new Error("Connection lost"));

			const healthy = await connectionManager.healthCheck("dir1");

			expect(healthy).toBe(false);
			expect(logger.warn).toHaveBeenCalledWith(
				expect.objectContaining({ dirId: "dir1" }),
				"健康检查失败"
			);
		});
	});

	describe("连接清理", () => {
		it("应该正确关闭单个连接", async () => {
			await connectionManager.get("dir1");

			await connectionManager.close("dir1");

			expect(connectionManager.has("dir1")).toBe(false);
			expect(mockBrowser.close).toHaveBeenCalled();
			expect(logger.info).toHaveBeenCalledWith(
				expect.objectContaining({ dirId: "dir1", remainingConnections: 0 }),
				"连接已关闭"
			);
		});

		it("应该忽略不存在的连接", async () => {
			await connectionManager.close("non-existent");

			// 不应该抛出错误
			expect(mockBrowser.close).not.toHaveBeenCalled();
		});

		it("应该关闭所有连接", async () => {
			await connectionManager.get("dir1");
			await connectionManager.get("dir2");

			await connectionManager.closeAll();

			expect(connectionManager.list()).toEqual([]);
			expect(mockBrowser.close).toHaveBeenCalledTimes(2);
			expect(logger.info).toHaveBeenCalledWith(
				{ count: 2 },
				"关闭所有连接"
			);
		});

		it("应该在关闭失败时继续处理", async () => {
			await connectionManager.get("dir1");

			// 模拟关闭失败
			(mockBrowser.close as any).mockRejectedValue(new Error("Close failed"));

			await connectionManager.close("dir1");

			expect(connectionManager.has("dir1")).toBe(false);
			expect(logger.warn).toHaveBeenCalledWith(
				expect.objectContaining({ dirId: "dir1" }),
				"关闭浏览器失败，忽略"
			);
		});
	});

	describe("TTL 自动清理", () => {
		it("应该自动清理过期连接", async () => {
			// 创建短 TTL 连接管理器（100ms）
			const shortTtlManager = new ConnectionManager(connector, 100, logger);

			await shortTtlManager.get("dir1");

			// 等待 TTL 过期并触发清理（默认清理间隔是 TTL/2，即 50ms）
			await new Promise(resolve => setTimeout(resolve, 200));

			// 手动触发清理（因为定时器使用 unref，可能不会自动触发）
			await (shortTtlManager as any).sweep();

			expect(shortTtlManager.has("dir1")).toBe(false);
			expect(logger.info).toHaveBeenCalledWith(
				expect.objectContaining({ count: 1, ids: ["dir1"] }),
				"清理过期连接"
			);

			await shortTtlManager.closeAll();
		}, 10000);

		it("应该保留未过期的连接", async () => {
			await connectionManager.get("dir1");

			// 手动触发清理（连接未过期）
			await (connectionManager as any).sweep();

			expect(connectionManager.has("dir1")).toBe(true);
			expect(mockBrowser.close).not.toHaveBeenCalled();
		});
	});
});
