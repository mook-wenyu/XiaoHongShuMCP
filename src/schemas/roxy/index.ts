/**
 * Roxy API Zod Schema 统一导出
 *
 * @remarks
 * 提供统一的导入入口，简化 Schema 引用。
 * 使用示例：`import { PaginatedParamsSchema, ApiResponseSchema } from "./schemas/roxy/index.js"`
 *
 * @packageDocumentation
 */

export {
	PaginatedParamsSchema,
	ApiResponseSchema,
	PaginatedResponseSchema,
	type PaginatedParams,
	type ApiResponse,
	type PaginatedResponse,
} from "./common.js";

// 工作空间相关 Schema 和类型
export {
	ProjectDetailSchema,
	WorkspaceSchema,
	WorkspaceListParamsSchema,
	WorkspaceListResponseSchema,
	type ProjectDetail,
	type Workspace,
	type WorkspaceListParams,
	type WorkspaceListResponse,
} from "./workspace.js";

// 窗口管理相关 Schema 和类型
export {
	WindowSchema,
	WindowListParamsSchema,
	WindowListResponseSchema,
	WindowCreateRequestSchema,
	WindowCreateResponseSchema,
	type Window,
	type WindowListParams,
	type WindowListResponse,
	type WindowCreateRequest,
	type WindowCreateResponse,
} from "./window.js";

// 连接相关 Schema 和类型
export {
	OpenRequestSchema,
	OpenResponseSchema,
	CloseResponseSchema,
	ConnectionInfoSchema,
	ConnectionInfoResponseSchema,
	type OpenRequest,
	type OpenResponse,
	type CloseResponse,
	type ConnectionInfo,
	type ConnectionInfoResponse,
} from "./connection.js";

// 指纹和代理配置 Schema 和类型
export {
	FingerprintConfigSchema,
	type FingerprintConfig,
} from "./fingerprint.js";
export { ProxyConfigSchema, type ProxyConfig } from "./proxy.js";
