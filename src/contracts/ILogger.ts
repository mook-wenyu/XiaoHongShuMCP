/**
 * 日志记录器接口
 *
 * 封装日志功能，支持不同日志级别和上下文绑定（child logger）。
 *
 * @remarks
 * 特性：
 * - 支持 debug、info、warn、error 四个日志级别
 * - 支持上下文绑定（child logger）用于请求追踪
 * - 自动序列化 Error 对象
 *
 * @example
 * ```typescript
 * const logger: ILogger = createLogger();
 *
 * // 基本使用
 * logger.info({ userId: '123' }, '用户登录');
 * logger.error({ err: new Error('失败') }, '操作失败');
 *
 * // 上下文绑定
 * const requestLogger = logger.child({ requestId: 'req-123' });
 * requestLogger.info('处理请求'); // 自动包含 requestId
 * ```
 */
export interface ILogger {
	/**
	 * 调试级别日志
	 * @param obj 结构化数据
	 * @param msg 日志消息
	 */
	debug(obj: Record<string, unknown>, msg?: string): void;
	debug(msg: string): void;

	/**
	 * 信息级别日志
	 * @param obj 结构化数据
	 * @param msg 日志消息
	 */
	info(obj: Record<string, unknown>, msg?: string): void;
	info(msg: string): void;

	/**
	 * 警告级别日志
	 * @param obj 结构化数据
	 * @param msg 日志消息
	 */
	warn(obj: Record<string, unknown>, msg?: string): void;
	warn(msg: string): void;

	/**
	 * 错误级别日志
	 * @param obj 结构化数据（包含 Error 对象时自动提取堆栈）
	 * @param msg 日志消息
	 */
	error(obj: Record<string, unknown>, msg?: string): void;
	error(msg: string): void;

	/**
	 * 创建子日志记录器（绑定上下文）
	 * @param bindings 上下文绑定数据
	 * @returns 新的日志记录器实例
	 */
	child(bindings: Record<string, unknown>): ILogger;
}
