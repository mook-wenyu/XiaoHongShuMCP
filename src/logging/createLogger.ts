import pino from "pino";
import { PinoLogger } from "./PinoLogger.js";
import type { ILogger } from "../contracts/ILogger.js";

/**
 * 创建日志记录器
 *
 * 工厂函数，创建配置好的 ILogger 实例。
 * 支持环境变量配置日志级别和格式。
 *
 * @remarks
 * 环境变量：
 * - LOG_LEVEL: 日志级别（debug、info、warn、error），默认 info
 * - LOG_PRETTY: 是否使用 pretty 格式（true/false），默认 false
 * - MCP_LOG_STDERR: MCP 模式可将日志重定向到 stderr（true/false），默认 false
 *
 * 配置特性：
 * - 自动脱敏：token、密码等敏感字段
 * - 开发环境：pino-pretty 格式化输出（彩色、时间戳）
 * - 生产环境：JSON 格式输出（便于日志收集）
 * - MCP 模式：建议静默或输出到 stderr（避免污染 stdio 传输通道）
 *
 * @param options - 可选配置
 * @param options.useSilent - 是否静默模式（完全禁用日志），用于 MCP 服务器
 * @param options.toStderr - 非 pretty 模式下将输出重定向至 stderr（fd=2）
 * @returns ILogger 实例
 *
 * @example
 * ```typescript
 * // 创建日志记录器
 * const logger = createLogger();
 * logger.info('应用启动');
 *
 * // MCP 模式（静默）
 * const mcpLogger = createLogger({ useSilent: true });
 * mcpLogger.info('此消息不会输出');
 *
 * // MCP 模式（调试，将日志重定向 stderr）
 * const mcpDebugLogger = createLogger({ toStderr: true });
 * mcpDebugLogger.info('stderr 日志，不污染 stdout');
 * ```
 */
export function createLogger(options?: { useSilent?: boolean; toStderr?: boolean }): ILogger {
	// MCP 模式可选择静默，避免污染 stdio 通道
	if (options?.useSilent) {
		const pinoInstance = pino({ level: "silent" });
		return new PinoLogger(pinoInstance);
	}

	const pretty = process.env.LOG_PRETTY === "true";
	const level = process.env.LOG_LEVEL || "info";

	// 允许通过选项或环境变量将日志输出至 stderr（fd=2）
	const toStderr = options?.toStderr === true || process.env.MCP_LOG_STDERR === "true";

	// 注意：为避免与 pretty transport 冲突，toStderr 仅在非 pretty 模式生效
	if (toStderr && !pretty) {
		const pinoInstance = pino(
			{
				level,
				redact: { paths: ["req.headers.token", "headers.token", "token"], censor: "[Redacted]" },
			},
			// fd=2 → stderr
			pino.destination(2)
		);
		return new PinoLogger(pinoInstance);
	}

	const pinoInstance = pino({
		level,
		redact: { paths: ["req.headers.token", "headers.token", "token"], censor: "[Redacted]" },
		transport: pretty
			? {
					target: "pino-pretty",
					options: { colorize: true, translateTime: "SYS:standard" },
			  }
			: undefined,
	});

	return new PinoLogger(pinoInstance);
}

/**
 * @deprecated 使用 createLogger() 代替
 *
 * 全局日志记录器单例，保留以保持向后兼容。
 * 未来版本将删除，请使用 createLogger() 创建日志记录器实例。
 *
 * @example
 * ```typescript
 * // 旧代码
 * import { logger } from './logging/index.js';
 * logger.info('消息');
 *
 * // 新代码
 * import { createLogger } from './logging/index.js';
 * const logger = createLogger();
 * logger.info('消息');
 * ```
 */
export const logger = createLogger();
