import { BusinessError } from "./BusinessError.js";

/**
 * 会话过期错误
 *
 * 表示用户会话已过期，需要重新登录。
 * 继承自 BusinessError，默认不可重试。
 *
 * @remarks
 * 典型场景：
 * - Cookies 过期
 * - 登录态失效
 * - Token 过期
 *
 * @example
 * ```typescript
 * throw new SessionExpiredError('登录会话已过期，请重新登录', {
 *   dirId: 'dir123',
 *   lastActiveTime: Date.now() - 3600000
 * });
 * ```
 */
export class SessionExpiredError extends BusinessError {
	readonly code = "SESSION_EXPIRED";
	readonly retryable = false;

	/**
	 * 构造函数
	 * @param message 错误消息（默认提示）
	 * @param context 上下文信息（dirId、lastActiveTime 等）
	 */
	constructor(message: string = "会话已过期，请重新登录", context?: Record<string, unknown>) {
		super(message, context);
	}
}
