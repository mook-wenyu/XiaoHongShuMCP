/**
 * Roxy API 工作空间相关 Zod Schema 定义
 *
 * 提供运行时验证和类型推断能力。
 *
 * @remarks
 * 使用 Zod Schema 确保 API 响应数据的正确性：
 * - 类型验证：防止运行时类型错误
 * - 数据清洗：自动过滤无效字段
 * - 错误提示：提供友好的验证错误信息
 *
 * @packageDocumentation
 */

import { z } from "zod";
import {
	PaginatedParamsSchema,
	ApiResponseSchema,
	PaginatedResponseSchema,
} from "./common.js";

/**
 * 项目详情 Schema
 *
 * @remarks
 * 验证规则：
 * - projectId: 必须是数字
 * - projectName: 必须是非空字符串
 */
export const ProjectDetailSchema = z.object({
	projectId: z.number().describe("项目 ID"),
	projectName: z.string().min(1).describe("项目名称"),
});

/**
 * 从 Schema 推断的项目详情类型
 */
export type ProjectDetail = z.infer<typeof ProjectDetailSchema>;

/**
 * 工作空间信息 Schema
 *
 * @remarks
 * 验证规则：
 * - id: 必须是正整数
 * - workspaceName: 必须是非空字符串
 * - project_details: 必须是项目详情数组
 */
export const WorkspaceSchema = z.object({
	id: z.number().int().positive().describe("工作空间 ID（数字，如 28255）"),
	workspaceName: z.string().min(1).describe("工作空间名称"),
	project_details: z.array(ProjectDetailSchema).describe("项目详情列表"),
});

/**
 * 从 Schema 推断的工作空间类型
 */
export type Workspace = z.infer<typeof WorkspaceSchema>;

/**
 * 工作空间列表查询参数 Schema
 *
 * @remarks
 * 继承自 PaginatedParamsSchema，支持分页查询。
 */
export const WorkspaceListParamsSchema = PaginatedParamsSchema;

/**
 * 从 Schema 推断的工作空间列表查询参数类型
 */
export type WorkspaceListParams = z.infer<typeof WorkspaceListParamsSchema>;

/**
 * 工作空间列表响应 Schema
 *
 * @remarks
 * 完整响应结构验证：
 * ```typescript
 * {
 *   code: 0,
 *   msg: "success",
 *   data: {
 *     total: 5,
 *     rows: [Workspace, ...]
 *   }
 * }
 * ```
 */
export const WorkspaceListResponseSchema = ApiResponseSchema(
	PaginatedResponseSchema(WorkspaceSchema)
);

/**
 * 从 Schema 推断的工作空间列表响应类型
 */
export type WorkspaceListResponse = z.infer<typeof WorkspaceListResponseSchema>;
