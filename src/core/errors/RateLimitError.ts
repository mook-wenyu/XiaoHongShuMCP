import { BusinessError } from "./BusinessError.js";

/**
 * 限流错误
 *
 * 表示请求频率超过限制，触发了速率限制或熔断策略。
 * 继承自 BusinessError，但覆盖 retryable 为 true（等待后可重试）。
 *
 * @remarks
 * 典型场景：
 * - QPS 限制触发
 * - 熔断器开启
 * - API 速率限制
 *
 * @example
 * ```typescript
 * throw new RateLimitError('请求过于频繁，请稍后重试', {
 *   key: 'dirId123',
 *   qps: 5,
 *   retryAfter: 15000
 * });
 * ```
 */
export class RateLimitError extends BusinessError {
	readonly code = "RATE_LIMIT";
	readonly retryable = true; // 覆盖父类默认值，限流错误可重试

	/**
	 * 构造函数
	 * @param message 错误消息（默认提示）
	 * @param context 上下文信息（key、qps、retryAfter 等）
	 */
	constructor(
		message: string = "请求频率超限，请稍后重试",
		context?: Record<string, unknown>
	) {
		super(message, context);
	}

	/**
	 * 限流 key（通常是 dirId 或用户标识）
	 */
	get key(): string | undefined {
		return this.context?.key as string | undefined;
	}

	/**
	 * QPS 限制
	 */
	get qps(): number | undefined {
		return this.context?.qps as number | undefined;
	}

	/**
	 * 建议重试等待时间（毫秒）
	 */
	get retryAfter(): number | undefined {
		return this.context?.retryAfter as number | undefined;
	}
}
