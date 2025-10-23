import type { Logger } from "pino";
import type { ILogger } from "../contracts/ILogger.js";

/**
 * Pino 日志记录器实现
 *
 * 封装 pino 实例，实现 ILogger 接口。
 * 支持子日志记录器（child logger）用于上下文追踪。
 *
 * @remarks
 * 特性：
 * - 自动处理 Error 对象，提取堆栈信息
 * - 支持上下文绑定（child logger）
 * - 异步日志写入，避免阻塞主线程
 *
 * @example
 * ```typescript
 * const logger = new PinoLogger(pinoInstance);
 * logger.info({ userId: '123' }, '用户登录');
 *
 * const requestLogger = logger.child({ requestId: 'req-123' });
 * requestLogger.info('处理请求');
 * ```
 */
export class PinoLogger implements ILogger {
	/**
	 * 构造函数
	 * @param pino pino 实例
	 */
	constructor(private pino: Logger) {}

	/**
	 * 调试级别日志
	 */
	debug(obj: Record<string, unknown> | string, msg?: string): void {
		if (typeof obj === "string") {
			this.pino.debug(obj);
		} else {
			this.pino.debug(this.sanitizeObj(obj), msg);
		}
	}

	/**
	 * 信息级别日志
	 */
	info(obj: Record<string, unknown> | string, msg?: string): void {
		if (typeof obj === "string") {
			this.pino.info(obj);
		} else {
			this.pino.info(this.sanitizeObj(obj), msg);
		}
	}

	/**
	 * 警告级别日志
	 */
	warn(obj: Record<string, unknown> | string, msg?: string): void {
		if (typeof obj === "string") {
			this.pino.warn(obj);
		} else {
			this.pino.warn(this.sanitizeObj(obj), msg);
		}
	}

	/**
	 * 错误级别日志（特殊处理 Error 对象）
	 */
	error(obj: Record<string, unknown> | string, msg?: string): void {
		if (typeof obj === "string") {
			this.pino.error(obj);
		} else {
			// 特殊处理 Error 对象，提取堆栈信息
			const sanitized = this.sanitizeObj(obj);
			if (obj.err instanceof Error || obj.error instanceof Error) {
				const err = (obj.err || obj.error) as Error;
				sanitized.err = {
					name: err.name,
					message: err.message,
					stack: err.stack,
				};
			}
			this.pino.error(sanitized, msg);
		}
	}

	/**
	 * 创建子日志记录器（绑定上下文）
	 * @param bindings 上下文绑定数据
	 * @returns 新的日志记录器实例
	 */
	child(bindings: Record<string, unknown>): ILogger {
		const childPino = this.pino.child(bindings);
		return new PinoLogger(childPino);
	}

	/**
	 * 清理日志对象（移除循环引用等）
	 * @param obj 原始对象
	 * @returns 清理后的对象
	 * @private
	 */
	private sanitizeObj(obj: Record<string, unknown>): Record<string, unknown> {
		// pino 已经处理了大部分序列化问题，这里只做简单处理
		return { ...obj };
	}
}
