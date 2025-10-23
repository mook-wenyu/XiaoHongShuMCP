import { BaseError } from "./BaseError.js";

/**
 * 网络错误
 *
 * 表示网络请求失败、超时、服务不可用等网络相关错误。
 * 默认可重试（适用于瞬态网络故障）。
 *
 * @remarks
 * 典型场景：
 * - HTTP 5xx 错误
 * - 网络超时
 * - 连接拒绝
 * - DNS 解析失败
 *
 * @example
 * ```typescript
 * throw new NetworkError('HTTP 503: 服务暂时不可用', {
 *   status: 503,
 *   url: 'https://api.example.com/endpoint',
 *   method: 'POST'
 * });
 * ```
 */
export class NetworkError extends BaseError {
	readonly code = "NETWORK_ERROR";
	readonly retryable = true;

	/**
	 * 构造函数
	 * @param message 错误消息
	 * @param context 上下文信息（status、url、method 等）
	 */
	constructor(message: string, context?: Record<string, unknown>) {
		super(message, context);
	}

	/**
	 * HTTP 状态码（如果适用）
	 */
	get status(): number | undefined {
		return this.context?.status as number | undefined;
	}

	/**
	 * 请求 URL（如果适用）
	 */
	get url(): string | undefined {
		return this.context?.url as string | undefined;
	}

	/**
	 * HTTP 方法（如果适用）
	 */
	get method(): string | undefined {
		return this.context?.method as string | undefined;
	}
}
