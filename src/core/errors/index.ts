/**
 * 错误体系（Error Hierarchy）
 *
 * 分层错误体系，提供统一的错误处理和序列化机制。
 *
 * @remarks
 * 错误层次：
 * - BaseError: 抽象基类
 *   - NetworkError: 网络错误（可重试）
 *   - ValidationError: 验证错误（不可重试）
 *   - BrowserError: 浏览器操作错误（可重试）
 *   - BusinessError: 业务错误基类（不可重试）
 *     - SessionExpiredError: 会话过期错误
 *     - RateLimitError: 限流错误（可重试）
 *
 * 所有错误支持：
 * - code: 唯一错误代码
 * - retryable: 是否可重试标记
 * - context: 上下文信息
 * - toJSON(): JSON 序列化
 *
 * @packageDocumentation
 */

export { BaseError } from "./BaseError.js";
export { NetworkError } from "./NetworkError.js";
export { ValidationError } from "./ValidationError.js";
export { BrowserError } from "./BrowserError.js";
export { BusinessError } from "./BusinessError.js";
export { SessionExpiredError } from "./SessionExpiredError.js";
export { RateLimitError } from "./RateLimitError.js";
