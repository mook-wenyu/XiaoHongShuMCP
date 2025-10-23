import { BaseError } from "./BaseError.js";

/**
 * 业务错误基类
 *
 * 表示业务逻辑层面的错误，通常由业务规则违反或状态不满足条件引起。
 * 默认不可重试（需要业务层面处理或用户干预）。
 *
 * @remarks
 * 典型场景：
 * - 会话过期
 * - 权限不足
 * - 资源不存在
 * - 业务规则违反
 *
 * 子类必须定义自己的 code 和 retryable 属性。
 *
 * @example
 * ```typescript
 * class PermissionDeniedError extends BusinessError {
 *   readonly code = 'PERMISSION_DENIED';
 *   readonly retryable = false;
 * }
 *
 * throw new PermissionDeniedError('无权访问此资源', {
 *   userId: '123',
 *   resource: '/api/admin'
 * });
 * ```
 */
export abstract class BusinessError extends BaseError {
	/**
	 * 构造函数
	 * @param message 错误消息
	 * @param context 上下文信息
	 */
	constructor(message: string, context?: Record<string, unknown>) {
		super(message, context);
	}
}
