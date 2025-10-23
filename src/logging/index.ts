/**
 * 日志管理模块
 *
 * 提供统一的日志记录功能，支持上下文绑定（child logger）。
 * 基于 pino 实现，提供高性能异步日志写入。
 *
 * @remarks
 * 核心特性：
 * - ILogger 接口：统一的日志记录契约
 * - PinoLogger 实现：基于 pino 的高性能日志记录
 * - createLogger 工厂：创建配置好的日志记录器
 * - child logger：支持上下文绑定，便于请求追踪
 * - 自动脱敏：敏感字段自动脱敏（token等）
 * - 环境适配：开发环境 pretty 格式，生产环境 JSON 格式
 *
 * @packageDocumentation
 */

export { PinoLogger } from "./PinoLogger.js";
export { createLogger, logger } from "./createLogger.js";
