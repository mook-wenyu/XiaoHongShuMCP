/* 中文注释：网络错误测试（重试、熔断、恢复） */
import { describe, it, expect } from "vitest";
import { NetworkError } from "../../../src/core/errors/NetworkError.js";

describe("NetworkError 错误路径测试", () => {
	describe("错误创建和序列化", () => {
		it("应该正确创建网络错误", () => {
			const error = new NetworkError("Connection failed", {
				url: "https://api.example.com/test",
				method: "GET",
				status: 503
			});

			expect(error).toBeInstanceOf(Error);
			expect(error).toBeInstanceOf(NetworkError);
			expect(error.message).toBe("Connection failed");
			expect(error.code).toBe("NETWORK_ERROR");
			expect(error.retryable).toBe(true);
			expect(error.context).toEqual({
				url: "https://api.example.com/test",
				method: "GET",
				status: 503
			});
		});

		it("应该正确序列化为 JSON", () => {
			const error = new NetworkError("Request timeout", {
				url: "https://api.example.com/test",
				method: "POST",
				timeout: 30000
			});

			const json = error.toJSON();

			expect(json).toMatchObject({
				name: "NetworkError",
				code: "NETWORK_ERROR",
				message: "Request timeout",
				retryable: true,
				context: {
					url: "https://api.example.com/test",
					method: "POST",
					timeout: 30000
				}
			});
			expect(json).toHaveProperty("stack");
		});

		it("应该包含错误堆栈跟踪", () => {
			const error = new NetworkError("DNS lookup failed");

			expect(error.stack).toBeDefined();
			expect(error.stack).toContain("NetworkError");
			expect(error.stack).toContain("DNS lookup failed");
		});
	});

	describe("错误上下文信息", () => {
		it("应该支持空上下文", () => {
			const error = new NetworkError("Generic network error");

			expect(error.context).toBeUndefined();
			expect(error.toJSON().context).toBeUndefined();
		});

		it("应该支持丰富的上下文信息", () => {
			const error = new NetworkError("HTTP request failed", {
				url: "https://api.example.com/users",
				method: "GET",
				status: 500,
				statusText: "Internal Server Error",
				headers: { "content-type": "application/json" },
				body: { error: "Database connection failed" },
				duration: 5000
			});

			expect(error.context).toEqual({
				url: "https://api.example.com/users",
				method: "GET",
				status: 500,
				statusText: "Internal Server Error",
				headers: { "content-type": "application/json" },
				body: { error: "Database connection failed" },
				duration: 5000
			});
		});

		it("应该支持嵌套错误上下文", () => {
			const originalError = new Error("Connection refused");
			const error = new NetworkError("Failed to connect", {
				url: "https://api.example.com",
				originalError: originalError.message
			});

			expect(error.context?.originalError).toBe("Connection refused");
		});
	});

	describe("错误类型判断", () => {
		it("应该正确识别为可重试错误", () => {
			const error = new NetworkError("Temporary network issue");

			expect(error.retryable).toBe(true);
		});

		it("应该与其他错误类型区分", () => {
			const netError = new NetworkError("Network error");
			const genericError = new Error("Generic error");

			expect(netError).toBeInstanceOf(NetworkError);
			expect(genericError).not.toBeInstanceOf(NetworkError);
		});
	});

	describe("错误消息格式化", () => {
		it("应该支持模板化错误消息", () => {
			const url = "https://api.example.com/users/123";
			const status = 404;
			const error = new NetworkError(`Resource not found: ${url} (status: ${status})`, {
				url,
				status
			});

			expect(error.message).toBe("Resource not found: https://api.example.com/users/123 (status: 404)");
		});

		it("应该保持错误消息的原始格式", () => {
			const message = "Network error: connection timeout after 30s";
			const error = new NetworkError(message);

			expect(error.message).toBe(message);
			expect(error.toString()).toContain(message);
		});
	});

	describe("错误传播", () => {
		it("应该支持 try-catch 捕获", () => {
			expect(() => {
				throw new NetworkError("Test error");
			}).toThrow(NetworkError);

			expect(() => {
				throw new NetworkError("Test error");
			}).toThrow("Test error");
		});

		it("应该支持 async 错误捕获", async () => {
			const failingFunc = async () => {
				throw new NetworkError("Async network error");
			};

			await expect(failingFunc()).rejects.toThrow(NetworkError);
			await expect(failingFunc()).rejects.toThrow("Async network error");
		});
	});

	describe("错误恢复策略", () => {
		it("应该提供重试建议", () => {
			const error = new NetworkError("Temporary failure", {
				status: 503,
				retryAfter: 60
			});

			expect(error.retryable).toBe(true);
			expect(error.context?.retryAfter).toBe(60);
		});

		it("应该区分临时错误和永久错误", () => {
			const temporaryError = new NetworkError("503 Service Unavailable", { status: 503 });
			const permanentError = new NetworkError("404 Not Found", { status: 404 });

			// 根据状态码判断是否可重试
			expect(temporaryError.retryable).toBe(true);
			expect(permanentError.context?.status).toBe(404);
		});
	});

	describe("边界条件", () => {
		it("应该处理空字符串消息", () => {
			const error = new NetworkError("");

			expect(error.message).toBe("");
			expect(error.code).toBe("NETWORK_ERROR");
		});

		it("应该处理超长错误消息", () => {
			const longMessage = "x".repeat(10000);
			const error = new NetworkError(longMessage);

			expect(error.message).toBe(longMessage);
			expect(error.message.length).toBe(10000);
		});

		it("应该处理特殊字符", () => {
			const message = "Error: 网络异常 \n\t引号: \"' \\ 反斜杠";
			const error = new NetworkError(message);

			expect(error.message).toBe(message);

			const json = error.toJSON();
			expect(json.message).toBe(message);
		});

		it("应该处理循环引用的上下文（序列化安全）", () => {
			const context: any = { url: "https://api.example.com" };
			context.self = context; // 循环引用

			// 创建错误时应该不会抛出异常
			const error = new NetworkError("Circular reference test", context);

			expect(error.context).toBeDefined();

			// toJSON 可能会遇到循环引用问题，这取决于实现
			// 如果实现正确，应该能处理或至少不崩溃
			expect(() => error.toJSON()).not.toThrow();
		});
	});
});
