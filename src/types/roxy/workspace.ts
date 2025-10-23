/**
 * Roxy API 工作空间相关类型定义
 *
 * 基于官方 API 文档定义工作空间列表、项目详情等类型。
 *
 * @remarks
 * 工作空间（Workspace）是 Roxy Browser 的组织单元，每个工作空间可以包含多个项目。
 * 工作空间 ID 是数字类型（如 28255），桌面端显示的前缀格式（如 NJJ0028255）仅用于 UI 展示。
 *
 * @packageDocumentation
 */

import type { PaginatedParams, ApiResponse, PaginatedResponse } from "./common.js";

/**
 * 项目详情
 *
 * @remarks
 * 每个工作空间可以包含多个项目，项目用于进一步组织浏览器窗口。
 */
export interface ProjectDetail {
	/** 项目 ID */
	projectId: number;
	/** 项目名称 */
	projectName: string;
}

/**
 * 工作空间信息
 *
 * @remarks
 * 工作空间是 Roxy Browser 的顶层组织单元。
 * API 返回的 ID 是数字类型，与桌面端显示格式不同。
 */
export interface Workspace {
	/** 工作空间 ID（数字，如 28255） */
	id: number;
	/** 工作空间名称 */
	workspaceName: string;
	/** 项目详情列表 */
	project_details: ProjectDetail[];
}

/**
 * 工作空间列表查询参数
 *
 * @remarks
 * 继承自 PaginatedParams，支持分页查询。
 */
export interface WorkspaceListParams extends PaginatedParams {}

/**
 * 工作空间列表响应
 *
 * @remarks
 * API 返回格式：
 * ```json
 * {
 *   "code": 0,
 *   "msg": "success",
 *   "data": {
 *     "total": 5,
 *     "rows": [{ "id": 28255, "workspaceName": "工作空间1", "project_details": [...] }]
 *   }
 * }
 * ```
 */
export interface WorkspaceListResponse extends ApiResponse<PaginatedResponse<Workspace>> {}
