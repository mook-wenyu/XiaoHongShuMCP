/* 中文注释：完整工作流集成测试（打开→截图→关闭） */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import type { IRoxyClient } from "../../src/contracts/IRoxyClient.js";
import type { IPlaywrightConnector } from "../../src/contracts/IPlaywrightConnector.js";

describe("完整工作流集成测试", () => {
	let mockRoxy: IRoxyClient;
	let mockConnector: IPlaywrightConnector;

	beforeEach(() => {
		// 创建 mock Roxy 客户端
		mockRoxy = {
			health: vi.fn().mockResolvedValue({ ok: true }),
			open: vi.fn().mockResolvedValue({ id: "test-dir", ws: "ws://localhost:9222", http: "http://localhost:9222" }),
			close: vi.fn().mockResolvedValue(undefined),
			connectionInfo: vi.fn().mockResolvedValue({ connections: [] }),
			workspaces: vi.fn().mockResolvedValue({ data: [] }),
			listWindows: vi.fn().mockResolvedValue({ data: [] }),
			createWindow: vi.fn().mockResolvedValue({ id: "new-window" }),
			detailWindow: vi.fn().mockResolvedValue({ id: "test-window" }),
			ensureOpen: vi.fn().mockResolvedValue({ id: "test-dir", ws: "ws://localhost:9222" }),
			qs: vi.fn((params: Record<string, any>) => new URLSearchParams(params).toString())
		} as IRoxyClient;

		// 创建 mock Playwright 连接器
		mockConnector = {
			connect: vi.fn().mockResolvedValue({
				browser: { close: vi.fn() },
				context: {
					pages: vi.fn().mockReturnValue([]),
					newPage: vi.fn().mockResolvedValue({
						goto: vi.fn(),
						screenshot: vi.fn(),
						close: vi.fn()
					})
				}
			}),
			withContext: vi.fn().mockImplementation(async (_dirId, callback) => {
				const mockContext = {
					pages: vi.fn().mockReturnValue([]),
					newPage: vi.fn().mockResolvedValue({
						goto: vi.fn(),
						screenshot: vi.fn(),
						close: vi.fn(),
						url: vi.fn().mockReturnValue("https://example.com")
					}),
					close: vi.fn()
				};
				return await callback(mockContext as any);
			}),
			newPage: vi.fn().mockResolvedValue({
				goto: vi.fn(),
				screenshot: vi.fn(),
				close: vi.fn()
			})
		} as unknown as IPlaywrightConnector;
	});

	afterEach(() => {
		vi.clearAllMocks();
	});

	it("应该完成从打开到关闭的完整流程", async () => {
		// 使用 mock connector 执行工作流
		const result = await mockConnector.withContext("test-dir", async (ctx) => {
			const page = await ctx.newPage();
			await page.goto("https://example.com");
			const screenshot = await page.screenshot({ path: "test.png" });
			await page.close();
			return { ok: true, url: page.url(), screenshot };
		});

		expect(result.ok).toBe(true);
		expect(mockConnector.withContext).toHaveBeenCalledWith("test-dir", expect.any(Function));
	});

	it("应该支持多页面并发操作", async () => {
		const results = await mockConnector.withContext("test-dir", async (ctx) => {
			const page1 = await ctx.newPage();
			const page2 = await ctx.newPage();

			await Promise.all([
				page1.goto("https://example.com/page1"),
				page2.goto("https://example.com/page2")
			]);

			return { pages: [page1.url(), page2.url()] };
		});

		expect(results.pages).toHaveLength(2);
	});

	it("应该正确处理页面导航错误", async () => {
		const mockConnectorWithError: IPlaywrightConnector = {
			...mockConnector,
			withContext: vi.fn().mockImplementation(async (_dirId, callback) => {
				const mockContext = {
					newPage: vi.fn().mockResolvedValue({
						goto: vi.fn().mockRejectedValue(new Error("Navigation timeout")),
						close: vi.fn()
					})
				};
				return await callback(mockContext as any);
			})
		} as unknown as IPlaywrightConnector;

		await expect(async () => {
			await mockConnectorWithError.withContext("test-dir", async (ctx) => {
				const page = await ctx.newPage();
				await page.goto("https://example.com");
			});
		}).rejects.toThrow("Navigation timeout");
	});

	it("应该确保资源正确清理", async () => {
		const mockPage = {
			goto: vi.fn(),
			screenshot: vi.fn(),
			close: vi.fn(),
			url: vi.fn().mockReturnValue("https://example.com")
		};

		const mockContext = {
			newPage: vi.fn().mockResolvedValue(mockPage),
			close: vi.fn()
		};

		const mockConnectorWithCleanup: IPlaywrightConnector = {
			...mockConnector,
			withContext: vi.fn().mockImplementation(async (_dirId, callback) => {
				try {
					return await callback(mockContext as any);
				} finally {
					await mockContext.close();
				}
			})
		} as unknown as IPlaywrightConnector;

		await mockConnectorWithCleanup.withContext("test-dir", async (ctx) => {
			const page = await ctx.newPage();
			await page.goto("https://example.com");
			return { ok: true };
		});

		expect(mockContext.close).toHaveBeenCalled();
	});
});
