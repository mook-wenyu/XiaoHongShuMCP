import { BaseError } from "./BaseError.js";

/**
 * 验证错误
 *
 * 表示输入参数验证失败、数据格式错误等验证相关错误。
 * 默认不可重试（需要修正输入数据）。
 *
 * @remarks
 * 典型场景：
 * - 参数类型错误
 * - 必填字段缺失
 * - 格式不符合要求
 * - 值超出允许范围
 *
 * @example
 * ```typescript
 * throw new ValidationError('参数验证失败', {
 *   field: 'email',
 *   value: 'invalid-email',
 *   expected: 'valid email format'
 * });
 * ```
 */
export class ValidationError extends BaseError {
	readonly code = "VALIDATION_ERROR";
	readonly retryable = false;

	/**
	 * 构造函数
	 * @param message 错误消息
	 * @param context 上下文信息（field、value、expected 等）
	 */
	constructor(message: string, context?: Record<string, unknown>) {
		super(message, context);
	}

	/**
	 * 验证失败的字段名
	 */
	get field(): string | undefined {
		return this.context?.field as string | undefined;
	}

	/**
	 * 实际值
	 */
	get value(): unknown {
		return this.context?.value;
	}

	/**
	 * 期望值或格式
	 */
	get expected(): string | undefined {
		return this.context?.expected as string | undefined;
	}
}
