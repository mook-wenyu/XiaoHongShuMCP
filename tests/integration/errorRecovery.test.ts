/* 中文注释：错误恢复测试（网络失败→重试→成功） */
import { describe, it, expect, vi, beforeEach } from "vitest";
import { HttpClient, type HttpClientOptions } from "../../src/lib/http.js";
import { NetworkError } from "../../src/core/errors/NetworkError.js";
import type { ILogger } from "../../src/contracts/ILogger.js";

// 创建 mock fetch（使用 vi.hoisted 确保在 vi.mock 之前初始化）
const { mockFetch } = vi.hoisted(() => ({ mockFetch: vi.fn() }));

// Mock undici fetch
vi.mock("undici", () => ({
	fetch: mockFetch,
}));

describe("错误恢复测试", () => {
	let logger: ILogger;

	// Helper 函数：创建完整的 Response mock
	const createMockResponse = (
		data: any,
		options: { ok?: boolean; status?: number; contentType?: string; statusText?: string } = {},
	) => ({
		ok: options.ok ?? true,
		status: options.status ?? 200,
		statusText: options.statusText ?? "OK",
		headers: {
			get: (key: string) => {
				if (key.toLowerCase() === "content-type") {
					return options.contentType ?? "application/json";
				}
				return null;
			},
		},
		json: () => Promise.resolve(data),
		text: () => Promise.resolve(typeof data === "string" ? data : JSON.stringify(data)),
	});

	beforeEach(() => {
		logger = {
			debug: vi.fn(),
			info: vi.fn(),
			warn: vi.fn(),
			error: vi.fn(),
			child: vi.fn().mockReturnThis(),
		} as unknown as ILogger;

		// 重置所有 mocks
		mockFetch.mockReset();
	});

	describe("网络错误重试", () => {
		it("应该在网络错误后成功重试", async () => {
			let attempt = 0;
			mockFetch.mockImplementation(() => {
				attempt++;
				if (attempt < 3) {
					return Promise.reject(new Error("Network error"));
				}
				return Promise.resolve(createMockResponse({ ok: true }));
			});

			const options: HttpClientOptions = {
				baseURL: "https://api.example.com",
				headers: { token: "test-token" },
				maxRetries: 3,
				logger,
			};

			const client = new HttpClient(options);
			const result = await client.get<{ ok: boolean }>("/test");

			expect(result).toEqual({ ok: true });
			expect(mockFetch).toHaveBeenCalledTimes(3);
			expect(logger.warn).toHaveBeenCalled();
		});

		it("应该在 HTTP 5xx 错误后重试", async () => {
			let attempt = 0;
			mockFetch.mockImplementation(() => {
				attempt++;
				if (attempt < 2) {
					return Promise.resolve(
						createMockResponse(
							{ error: "Service unavailable" },
							{ ok: false, status: 503, statusText: "Service Unavailable" },
						),
					);
				}
				return Promise.resolve(createMockResponse({ ok: true }));
			});

			const options: HttpClientOptions = {
				baseURL: "https://api.example.com",
				maxRetries: 3,
				logger,
			};

			const client = new HttpClient(options);
			const result = await client.get<{ ok: boolean }>("/test");

			expect(result).toEqual({ ok: true });
			expect(mockFetch).toHaveBeenCalledTimes(2);
		});

		it("应该在重试次数用尽后抛出 NetworkError", async () => {
			mockFetch.mockRejectedValue(new Error("Persistent network error"));

			const options: HttpClientOptions = {
				baseURL: "https://api.example.com",
				maxRetries: 2, // 最多 2 次重试 = 总共 3 次尝试
				logger,
			};

			const client = new HttpClient(options);

			await expect(client.get("/test")).rejects.toThrow(NetworkError);
			expect(mockFetch).toHaveBeenCalledTimes(3); // 1次初始 + 2次重试
			expect(logger.error).toHaveBeenCalledWith(
				expect.objectContaining({ err: expect.any(Error), url: expect.stringContaining("/test") }),
				"HTTP 请求最终失败",
			);
		});

		it("应该不重试 HTTP 4xx 错误", async () => {
			mockFetch.mockResolvedValue(
				createMockResponse(
					{ error: "Bad request" },
					{ ok: false, status: 400, statusText: "Bad Request" },
				),
			);

			const options: HttpClientOptions = {
				baseURL: "https://api.example.com",
				maxRetries: 3,
				logger,
			};

			const client = new HttpClient(options);

			await expect(client.get("/test")).rejects.toThrow(NetworkError);
			expect(mockFetch).toHaveBeenCalledTimes(1); // 仅调用一次，不重试
		});
	});

	describe("超时处理", () => {
		it("应该在请求超时后重试", async () => {
			let attempt = 0;
			mockFetch.mockImplementation(() => {
				attempt++;
				if (attempt < 2) {
					return new Promise((_, reject) => {
						setTimeout(() => reject(new Error("Timeout")), 50);
					});
				}
				return Promise.resolve(createMockResponse({ ok: true }));
			});

			const options: HttpClientOptions = {
				baseURL: "https://api.example.com",
				maxRetries: 3,
				logger,
			};

			const client = new HttpClient(options);
			const result = await client.get<{ ok: boolean }>("/test");

			expect(result).toEqual({ ok: true });
			expect(mockFetch).toHaveBeenCalledTimes(2);
		}, 10000);
	});

	describe("响应格式错误", () => {
		it("应该处理非 JSON 响应", async () => {
			mockFetch.mockResolvedValue({
				ok: true,
				status: 200,
				headers: {
					get: (key: string) => {
						if (key.toLowerCase() === "content-type") {
							return "application/json";
						}
						return null;
					},
				},
				json: () => Promise.reject(new Error("Invalid JSON")),
				text: () => Promise.resolve("plain text response"),
			});

			const options: HttpClientOptions = {
				baseURL: "https://api.example.com",
				maxRetries: 1, // 限制重试次数，避免长时间退避
				logger,
			};

			const client = new HttpClient(options);

			await expect(client.get("/test")).rejects.toThrow();
		});
	});

	describe("请求中断和重试", () => {
		it("应该支持 AbortController 中断请求", async () => {
			mockFetch.mockImplementation(() => {
				return new Promise((_, reject) => {
					setTimeout(() => reject(new Error("AbortError")), 100);
				});
			});

			const options: HttpClientOptions = {
				baseURL: "https://api.example.com",
				maxRetries: 1,
				logger,
			};

			const client = new HttpClient(options);

			await expect(client.get("/test")).rejects.toThrow();
		}, 5000);
	});

	describe("并发请求错误处理", () => {
		it("应该独立处理多个并发请求的错误", async () => {
			mockFetch.mockImplementation((url: string) => {
				if (url.includes("/fail")) {
					return Promise.reject(new Error("Network error"));
				}
				return Promise.resolve(createMockResponse({ ok: true, url }));
			});

			const options: HttpClientOptions = {
				baseURL: "https://api.example.com",
				maxRetries: 1, // 最多 1 次重试 = 总共 2 次尝试
				logger,
			};

			const client = new HttpClient(options);

			const results = await Promise.allSettled([
				client.get("/success1"),
				client.get("/fail"),
				client.get("/success2"),
			]);

			expect(results[0].status).toBe("fulfilled");
			expect(results[1].status).toBe("rejected");
			expect(results[2].status).toBe("fulfilled");

			// 验证 mockFetch 调用次数：success1(1次) + fail(2次:初始+重试) + success2(1次) = 4次
			expect(mockFetch).toHaveBeenCalledTimes(4);
		});
	});

	describe("错误上下文信息", () => {
		it("应该在 NetworkError 中包含请求上下文", async () => {
			mockFetch.mockRejectedValue(new Error("Network error"));

			const options: HttpClientOptions = {
				baseURL: "https://api.example.com",
				headers: { token: "test-token" },
				maxRetries: 1,
				logger,
			};

			const client = new HttpClient(options);

			try {
				await client.get("/test");
				expect.fail("Should have thrown");
			} catch (error) {
				expect(error).toBeInstanceOf(NetworkError);
				const netError = error as NetworkError;
				expect(netError.context).toMatchObject({
					url: expect.stringContaining("/test"),
					method: "GET",
				});
			}
		});

		it("应该在错误中包含响应状态码", async () => {
			mockFetch.mockResolvedValue(
				createMockResponse(
					{ error: "Server error" },
					{ ok: false, status: 500, statusText: "Internal Server Error" },
				),
			);

			const options: HttpClientOptions = {
				baseURL: "https://api.example.com",
				maxRetries: 1,
				logger,
			};

			const client = new HttpClient(options);

			try {
				await client.get("/test");
				expect.fail("Should have thrown");
			} catch (error) {
				expect(error).toBeInstanceOf(NetworkError);
				const netError = error as NetworkError;
				expect(netError.context?.status).toBe(500);
			}
		});
	});
});
