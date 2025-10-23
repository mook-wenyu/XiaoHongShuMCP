/**
 * Roxy API 连接相关 Zod Schema 定义
 *
 * 提供运行时验证和类型推断能力。
 *
 * @remarks
 * 连接相关 Schema 涵盖窗口打开、关闭、连接信息查询等操作的验证。
 *
 * @packageDocumentation
 */

import { z } from "zod";
import { ApiResponseSchema } from "./common.js";

/**
 * 打开窗口请求参数 Schema
 */
export const OpenRequestSchema = z.object({
	dirId: z.string().min(1).describe("窗口 ID（必需）"),
	workspaceId: z.union([z.number(), z.string()]).optional().describe("工作空间 ID（可选）"),
	args: z.array(z.string()).optional().describe("启动参数（可选）"),
});

/**
 * 从 Schema 推断的打开窗口请求类型
 */
export type OpenRequest = z.infer<typeof OpenRequestSchema>;

/**
 * 窗口连接信息 Schema
 *
 * @remarks
 * id 字段可选，因为某些 Roxy API 端点（如 /browser/open）可能不返回 id
 */
export const ConnectionInfoSchema = z.object({
	id: z.string().min(1).optional().describe("窗口 ID（可选）"),
	ws: z.string().min(1).describe("WebSocket 端点"),
	http: z.string().optional().describe("HTTP 端点"),
});

/**
 * 从 Schema 推断的连接信息类型
 */
export type ConnectionInfo = z.infer<typeof ConnectionInfoSchema>;

/**
 * 打开窗口响应 Schema
 *
 * @remarks
 * data 字段可以为 null（当打开失败时）
 */
export const OpenResponseSchema = ApiResponseSchema(ConnectionInfoSchema.nullable());

/**
 * 从 Schema 推断的打开窗口响应类型
 */
export type OpenResponse = z.infer<typeof OpenResponseSchema>;

/**
 * 关闭窗口响应 Schema
 */
export const CloseResponseSchema = ApiResponseSchema(z.null());

/**
 * 从 Schema 推断的关闭窗口响应类型
 */
export type CloseResponse = z.infer<typeof CloseResponseSchema>;

/**
 * 连接信息查询响应 Schema
 *
 * @remarks
 * data 字段可以为 null（当查询的窗口不存在时）
 */
export const ConnectionInfoResponseSchema = ApiResponseSchema(
	z.array(ConnectionInfoSchema).nullable()
);

/**
 * 从 Schema 推断的连接信息查询响应类型
 */
export type ConnectionInfoResponse = z.infer<typeof ConnectionInfoResponseSchema>;
