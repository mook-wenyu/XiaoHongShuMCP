/* 中文注释：连接管理器单元测试 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { ConnectionManager, type ManagedConn } from "../../../src/services/connectionManager.js";
import type { IPlaywrightConnector } from "../../../src/contracts/IPlaywrightConnector.js";
import type { ILogger } from "../../../src/contracts/ILogger.js";
import type { Browser, BrowserContext, Page } from "playwright";

describe("ConnectionManager 服务单元测试", () => {
	let connectionManager: ConnectionManager;
	let mockConnector: IPlaywrightConnector;
	let mockLogger: ILogger;
	let mockBrowser: Browser;
	let mockContext: BrowserContext;
	let mockPage: Page;

	beforeEach(() => {
		// 设置环境变量以控制 TTL 和清理间隔
		process.env.CONNECTION_TTL_MS = "100";
		process.env.CONNECTION_SWEEP_MS = "50"; // 50ms 清理间隔

		// Mock Browser
		mockBrowser = {
			close: vi.fn().mockResolvedValue(undefined),
			isConnected: vi.fn().mockReturnValue(true),
		} as any;

		// Mock Page
		mockPage = {
			close: vi.fn().mockResolvedValue(undefined),
			goto: vi.fn().mockResolvedValue(null),
		} as any;

		// Mock BrowserContext
		mockContext = {
			newPage: vi.fn().mockResolvedValue(mockPage),
			close: vi.fn().mockResolvedValue(undefined),
		} as any;

		// Mock IPlaywrightConnector
		mockConnector = {
			connect: vi.fn().mockResolvedValue({
				browser: mockBrowser,
				context: mockContext,
			}),
			disconnect: vi.fn().mockResolvedValue(undefined),
		};

		// Mock ILogger
		mockLogger = {
			debug: vi.fn(),
			info: vi.fn(),
			warn: vi.fn(),
			error: vi.fn(),
			child: vi.fn().mockReturnThis(),
		} as unknown as ILogger;

		// 创建 ConnectionManager 实例（TTL 设置为 100ms 便于测试）
		connectionManager = new ConnectionManager(mockConnector, 100, mockLogger);
	});

	afterEach(async () => {
		// 清理定时器和连接
		await connectionManager.closeAll();

		// 清理环境变量
		delete process.env.CONNECTION_TTL_MS;
		delete process.env.CONNECTION_SWEEP_MS;
	});

	describe("连接获取和复用", () => {
		it("应该成功获取新连接", async () => {
			const conn = await connectionManager.get("test-dir-1");

			expect(conn).toBeDefined();
			expect(conn.browser).toBe(mockBrowser);
			expect(conn.context).toBe(mockContext);
			expect(conn.lastUsed).toBeGreaterThan(0);
			expect(mockConnector.connect).toHaveBeenCalledWith("test-dir-1", undefined);
			expect(mockLogger.info).toHaveBeenCalledWith(
				expect.objectContaining({ dirId: "test-dir-1", totalConnections: 1 }),
				"连接已创建"
			);
		});

		it("应该复用已存在的连接", async () => {
			const conn1 = await connectionManager.get("test-dir-1");
			const lastUsed1 = conn1.lastUsed;

			// 等待 10ms 后再次获取
			await new Promise((resolve) => setTimeout(resolve, 10));

			const conn2 = await connectionManager.get("test-dir-1");
			const lastUsed2 = conn2.lastUsed;

			expect(conn2).toBe(conn1);
			expect(lastUsed2).toBeGreaterThan(lastUsed1);
			expect(mockConnector.connect).toHaveBeenCalledTimes(1);
			expect(mockLogger.debug).toHaveBeenCalledWith(
				expect.objectContaining({ dirId: "test-dir-1" }),
				"复用已存在的连接"
			);
		});

		it("应该支持带 workspaceId 的连接", async () => {
			const conn = await connectionManager.get("test-dir-2", { workspaceId: "ws-123" });

			expect(conn.workspaceId).toBe("ws-123");
			expect(mockConnector.connect).toHaveBeenCalledWith("test-dir-2", { workspaceId: "ws-123" });
		});

		it("应该正确检查连接是否存在", async () => {
			expect(connectionManager.has("test-dir-1")).toBe(false);

			await connectionManager.get("test-dir-1");

			expect(connectionManager.has("test-dir-1")).toBe(true);
		});

		it("应该列出所有连接的 dirId", async () => {
			await connectionManager.get("dir1");
			await connectionManager.get("dir2");
			await connectionManager.get("dir3");

			const list = connectionManager.list();

			expect(list).toHaveLength(3);
			expect(list).toContain("dir1");
			expect(list).toContain("dir2");
			expect(list).toContain("dir3");
		});
	});

	describe("连接关闭", () => {
		it("应该成功关闭单个连接", async () => {
			await connectionManager.get("test-dir-1");

			await connectionManager.close("test-dir-1");

			expect(mockBrowser.close).toHaveBeenCalled();
			expect(connectionManager.has("test-dir-1")).toBe(false);
			expect(mockLogger.info).toHaveBeenCalledWith(
				expect.objectContaining({ dirId: "test-dir-1", remainingConnections: 0 }),
				"连接已关闭"
			);
		});

		it("应该忽略不存在的连接", async () => {
			await connectionManager.close("non-existent");

			expect(mockBrowser.close).not.toHaveBeenCalled();
		});

		it("应该处理关闭失败", async () => {
			await connectionManager.get("test-dir-1");
			(mockBrowser.close as any).mockRejectedValueOnce(new Error("Close failed"));

			await connectionManager.close("test-dir-1");

			expect(mockLogger.warn).toHaveBeenCalledWith(
				expect.objectContaining({ dirId: "test-dir-1" }),
				"关闭浏览器失败，忽略"
			);
			expect(connectionManager.has("test-dir-1")).toBe(false);
		});

		it("应该关闭所有连接", async () => {
			await connectionManager.get("dir1");
			await connectionManager.get("dir2");
			await connectionManager.get("dir3");

			await connectionManager.closeAll();

			expect(mockBrowser.close).toHaveBeenCalledTimes(3);
			expect(connectionManager.list()).toHaveLength(0);
			expect(mockLogger.info).toHaveBeenCalledWith(
				expect.objectContaining({ count: 3 }),
				"关闭所有连接"
			);
		});
	});

	describe("预热功能", () => {
		it("应该成功预热多个连接", async () => {
			const dirIds = ["dir1", "dir2", "dir3"];

			const warmed = await connectionManager.warmup(dirIds);

			expect(warmed).toEqual(dirIds);
			expect(mockConnector.connect).toHaveBeenCalledTimes(3);
			expect(connectionManager.list()).toEqual(dirIds);
			expect(mockLogger.info).toHaveBeenCalledWith(
				expect.objectContaining({ succeeded: 3, failed: 0, total: 3 }),
				"连接池预热完成"
			);
		});

		it("应该部分成功预热（部分失败）", async () => {
			const dirIds = ["dir1", "dir2", "dir3"];
			(mockConnector.connect as any).mockImplementation((dirId: string) => {
				if (dirId === "dir2") {
					return Promise.reject(new Error("Connection failed"));
				}
				return Promise.resolve({ browser: mockBrowser, context: mockContext });
			});

			const warmed = await connectionManager.warmup(dirIds);

			expect(warmed).toEqual(["dir1", "dir3"]);
			expect(mockLogger.info).toHaveBeenCalledWith(
				expect.objectContaining({ succeeded: 2, failed: 1, total: 3 }),
				"连接池预热完成"
			);
			expect(mockLogger.warn).toHaveBeenCalledWith(
				expect.objectContaining({ dirId: "dir2" }),
				"预热连接失败"
			);
		});

		it("应该支持带 workspaceId 的预热", async () => {
			const dirIds = ["dir1", "dir2"];
			const opts = { workspaceId: "ws-123" };

			await connectionManager.warmup(dirIds, opts);

			expect(mockConnector.connect).toHaveBeenCalledWith("dir1", opts);
			expect(mockConnector.connect).toHaveBeenCalledWith("dir2", opts);
		});

		it("应该处理空数组预热", async () => {
			const warmed = await connectionManager.warmup([]);

			expect(warmed).toEqual([]);
			expect(mockConnector.connect).not.toHaveBeenCalled();
		});
	});

	describe("健康检查", () => {
		it("应该对健康连接返回 true", async () => {
			await connectionManager.get("test-dir-1");

			const healthy = await connectionManager.healthCheck("test-dir-1");

			expect(healthy).toBe(true);
			expect(mockContext.newPage).toHaveBeenCalled();
			expect(mockPage.close).toHaveBeenCalled();
			expect(mockLogger.debug).toHaveBeenCalledWith(
				expect.objectContaining({ dirId: "test-dir-1" }),
				"健康检查通过"
			);
		});

		it("应该对不存在的连接返回 false", async () => {
			const healthy = await connectionManager.healthCheck("non-existent");

			expect(healthy).toBe(false);
			expect(mockLogger.debug).toHaveBeenCalledWith(
				expect.objectContaining({ dirId: "non-existent" }),
				"连接不存在，健康检查失败"
			);
		});

		it("应该对不健康的连接返回 false", async () => {
			await connectionManager.get("test-dir-1");
			(mockContext.newPage as any).mockRejectedValueOnce(new Error("Context closed"));

			const healthy = await connectionManager.healthCheck("test-dir-1");

			expect(healthy).toBe(false);
			expect(mockLogger.warn).toHaveBeenCalledWith(
				expect.objectContaining({ dirId: "test-dir-1" }),
				"健康检查失败"
			);
		});
	});

	describe("TTL 自动清理", () => {
		it("应该清理过期连接", async () => {
			// 创建连接
			await connectionManager.get("test-dir-1");
			expect(connectionManager.has("test-dir-1")).toBe(true);

			// 等待超过 TTL 时间（100ms）+ sweep 间隔
			await new Promise((resolve) => setTimeout(resolve, 200));

			// 连接应该被自动清理
			expect(connectionManager.has("test-dir-1")).toBe(false);
			expect(mockLogger.info).toHaveBeenCalledWith(
				expect.objectContaining({ count: 1, ids: ["test-dir-1"] }),
				"清理过期连接"
			);
		}, 5000);

		it("应该不清理活跃连接", async () => {
			// 创建连接
			await connectionManager.get("test-dir-1");

			// 每隔 50ms 访问一次（保持活跃）
			const interval = setInterval(async () => {
				await connectionManager.get("test-dir-1");
			}, 50);

			// 等待 200ms
			await new Promise((resolve) => setTimeout(resolve, 200));

			clearInterval(interval);

			// 连接应该仍然存在
			expect(connectionManager.has("test-dir-1")).toBe(true);
		}, 5000);

		it("应该只清理过期连接，保留活跃连接", async () => {
			// 创建两个连接
			await connectionManager.get("dir1");
			await connectionManager.get("dir2");

			// 等待 50ms 后访问 dir2（保持活跃）
			await new Promise((resolve) => setTimeout(resolve, 50));
			await connectionManager.get("dir2");

			// 等待 70ms（总共 120ms）
			// dir1: t=0 创建，t=120 时已过期（超过 TTL 100ms）
			// dir2: t=50 刷新，t=120 时未过期（距刷新仅 70ms < 100ms）
			await new Promise((resolve) => setTimeout(resolve, 70));

			// dir1 应该被清理，dir2 仍然存在
			expect(connectionManager.has("dir1")).toBe(false);
			expect(connectionManager.has("dir2")).toBe(true);
		}, 5000);
	});

	describe("边界条件", () => {
		it("应该处理并发获取相同 dirId", async () => {
			const promises = [
				connectionManager.get("concurrent-dir"),
				connectionManager.get("concurrent-dir"),
				connectionManager.get("concurrent-dir"),
			];

			const results = await Promise.all(promises);

			// 应该都返回同一个连接（使用 toStrictEqual 进行深度比较）
			expect(results[0]).toStrictEqual(results[1]);
			expect(results[1]).toStrictEqual(results[2]);
			// connect 可能被调用 1-3 次（取决于竞态条件）
			expect(mockConnector.connect).toHaveBeenCalled();
		});

		it("应该处理连接失败", async () => {
			(mockConnector.connect as any).mockRejectedValueOnce(new Error("Connection failed"));

			await expect(connectionManager.get("failing-dir")).rejects.toThrow("Connection failed");

			expect(connectionManager.has("failing-dir")).toBe(false);
		});

		it("应该处理空 dirId", async () => {
			const conn = await connectionManager.get("");

			expect(conn).toBeDefined();
			expect(mockConnector.connect).toHaveBeenCalledWith("", undefined);
		});

		it("应该处理特殊字符 dirId", async () => {
			const specialDirId = "dir/with:special@chars#123";
			const conn = await connectionManager.get(specialDirId);

			expect(conn).toBeDefined();
			expect(mockConnector.connect).toHaveBeenCalledWith(specialDirId, undefined);
		});
	});

	describe("日志记录", () => {
		it("应该在创建连接时记录日志", async () => {
			await connectionManager.get("test-dir-1");

			expect(mockLogger.debug).toHaveBeenCalledWith(
				expect.objectContaining({ dirId: "test-dir-1" }),
				"创建新连接"
			);
			expect(mockLogger.info).toHaveBeenCalledWith(
				expect.objectContaining({ dirId: "test-dir-1", totalConnections: 1 }),
				"连接已创建"
			);
		});

		it("应该在复用连接时记录日志", async () => {
			await connectionManager.get("test-dir-1");
			vi.clearAllMocks();

			await connectionManager.get("test-dir-1");

			expect(mockLogger.debug).toHaveBeenCalledWith(
				expect.objectContaining({ dirId: "test-dir-1" }),
				"复用已存在的连接"
			);
		});

		it("应该在关闭连接时记录日志", async () => {
			await connectionManager.get("test-dir-1");
			vi.clearAllMocks();

			await connectionManager.close("test-dir-1");

			expect(mockLogger.debug).toHaveBeenCalledWith(
				expect.objectContaining({ dirId: "test-dir-1" }),
				"关闭连接"
			);
			expect(mockLogger.info).toHaveBeenCalledWith(
				expect.objectContaining({ dirId: "test-dir-1", remainingConnections: 0 }),
				"连接已关闭"
			);
		});
	});
});
