/**
 * 基础错误抽象类
 *
 * 所有业务错误的基类，提供统一的错误处理和序列化机制。
 *
 * @remarks
 * 设计特性：
 * - 错误代码：唯一标识错误类型
 * - 可重试标记：指示错误是否可重试
 * - 上下文信息：附加调试和日志所需的上下文数据
 * - 堆栈跟踪：正确捕获错误堆栈
 * - JSON 序列化：便于日志记录和传输
 *
 * @example
 * ```typescript
 * class MyError extends BaseError {
 *   readonly code = 'MY_ERROR';
 *   readonly retryable = false;
 * }
 *
 * throw new MyError('发生错误', { userId: '123' });
 * ```
 */
export abstract class BaseError extends Error {
	/** 错误代码（唯一标识） */
	abstract readonly code: string;

	/** 是否可重试 */
	abstract readonly retryable: boolean;

	/**
	 * 构造函数
	 * @param message 错误消息
	 * @param context 上下文信息（用于调试和日志）
	 */
	constructor(
		message: string,
		public readonly context?: Record<string, unknown>,
	) {
		super(message);
		this.name = this.constructor.name;
		// 正确捕获堆栈跟踪
		Error.captureStackTrace?.(this, this.constructor);
	}

	/**
	 * 序列化为 JSON（便于日志记录）
	 * @returns JSON 对象
	 */
	toJSON(): object {
		return {
			name: this.name,
			code: this.code,
			message: this.message,
			retryable: this.retryable,
			context: this.context,
			stack: this.stack,
		};
	}
}
