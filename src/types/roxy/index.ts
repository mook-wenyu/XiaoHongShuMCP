/**
 * Roxy API 类型定义统一导出
 *
 * @remarks
 * 提供统一的导入入口，简化类型引用。
 * 使用示例：`import type { PaginatedParams, ApiResponse } from "./types/roxy/index.js"`
 *
 * @packageDocumentation
 */

export type { PaginatedParams, ApiResponse, PaginatedResponse } from "./common.js";
export { ROXY_API } from "./api.js";

// 工作空间相关类型
export type {
	ProjectDetail,
	Workspace,
	WorkspaceListParams,
	WorkspaceListResponse,
} from "./workspace.js";

// 窗口管理相关类型
export type {
	Window,
	WindowListParams,
	WindowListResponse,
	WindowCreateRequest,
	WindowCreateResponse,
} from "./window.js";

// 连接相关类型
export type {
	OpenRequest,
	OpenResponse,
	CloseResponse,
	ConnectionInfo,
	ConnectionInfoResponse,
} from "./connection.js";

// 指纹和代理配置类型
export type { FingerprintConfig } from "./fingerprint.js";
export type { ProxyConfig } from "./proxy.js";
