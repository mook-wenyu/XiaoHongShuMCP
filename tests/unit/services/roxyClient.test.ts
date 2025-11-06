/* 中文注释:Roxy API 客户端单元测试 */
import { describe, it, expect, vi, beforeEach } from "vitest";
import { RoxyClient } from "../../../src/clients/roxyClient.js";
import type { ILogger } from "../../../src/contracts/ILogger.js";
// 移除未使用的类型导入

// 创建 mock fetch（使用 vi.hoisted 确保在 vi.mock 之前初始化）
const { mockFetch } = vi.hoisted(() => ({ mockFetch: vi.fn() }));

// Mock undici fetch
vi.mock("undici", () => ({
	fetch: mockFetch
}));

describe("RoxyClient 服务单元测试", () => {
	let roxyClient: RoxyClient;
	let mockLogger: ILogger;

	// Helper 函数：创建完整的 Response mock
	const createMockResponse = (data: any, options: { ok?: boolean; status?: number; contentType?: string } = {}) => ({
		ok: options.ok ?? true,
		status: options.status ?? 200,
		headers: {
			get: (key: string) => {
				if (key.toLowerCase() === "content-type") {
					return options.contentType ?? "application/json";
				}
				return null;
			}
		},
		json: () => Promise.resolve(data),
		text: () => Promise.resolve(typeof data === "string" ? data : JSON.stringify(data))
	});

	beforeEach(() => {
		// Mock ILogger
		mockLogger = {
			debug: vi.fn(),
			info: vi.fn(),
			warn: vi.fn(),
			error: vi.fn(),
			child: vi.fn().mockReturnThis(),
		} as unknown as ILogger;

		// 创建 RoxyClient 实例
		roxyClient = new RoxyClient("https://api.example.com", "test-token", mockLogger);

		// 清除所有 mocks
		mockFetch.mockReset();
	});

	describe("健康检查", () => {
		it("应该成功执行健康检查", async () => {
			mockFetch.mockResolvedValue(createMockResponse({ code: 0, msg: "OK" }));

			const result = await roxyClient.health();

			expect(result).toEqual({ code: 0, msg: "OK" });
			expect(mockFetch).toHaveBeenCalledWith(
				"https://api.example.com/health",
				expect.objectContaining({
					method: "GET",
					headers: expect.objectContaining({ token: "test-token" }),
				})
			);
		});

		it("应该处理健康检查失败", async () => {
			// 创建限制重试次数的客户端，避免长时间等待
			const clientWithRetry = new RoxyClient("https://api.example.com", "test-token", mockLogger, 1);
			mockFetch.mockRejectedValue(new Error("Network error"));

			await expect(clientWithRetry.health()).rejects.toThrow();
			expect(mockLogger.error).toHaveBeenCalledWith(
				expect.objectContaining({ err: expect.any(Error) }),
				"Roxy 健康检查失败"
			);
		});

		it("应该处理字符串响应", async () => {
			mockFetch.mockResolvedValue(createMockResponse("Healthy"));

			const result = await roxyClient.health();

			expect(result).toBe("Healthy");
		});
	});

	describe("打开浏览器窗口", () => {
		it("应该成功打开浏览器窗口", async () => {
			const mockResponse = {
				data: {
					id: "window-123",
					ws: "ws://localhost:9222/devtools/browser/abc",
					http: "http://localhost:9222",
				},
			};

			mockFetch.mockResolvedValue(createMockResponse(mockResponse));

			const result = await roxyClient.open("test-dir-1");

			expect(result).toEqual({
				id: "window-123",
				ws: "ws://localhost:9222/devtools/browser/abc",
				http: "http://localhost:9222",
			});
			expect(mockFetch).toHaveBeenCalledWith(
				"https://api.example.com/browser/open",
				expect.objectContaining({
					method: "POST",
					headers: expect.objectContaining({ token: "test-token" }),
					body: JSON.stringify({ dirId: "test-dir-1", args: undefined, workspaceId: undefined }),
				})
			);
			expect(mockLogger.info).toHaveBeenCalledWith(
				expect.objectContaining({ dirId: "test-dir-1", ws: mockResponse.data.ws }),
				"浏览器窗口已打开"
			);
		});

		it("应该支持带参数打开窗口", async () => {
			const mockResponse = {
				data: {
					id: "window-123",
					ws: "ws://localhost:9222/devtools/browser/abc",
					http: "http://localhost:9222",
				},
			};

			mockFetch.mockResolvedValue(createMockResponse(mockResponse));

			await roxyClient.open("test-dir-1", ["--headless"], "ws-123");

			expect(mockFetch).toHaveBeenCalledWith(
				"https://api.example.com/browser/open",
				expect.objectContaining({
					body: JSON.stringify({
						dirId: "test-dir-1",
						args: ["--headless"],
						workspaceId: "ws-123",
					}),
				})
			);
		});

		it("应该在缺少 ws 端点时抛出错误", async () => {
			mockFetch.mockResolvedValue(createMockResponse({ data: { id: "window-123" } }));

			await expect(roxyClient.open("test-dir-1")).rejects.toThrow("/browser/open 未返回 ws 端点");
		});

		it("应该在无 data 字段时使用 dirId 作为 id", async () => {
			mockFetch.mockResolvedValue(createMockResponse({
					data: {
						ws: "ws://localhost:9222/devtools/browser/abc",
						http: "http://localhost:9222",
					},
				}));

			const result = await roxyClient.open("test-dir-1");

			expect(result.id).toBe("test-dir-1");
		});
	});

	describe("关闭浏览器窗口", () => {
		it("应该成功关闭浏览器窗口", async () => {
			mockFetch.mockResolvedValue(createMockResponse({ success: true }));

			await roxyClient.close("test-dir-1");

			expect(mockFetch).toHaveBeenCalledWith(
				"https://api.example.com/browser/close",
				expect.objectContaining({
					method: "POST",
					body: JSON.stringify({ dirId: "test-dir-1" }),
				})
			);
			expect(mockLogger.info).toHaveBeenCalledWith(
				expect.objectContaining({ dirId: "test-dir-1" }),
				"浏览器窗口已关闭"
			);
		});

		it("应该处理关闭失败", async () => {
			const clientWithRetry = new RoxyClient("https://api.example.com", "test-token", mockLogger, 1);
			mockFetch.mockRejectedValue(new Error("Close failed"));

			await expect(clientWithRetry.close("test-dir-1")).rejects.toThrow();
		});
	});

	describe("查询连接信息", () => {
		it("应该成功查询单个连接信息", async () => {
			const mockResponse = {
				data: [
					{
						dirId: "test-dir-1",
						ws: "ws://localhost:9222/devtools/browser/abc",
						http: "http://localhost:9222",
					},
				],
			};

			mockFetch.mockResolvedValue(createMockResponse(mockResponse));

			const result = await roxyClient.connectionInfo(["test-dir-1"]);

			// connectionInfo 现在返回完整响应对象（包含 data），而不是解包的数组
			expect(result).toEqual(mockResponse);
			expect(mockFetch).toHaveBeenCalledWith(
				"https://api.example.com/browser/connection_info?dirIds=test-dir-1",
				expect.objectContaining({ method: "GET" })
			);
		});

		it("应该查询多个连接信息", async () => {
			mockFetch.mockResolvedValue(createMockResponse({ data: [] }));

			await roxyClient.connectionInfo(["dir1", "dir2", "dir3"]);

			expect(mockFetch).toHaveBeenCalledWith(
				"https://api.example.com/browser/connection_info?dirIds=dir1%2Cdir2%2Cdir3",
				expect.any(Object)
			);
		});

		it("应该处理无 data 字段的响应", async () => {
			const mockResponse = { dirId: "test-dir-1", ws: "ws://..." };

			mockFetch.mockResolvedValue(createMockResponse(mockResponse));

			const result = await roxyClient.connectionInfo(["test-dir-1"]);

			expect(result).toEqual(mockResponse);
		});
	});

	describe("确保窗口打开", () => {
		it("应该复用已存在的窗口", async () => {
			const mockConnInfo = {
				data: [
					{
						dirId: "test-dir-1",
						ws: "ws://localhost:9222/devtools/browser/abc",
						http: "http://localhost:9222",
					},
				],
			};

			mockFetch.mockResolvedValue(createMockResponse(mockConnInfo));

			const result = await roxyClient.ensureOpen("test-dir-1");

			expect(result).toEqual({
				id: "test-dir-1",
				ws: "ws://localhost:9222/devtools/browser/abc",
				http: "http://localhost:9222",
			});
			expect(mockLogger.debug).toHaveBeenCalledWith(
				expect.objectContaining({ dirId: "test-dir-1", ws: mockConnInfo.data[0].ws }),
				"复用已存在的浏览器窗口"
			);
			// 不应该调用 open
			expect(mockFetch).toHaveBeenCalledTimes(1);
		});

		it("应该在窗口不存在时打开新窗口", async () => {
			mockFetch.mockImplementation((url: string) => {
				if (url.includes("connection_info")) {
					return Promise.resolve(createMockResponse({ data: [] }));
				}
				// open
				return Promise.resolve(createMockResponse({
					data: {
						id: "window-123",
						ws: "ws://localhost:9222/devtools/browser/abc",
						http: "http://localhost:9222",
					},
				}));
			});

			const result = await roxyClient.ensureOpen("test-dir-1");

			expect(result.ws).toBe("ws://localhost:9222/devtools/browser/abc");
			expect(mockFetch).toHaveBeenCalledTimes(2); // connectionInfo + open
		});

		it("应该在 connectionInfo 失败时尝试打开", async () => {
			// 创建限制重试次数的客户端，避免长时间等待
			const clientWithRetry = new RoxyClient("https://api.example.com", "test-token", mockLogger, 1);

			mockFetch.mockImplementation((url: string) => {
				if (url.includes("connection_info")) {
					return Promise.reject(new Error("ConnectionInfo failed"));
				}
				// open
				return Promise.resolve(createMockResponse({
					data: {
						id: "window-123",
						ws: "ws://localhost:9222/devtools/browser/abc",
						http: "http://localhost:9222",
					},
				}));
			});

			const result = await clientWithRetry.ensureOpen("test-dir-1");

			expect(result.ws).toBe("ws://localhost:9222/devtools/browser/abc");
			expect(mockLogger.debug).toHaveBeenCalledWith(
				expect.objectContaining({ dirId: "test-dir-1" }),
				"查询连接信息失败，尝试打开新窗口"
			);
		});

		it("应该支持带 workspaceId 的确保打开", async () => {
			mockFetch.mockImplementation((url: string) => {
				if (url.includes("connection_info")) {
					return Promise.resolve(createMockResponse({ data: [] }));
				}
				return Promise.resolve(createMockResponse({
					data: {
						ws: "ws://localhost:9222/devtools/browser/abc",
						http: "http://localhost:9222",
					},
				}));
			});

			await roxyClient.ensureOpen("test-dir-1", "ws-123", ["--headless"]);

			expect(mockFetch).toHaveBeenCalledWith(
				"https://api.example.com/browser/open",
				expect.objectContaining({
					body: JSON.stringify({
						dirId: "test-dir-1",
						args: ["--headless"],
						workspaceId: "ws-123",
					}),
				})
			);
		});
	});

	describe("工作空间管理", () => {
		it("应该获取工作空间列表", async () => {
			const mockResponse = {
				data: [
					{ id: "ws-1", name: "Workspace 1" },
					{ id: "ws-2", name: "Workspace 2" },
				],
			};

			mockFetch.mockResolvedValue(createMockResponse(mockResponse));

			const result = await roxyClient.workspaces();

			expect(result).toEqual(mockResponse);
			expect(mockFetch).toHaveBeenCalledWith(
				"https://api.example.com/browser/workspace",
				expect.any(Object)
			);
		});

		it("应该支持分页参数", async () => {
			mockFetch.mockResolvedValue(createMockResponse({ data: [] }));

			await roxyClient.workspaces({ page_index: 2, page_size: 20 });

			expect(mockFetch).toHaveBeenCalledWith(
				"https://api.example.com/browser/workspace?page_index=2&page_size=20",
				expect.any(Object)
			);
		});
	});

	describe("窗口管理", () => {
		it("应该获取窗口列表", async () => {
			mockFetch.mockResolvedValue(createMockResponse({ data: [] }));

			await roxyClient.listWindows({ workspaceId: "ws-123" });

			expect(mockFetch).toHaveBeenCalledWith(
				"https://api.example.com/browser/list_v3?workspaceId=ws-123",
				expect.any(Object)
			);
		});

		it("应该支持多个查询参数", async () => {
			mockFetch.mockResolvedValue(createMockResponse({ data: [] }));

			await roxyClient.listWindows({
				workspaceId: "ws-123",
				dirIds: "dir1,dir2",
				status: 1,
				page_index: 1,
				page_size: 10,
			});

			expect(mockFetch).toHaveBeenCalledWith(
				expect.stringContaining("workspaceId=ws-123"),
				expect.any(Object)
			);
			expect(mockFetch).toHaveBeenCalledWith(
				expect.stringContaining("dirIds=dir1%2Cdir2"),
				expect.any(Object)
			);
		});

		it("应该创建浏览器窗口", async () => {
			mockFetch.mockResolvedValue(createMockResponse({ data: { id: "window-123" } }));

			const body = { workspaceId: "ws-123", name: "Test Window" };
			const result = await roxyClient.createWindow(body);

			expect(result).toEqual({ data: { id: "window-123" } });
			expect(mockFetch).toHaveBeenCalledWith(
				"https://api.example.com/browser/create",
				expect.objectContaining({
					method: "POST",
					body: JSON.stringify(body),
				})
			);
		});

		// 已移除：detailWindow 不再对外暴露（减少不必要的 API 依赖）
	});

	describe("查询字符串构建", () => {
		it("应该正确编码查询参数", async () => {
			mockFetch.mockResolvedValue(createMockResponse({ data: [] }));

			await roxyClient.workspaces({ page_index: 1, page_size: 10 });

			expect(mockFetch).toHaveBeenCalledWith(
				"https://api.example.com/browser/workspace?page_index=1&page_size=10",
				expect.any(Object)
			);
		});

		it("应该跳过 undefined/null/空值", async () => {
			mockFetch.mockResolvedValue(createMockResponse({ data: [] }));

			await roxyClient.listWindows({
				workspaceId: "ws-123",
				dirIds: undefined as any,
				windowName: "",
				status: 0,
			});

			const callUrl = mockFetch.mock.calls[0][0];
			expect(callUrl).toContain("workspaceId=ws-123");
			expect(callUrl).toContain("status=0"); // 0 不应该被跳过
			expect(callUrl).not.toContain("dirIds");
			expect(callUrl).not.toContain("windowName");
		});

		it("应该处理特殊字符编码", async () => {
			mockFetch.mockResolvedValue(createMockResponse({ data: [] }));

			await roxyClient.listWindows({
				workspaceId: "ws-123",
				windowName: "Test & Special=Chars",
			});

			const callUrl = mockFetch.mock.calls[0][0];
			expect(callUrl).toContain("windowName=Test%20%26%20Special%3DChars");
		});
	});

	describe("边界条件", () => {
		it("应该处理空 dirIds 数组", async () => {
			mockFetch.mockResolvedValue(createMockResponse({ data: [] }));

			await roxyClient.connectionInfo([]);

			// 空数组会生成空字符串，被 qs() 过滤掉，所以没有查询参数
			expect(mockFetch).toHaveBeenCalledWith(
				"https://api.example.com/browser/connection_info",
				expect.any(Object)
			);
		});

		it("应该处理特殊字符 dirId", async () => {
			mockFetch.mockResolvedValue(createMockResponse({
						data: {
							ws: "ws://localhost:9222/devtools/browser/abc",
							http: "http://localhost:9222",
						},
					}));

			const specialDirId = "dir/with:special@chars#123";
			await roxyClient.open(specialDirId);

			expect(mockFetch).toHaveBeenCalledWith(
				"https://api.example.com/browser/open",
				expect.objectContaining({
					body: JSON.stringify({ dirId: specialDirId, args: undefined, workspaceId: undefined }),
				})
			);
		});
	});
});
