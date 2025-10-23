import { BaseError } from "./BaseError.js";

/**
 * 浏览器操作错误
 *
 * 表示浏览器自动化操作失败，如元素未找到、操作超时等。
 * 默认可重试（适用于页面加载延迟等瞬态问题）。
 *
 * @remarks
 * 典型场景：
 * - 元素定位失败
 * - 页面加载超时
 * - CDP 连接中断
 * - Playwright 操作异常
 *
 * @example
 * ```typescript
 * throw new BrowserError('元素未找到', {
 *   dirId: 'dir123',
 *   operation: 'click',
 *   selector: '.submit-button',
 *   timeout: 30000
 * });
 * ```
 */
export class BrowserError extends BaseError {
	readonly code = "BROWSER_ERROR";
	readonly retryable = true;

	/**
	 * 构造函数
	 * @param message 错误消息
	 * @param context 上下文信息（dirId、operation、selector 等）
	 */
	constructor(message: string, context?: Record<string, unknown>) {
		super(message, context);
	}

	/**
	 * 窗口标识符
	 */
	get dirId(): string | undefined {
		return this.context?.dirId as string | undefined;
	}

	/**
	 * 操作类型（click、type、goto 等）
	 */
	get operation(): string | undefined {
		return this.context?.operation as string | undefined;
	}

	/**
	 * 选择器（如果适用）
	 */
	get selector(): string | undefined {
		return this.context?.selector as string | undefined;
	}
}
