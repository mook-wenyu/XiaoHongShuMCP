/* 中文注释：日志记录器单元测试 */
import { describe, it, expect, vi, beforeEach } from "vitest";
import { PinoLogger } from "../../../src/logging/PinoLogger.js";
import type { Logger as PinoInstance } from "pino";

describe("PinoLogger 服务单元测试", () => {
	let logger: PinoLogger;
	let mockPino: PinoInstance;

	beforeEach(() => {
		// Mock Pino 实例
		mockPino = {
			debug: vi.fn(),
			info: vi.fn(),
			warn: vi.fn(),
			error: vi.fn(),
			child: vi.fn().mockReturnThis(),
		} as any;

		logger = new PinoLogger(mockPino);
	});

	describe("基本日志记录", () => {
		it("应该记录 debug 级别日志（字符串）", () => {
			logger.debug("这是一条调试消息");

			expect(mockPino.debug).toHaveBeenCalledWith("这是一条调试消息");
		});

		it("应该记录 debug 级别日志（对象）", () => {
			const obj = { userId: "123", action: "login" };
			logger.debug(obj, "用户登录");

			expect(mockPino.debug).toHaveBeenCalledWith(obj, "用户登录");
		});

		it("应该记录 info 级别日志（字符串）", () => {
			logger.info("这是一条信息消息");

			expect(mockPino.info).toHaveBeenCalledWith("这是一条信息消息");
		});

		it("应该记录 info 级别日志（对象）", () => {
			const obj = { requestId: "req-123", duration: 150 };
			logger.info(obj, "请求完成");

			expect(mockPino.info).toHaveBeenCalledWith(obj, "请求完成");
		});

		it("应该记录 warn 级别日志（字符串）", () => {
			logger.warn("这是一条警告消息");

			expect(mockPino.warn).toHaveBeenCalledWith("这是一条警告消息");
		});

		it("应该记录 warn 级别日志（对象）", () => {
			const obj = { retry: 3, maxRetries: 5 };
			logger.warn(obj, "重试警告");

			expect(mockPino.warn).toHaveBeenCalledWith(obj, "重试警告");
		});

		it("应该记录 error 级别日志（字符串）", () => {
			logger.error("这是一条错误消息");

			expect(mockPino.error).toHaveBeenCalledWith("这是一条错误消息");
		});

		it("应该记录 error 级别日志（对象，无错误对象）", () => {
			const obj = { operation: "save", status: "failed" };
			logger.error(obj, "操作失败");

			expect(mockPino.error).toHaveBeenCalledWith(obj, "操作失败");
		});
	});

	describe("错误对象处理", () => {
		it("应该正确处理 Error 对象（err 字段）", () => {
			const error = new Error("测试错误");
			error.stack = "Error: 测试错误\n    at test.ts:10:15";

			logger.error({ err: error, context: "test" }, "发生错误");

			expect(mockPino.error).toHaveBeenCalledWith(
				expect.objectContaining({
					context: "test",
					err: {
						name: "Error",
						message: "测试错误",
						stack: expect.stringContaining("Error: 测试错误"),
					},
				}),
				"发生错误",
			);
		});

		it("应该正确处理 Error 对象（error 字段）", () => {
			const error = new Error("测试错误");
			error.stack = "Error: 测试错误\n    at test.ts:10:15";

			logger.error({ error: error, context: "test" }, "发生错误");

			expect(mockPino.error).toHaveBeenCalledWith(
				expect.objectContaining({
					context: "test",
					err: {
						name: "Error",
						message: "测试错误",
						stack: expect.stringContaining("Error: 测试错误"),
					},
				}),
				"发生错误",
			);
		});

		it("应该处理自定义错误类型", () => {
			class CustomError extends Error {
				constructor(
					message: string,
					public code: string,
				) {
					super(message);
					this.name = "CustomError";
				}
			}

			const error = new CustomError("自定义错误", "ERR_CUSTOM");
			logger.error({ err: error }, "自定义错误发生");

			expect(mockPino.error).toHaveBeenCalledWith(
				expect.objectContaining({
					err: {
						name: "CustomError",
						message: "自定义错误",
						stack: expect.any(String),
					},
				}),
				"自定义错误发生",
			);
		});

		it("应该处理无堆栈的错误", () => {
			const error = new Error("无堆栈错误");
			delete (error as any).stack;

			logger.error({ err: error }, "错误");

			expect(mockPino.error).toHaveBeenCalledWith(
				expect.objectContaining({
					err: {
						name: "Error",
						message: "无堆栈错误",
						stack: undefined,
					},
				}),
				"错误",
			);
		});
	});

	describe("子日志记录器", () => {
		it("应该创建子日志记录器", () => {
			const childLogger = logger.child({ requestId: "req-123" });

			expect(childLogger).toBeInstanceOf(PinoLogger);
			expect(mockPino.child).toHaveBeenCalledWith({ requestId: "req-123" });
		});

		it("子日志记录器应该继承父日志记录器的上下文", () => {
			const mockChildPino = {
				debug: vi.fn(),
				info: vi.fn(),
				warn: vi.fn(),
				error: vi.fn(),
				child: vi.fn().mockReturnThis(),
			} as any;

			(mockPino.child as any).mockReturnValue(mockChildPino);

			const childLogger = logger.child({ requestId: "req-123" });
			childLogger.info("子日志记录器消息");

			expect(mockPino.child).toHaveBeenCalledWith({ requestId: "req-123" });
			expect(mockChildPino.info).toHaveBeenCalledWith("子日志记录器消息");
		});

		it("应该支持多层嵌套子日志记录器", () => {
			const mockChildPino1 = {
				debug: vi.fn(),
				info: vi.fn(),
				warn: vi.fn(),
				error: vi.fn(),
				child: vi.fn(),
			} as any;

			const mockChildPino2 = {
				debug: vi.fn(),
				info: vi.fn(),
				warn: vi.fn(),
				error: vi.fn(),
				child: vi.fn(),
			} as any;

			(mockPino.child as any).mockReturnValue(mockChildPino1);
			(mockChildPino1.child as any).mockReturnValue(mockChildPino2);

			const childLogger1 = logger.child({ requestId: "req-123" });
			const childLogger2 = childLogger1.child({ userId: "user-456" });

			childLogger2.info("嵌套子日志");

			expect(mockPino.child).toHaveBeenCalledWith({ requestId: "req-123" });
			expect(mockChildPino1.child).toHaveBeenCalledWith({ userId: "user-456" });
			expect(mockChildPino2.info).toHaveBeenCalledWith("嵌套子日志");
		});

		it("应该支持空绑定创建子日志记录器", () => {
			const childLogger = logger.child({});

			expect(childLogger).toBeInstanceOf(PinoLogger);
			expect(mockPino.child).toHaveBeenCalledWith({});
		});
	});

	describe("日志级别", () => {
		it("应该按级别记录不同消息", () => {
			logger.debug("调试消息");
			logger.info("信息消息");
			logger.warn("警告消息");
			logger.error("错误消息");

			expect(mockPino.debug).toHaveBeenCalledWith("调试消息");
			expect(mockPino.info).toHaveBeenCalledWith("信息消息");
			expect(mockPino.warn).toHaveBeenCalledWith("警告消息");
			expect(mockPino.error).toHaveBeenCalledWith("错误消息");
		});

		it("应该支持混合使用字符串和对象", () => {
			logger.debug("调试字符串");
			logger.info({ key: "value" }, "信息对象");
			logger.warn("警告字符串");
			logger.error({ err: new Error("错误") }, "错误对象");

			expect(mockPino.debug).toHaveBeenCalledWith("调试字符串");
			expect(mockPino.info).toHaveBeenCalledWith({ key: "value" }, "信息对象");
			expect(mockPino.warn).toHaveBeenCalledWith("警告字符串");
			expect(mockPino.error).toHaveBeenCalledWith(expect.any(Object), "错误对象");
		});
	});

	describe("对象清理", () => {
		it("应该创建对象副本（避免修改原对象）", () => {
			const originalObj = { userId: "123", action: "login" };
			logger.info(originalObj, "用户登录");

			// 验证调用参数不是原对象（是副本）
			const callArgs = (mockPino.info as any).mock.calls[0][0];
			expect(callArgs).not.toBe(originalObj);
			expect(callArgs).toEqual(originalObj);
		});

		it("应该处理嵌套对象", () => {
			const obj = {
				user: { id: "123", name: "John" },
				request: { url: "/api/test", method: "GET" },
			};

			logger.info(obj, "嵌套对象日志");

			expect(mockPino.info).toHaveBeenCalledWith(expect.objectContaining(obj), "嵌套对象日志");
		});

		it("应该处理数组", () => {
			const obj = { items: [1, 2, 3], tags: ["a", "b", "c"] };

			logger.info(obj, "数组日志");

			expect(mockPino.info).toHaveBeenCalledWith(expect.objectContaining(obj), "数组日志");
		});
	});

	describe("边界条件", () => {
		it("应该处理空字符串", () => {
			logger.debug("");
			logger.info("");
			logger.warn("");
			logger.error("");

			expect(mockPino.debug).toHaveBeenCalledWith("");
			expect(mockPino.info).toHaveBeenCalledWith("");
			expect(mockPino.warn).toHaveBeenCalledWith("");
			expect(mockPino.error).toHaveBeenCalledWith("");
		});

		it("应该处理空对象", () => {
			logger.info({}, "空对象");

			expect(mockPino.info).toHaveBeenCalledWith({}, "空对象");
		});

		it("应该处理超长字符串", () => {
			const longMessage = "x".repeat(10000);
			logger.info(longMessage);

			expect(mockPino.info).toHaveBeenCalledWith(longMessage);
		});

		it("应该处理特殊字符", () => {
			const message = "包含特殊字符: \n\t\"引号\" '单引号' \\ 反斜杠";
			logger.info(message);

			expect(mockPino.info).toHaveBeenCalledWith(message);
		});

		it("应该处理 undefined 值", () => {
			const obj = { key: undefined, value: "test" };
			logger.info(obj, "包含 undefined");

			expect(mockPino.info).toHaveBeenCalledWith(
				expect.objectContaining({ key: undefined, value: "test" }),
				"包含 undefined",
			);
		});

		it("应该处理 null 值", () => {
			const obj = { key: null, value: "test" };
			logger.info(obj, "包含 null");

			expect(mockPino.info).toHaveBeenCalledWith(
				expect.objectContaining({ key: null, value: "test" }),
				"包含 null",
			);
		});

		it("应该处理 NaN 和 Infinity", () => {
			const obj = { nan: NaN, infinity: Infinity, negInfinity: -Infinity };
			logger.info(obj, "包含特殊数值");

			expect(mockPino.info).toHaveBeenCalledWith(expect.objectContaining(obj), "包含特殊数值");
		});
	});

	describe("实际使用场景", () => {
		it("应该记录 HTTP 请求", () => {
			const requestLogger = logger.child({ requestId: "req-123" });
			requestLogger.info(
				{ method: "GET", url: "/api/users", status: 200, duration: 150 },
				"HTTP 请求",
			);

			expect(mockPino.child).toHaveBeenCalledWith({ requestId: "req-123" });
		});

		it("应该记录数据库操作", () => {
			logger.debug({ query: "SELECT * FROM users", duration: 25 }, "数据库查询");

			expect(mockPino.debug).toHaveBeenCalledWith(
				expect.objectContaining({ query: "SELECT * FROM users", duration: 25 }),
				"数据库查询",
			);
		});

		it("应该记录浏览器操作", () => {
			const browserLogger = logger.child({ dirId: "test-dir-1" });
			browserLogger.info({ operation: "goto", url: "https://example.com" }, "页面导航");

			expect(mockPino.child).toHaveBeenCalledWith({ dirId: "test-dir-1" });
		});

		it("应该记录错误恢复", () => {
			const error = new Error("Network timeout");
			logger.warn({ err: error, attempt: 2, maxAttempts: 3 }, "重试中");

			expect(mockPino.warn).toHaveBeenCalledWith(
				expect.objectContaining({
					attempt: 2,
					maxAttempts: 3,
				}),
				"重试中",
			);
		});

		it("应该记录性能指标", () => {
			logger.info(
				{
					operation: "processData",
					itemsProcessed: 1000,
					duration: 5000,
					avgLatency: 5,
				},
				"批处理完成",
			);

			expect(mockPino.info).toHaveBeenCalledWith(
				expect.objectContaining({
					operation: "processData",
					itemsProcessed: 1000,
					duration: 5000,
					avgLatency: 5,
				}),
				"批处理完成",
			);
		});
	});

	describe("并发场景", () => {
		it("应该支持并发日志记录", () => {
			const promises = Array.from({ length: 100 }, (_, i) =>
				logger.info({ index: i }, `并发消息 ${i}`),
			);

			Promise.all(promises);

			expect(mockPino.info).toHaveBeenCalledTimes(100);
		});

		it("应该支持多个子日志记录器并发", () => {
			const mockChildPino = {
				info: vi.fn(),
				debug: vi.fn(),
				warn: vi.fn(),
				error: vi.fn(),
				child: vi.fn(),
			} as any;

			(mockPino.child as any).mockReturnValue(mockChildPino);

			const childLoggers = Array.from({ length: 10 }, (_, i) =>
				logger.child({ requestId: `req-${i}` }),
			);

			childLoggers.forEach((childLogger, i) => {
				childLogger.info(`请求 ${i} 完成`);
			});

			expect(mockPino.child).toHaveBeenCalledTimes(10);
			expect(mockChildPino.info).toHaveBeenCalledTimes(10);
		});
	});
});
