/**
 * Roxy API 连接相关类型定义
 *
 * 包括窗口打开、关闭、连接信息查询等类型。
 *
 * @remarks
 * 连接相关 API 涉及浏览器窗口的生命周期管理和 WebSocket/HTTP 连接信息。
 * 类型直接从 Zod Schema 推断，确保运行时验证和类型定义一致。
 *
 * @packageDocumentation
 */

import {
	OpenRequestSchema,
	OpenResponseSchema,
	CloseResponseSchema,
	ConnectionInfoSchema,
	ConnectionInfoResponseSchema,
} from "../../schemas/roxy/connection.js";
import type { z } from "zod";

/**
 * 打开窗口请求参数
 *
 * @remarks
 * dirId 是必需的，workspaceId 和 args 是可选的。
 */
export type OpenRequest = z.infer<typeof OpenRequestSchema>;

/**
 * 窗口连接信息
 *
 * @remarks
 * 包含窗口的 WebSocket 和 HTTP 端点信息。
 */
export type ConnectionInfo = z.infer<typeof ConnectionInfoSchema>;

/**
 * 打开窗口响应
 *
 * @remarks
 * 返回打开的窗口连接信息，data 可能为 null（打开失败时）。
 */
export type OpenResponse = z.infer<typeof OpenResponseSchema>;

/**
 * 关闭窗口响应
 *
 * @remarks
 * 关闭成功时，data 为 null。
 */
export type CloseResponse = z.infer<typeof CloseResponseSchema>;

/**
 * 连接信息查询响应
 *
 * @remarks
 * 返回多个窗口的连接信息数组，data 可能为 null（查询失败或窗口不存在时）。
 */
export type ConnectionInfoResponse = z.infer<typeof ConnectionInfoResponseSchema>;
