/* 中文注释：浏览器操作错误测试 */
import { describe, it, expect } from "vitest";
import { BrowserError } from "../../../src/core/errors/BrowserError.js";

describe("BrowserError 错误路径测试", () => {
	describe("错误创建和序列化", () => {
		it("应该正确创建浏览器错误", () => {
			const error = new BrowserError("Element not found", {
				dirId: "test-dir-123",
				operation: "click",
				selector: "#submit-button",
			});

			expect(error).toBeInstanceOf(Error);
			expect(error).toBeInstanceOf(BrowserError);
			expect(error.message).toBe("Element not found");
			expect(error.code).toBe("BROWSER_ERROR");
			expect(error.retryable).toBe(true);
			expect(error.context).toEqual({
				dirId: "test-dir-123",
				operation: "click",
				selector: "#submit-button",
			});
		});

		it("应该正确序列化为 JSON", () => {
			const error = new BrowserError("Page navigation timeout", {
				dirId: "dir-456",
				operation: "goto",
				url: "https://example.com",
				timeout: 30000,
			});

			const json = error.toJSON();

			expect(json).toMatchObject({
				name: "BrowserError",
				code: "BROWSER_ERROR",
				message: "Page navigation timeout",
				retryable: true,
				context: {
					dirId: "dir-456",
					operation: "goto",
					url: "https://example.com",
					timeout: 30000,
				},
			});
			expect(json).toHaveProperty("stack");
		});
	});

	describe("浏览器操作错误", () => {
		it("应该处理元素定位失败", () => {
			const error = new BrowserError("Selector not found", {
				dirId: "test-dir",
				operation: "click",
				selector: ".non-existent-class",
				searchedIn: "body",
			});

			expect(error.context?.selector).toBe(".non-existent-class");
			expect(error.context?.operation).toBe("click");
		});

		it("应该处理页面导航超时", () => {
			const error = new BrowserError("Navigation timeout", {
				dirId: "test-dir",
				operation: "goto",
				url: "https://slow-website.com",
				timeout: 30000,
				elapsed: 30500,
			});

			expect(error.context?.url).toBe("https://slow-website.com");
			expect(error.context?.timeout).toBe(30000);
			expect(error.context?.elapsed).toBe(30500);
		});

		it("应该处理元素不可见", () => {
			const error = new BrowserError("Element not visible", {
				dirId: "test-dir",
				operation: "click",
				selector: "#hidden-button",
				visibility: "hidden",
			});

			expect(error.context?.visibility).toBe("hidden");
		});

		it("应该处理元素不可交互", () => {
			const error = new BrowserError("Element not interactable", {
				dirId: "test-dir",
				operation: "type",
				selector: "input[disabled]",
				reason: "Element is disabled",
			});

			expect(error.context?.reason).toBe("Element is disabled");
		});
	});

	describe("页面操作错误", () => {
		it("应该处理页面加载失败", () => {
			const error = new BrowserError("Page load failed", {
				dirId: "test-dir",
				operation: "goto",
				url: "https://nonexistent.example.com",
				networkStatus: "DNS_FAILED",
			});

			expect(error.context?.networkStatus).toBe("DNS_FAILED");
		});

		it("应该处理截图失败", () => {
			const error = new BrowserError("Screenshot failed", {
				dirId: "test-dir",
				operation: "screenshot",
				path: "/invalid/path/screenshot.png",
				reason: "Invalid path",
			});

			expect(error.context?.path).toBe("/invalid/path/screenshot.png");
			expect(error.context?.reason).toBe("Invalid path");
		});

		it("应该处理脚本执行失败", () => {
			const error = new BrowserError("Script execution failed", {
				dirId: "test-dir",
				operation: "evaluate",
				script: "document.querySelector('.invalid').click()",
				jsError: "Cannot read property 'click' of null",
			});

			expect(error.context?.script).toBe("document.querySelector('.invalid').click()");
			expect(error.context?.jsError).toBe("Cannot read property 'click' of null");
		});

		it("应该处理文件上传失败", () => {
			const error = new BrowserError("File upload failed", {
				dirId: "test-dir",
				operation: "setInputFiles",
				selector: "input[type=file]",
				files: ["/path/to/file1.pdf", "/path/to/file2.jpg"],
				reason: "File not found",
			});

			expect(error.context?.files).toEqual(["/path/to/file1.pdf", "/path/to/file2.jpg"]);
			expect(error.context?.reason).toBe("File not found");
		});
	});

	describe("浏览器上下文错误", () => {
		it("应该处理上下文关闭错误", () => {
			const error = new BrowserError("Context already closed", {
				dirId: "test-dir",
				operation: "newPage",
				contextState: "closed",
			});

			expect(error.context?.contextState).toBe("closed");
		});

		it("应该处理浏览器断开连接", () => {
			const error = new BrowserError("Browser disconnected", {
				dirId: "test-dir",
				operation: "click",
				connectionStatus: "disconnected",
				lastHeartbeat: new Date().toISOString(),
			});

			expect(error.context?.connectionStatus).toBe("disconnected");
			expect(error.context?.lastHeartbeat).toBeDefined();
		});

		it("应该处理 CDP 连接失败", () => {
			const error = new BrowserError("CDP connection failed", {
				dirId: "test-dir",
				operation: "connect",
				wsEndpoint: "ws://localhost:9222/devtools/browser/abc-123",
				errorCode: "ECONNREFUSED",
			});

			expect(error.context?.wsEndpoint).toContain("ws://localhost:9222");
			expect(error.context?.errorCode).toBe("ECONNREFUSED");
		});
	});

	describe("复杂场景错误", () => {
		it("应该处理多帧操作错误", () => {
			const error = new BrowserError("Frame not found", {
				dirId: "test-dir",
				operation: "frameLocator",
				frameSelector: "iframe#nested-frame",
				parentFrame: "iframe#main-frame",
			});

			expect(error.context?.frameSelector).toBe("iframe#nested-frame");
			expect(error.context?.parentFrame).toBe("iframe#main-frame");
		});

		it("应该处理弹窗操作错误", () => {
			const error = new BrowserError("Dialog not found", {
				dirId: "test-dir",
				operation: "dialog.accept",
				dialogType: "confirm",
				reason: "No dialog present",
			});

			expect(error.context?.dialogType).toBe("confirm");
			expect(error.context?.reason).toBe("No dialog present");
		});

		it("应该处理键盘操作错误", () => {
			const error = new BrowserError("Keyboard action failed", {
				dirId: "test-dir",
				operation: "keyboard.press",
				key: "Enter",
				modifiers: ["Control", "Shift"],
			});

			expect(error.context?.key).toBe("Enter");
			expect(error.context?.modifiers).toEqual(["Control", "Shift"]);
		});

		it("应该处理鼠标操作错误", () => {
			const error = new BrowserError("Mouse action failed", {
				dirId: "test-dir",
				operation: "mouse.click",
				x: 100,
				y: 200,
				button: "right",
			});

			expect(error.context?.x).toBe(100);
			expect(error.context?.y).toBe(200);
			expect(error.context?.button).toBe("right");
		});
	});

	describe("错误上下文信息", () => {
		it("应该包含 dirId 信息", () => {
			const error = new BrowserError("Operation failed", {
				dirId: "unique-dir-id-789",
			});

			expect(error.context?.dirId).toBe("unique-dir-id-789");
		});

		it("应该支持空上下文", () => {
			const error = new BrowserError("Generic browser error");

			expect(error.context).toBeUndefined();
			expect(error.toJSON().context).toBeUndefined();
		});

		it("应该支持丰富的上下文信息", () => {
			const error = new BrowserError("Complex operation failed", {
				dirId: "test-dir",
				operation: "complexAction",
				selector: "#target",
				url: "https://example.com/page",
				viewport: { width: 1920, height: 1080 },
				userAgent: "Mozilla/5.0...",
				timestamp: new Date().toISOString(),
			});

			expect(error.context).toMatchObject({
				dirId: "test-dir",
				operation: "complexAction",
				selector: "#target",
				url: "https://example.com/page",
				viewport: { width: 1920, height: 1080 },
			});
		});
	});

	describe("错误类型判断", () => {
		it("应该正确识别为可重试错误", () => {
			const error = new BrowserError("Temporary browser issue");

			expect(error.retryable).toBe(true);
		});

		it("应该与其他错误类型区分", () => {
			const browserError = new BrowserError("Browser error");
			const genericError = new Error("Generic error");

			expect(browserError).toBeInstanceOf(BrowserError);
			expect(genericError).not.toBeInstanceOf(BrowserError);
		});
	});

	describe("错误传播", () => {
		it("应该支持 try-catch 捕获", () => {
			expect(() => {
				throw new BrowserError("Test error");
			}).toThrow(BrowserError);

			expect(() => {
				throw new BrowserError("Test error");
			}).toThrow("Test error");
		});

		it("应该支持 async 错误捕获", async () => {
			const failingFunc = async () => {
				throw new BrowserError("Async browser error");
			};

			await expect(failingFunc()).rejects.toThrow(BrowserError);
			await expect(failingFunc()).rejects.toThrow("Async browser error");
		});
	});

	describe("边界条件", () => {
		it("应该处理空字符串消息", () => {
			const error = new BrowserError("");

			expect(error.message).toBe("");
			expect(error.code).toBe("BROWSER_ERROR");
		});

		it("应该处理超长选择器", () => {
			const longSelector = "div ".repeat(100) + "> span";
			const error = new BrowserError("Selector too complex", {
				dirId: "test",
				selector: longSelector,
			});

			expect(error.context?.selector).toBe(longSelector);
		});

		it("应该处理特殊字符", () => {
			const message = '操作失败: 选择器 "#id > .class" \n 包含特殊字符';
			const error = new BrowserError(message, {
				selector: "#id > .class[data-test='value']",
			});

			expect(error.message).toBe(message);
			expect(error.context?.selector).toBe("#id > .class[data-test='value']");
		});

		it("应该处理循环引用的上下文", () => {
			const context: any = { dirId: "test" };
			context.self = context;

			const error = new BrowserError("Circular reference", context);

			expect(() => error.toJSON()).not.toThrow();
		});
	});
});
