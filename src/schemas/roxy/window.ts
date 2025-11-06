/**
 * Roxy API 窗口管理相关 Zod Schema 定义
 *
 * 提供运行时验证和类型推断能力。
 *
 * @remarks
 * 窗口管理 Schema 涵盖列表查询、创建、详情等所有操作的验证。
 * 所有枚举值和可选字段都有明确的验证规则。
 *
 * @packageDocumentation
 */

import { z } from "zod";
import { PaginatedParamsSchema, ApiResponseSchema, PaginatedResponseSchema } from "./common.js";
import { FingerprintConfigSchema } from "./fingerprint.js";
import { ProxyConfigSchema } from "./proxy.js";

/**
 * 浏览器窗口信息 Schema
 *
 * @remarks
 * 验证规则：
 * - dirId: 必须是非空字符串
 * - windowName: 必须是非空字符串
 * - 其他字段都是可选的
 */
export const WindowSchema = z.object({
	dirId: z.string().min(1).describe("窗口 ID（唯一标识符）"),
	windowSortNum: z.union([z.string(), z.number()]).optional().describe("窗口序号"),
	windowName: z.string().min(1).describe("窗口名称"),
	coreVersion: z.string().optional().describe("内核版本"),
	os: z.string().optional().describe("操作系统类型（Windows, macOS, Linux 等）"),
	osVersion: z.string().optional().describe("操作系统版本"),
	windowRemark: z.string().optional().describe("窗口备注"),
	createTime: z.string().optional().describe("创建时间（ISO 8601 格式）"),
	updateTime: z.string().optional().describe("更新时间（ISO 8601 格式）"),
	userName: z.string().optional().describe("用户名"),
});

/**
 * 从 Schema 推断的窗口类型
 */
export type Window = z.infer<typeof WindowSchema>;

/**
 * 窗口列表查询参数 Schema
 *
 * @remarks
 * 验证规则：
 * - workspaceId: 必需，可以是数字或字符串
 * - 其他过滤条件都是可选的
 * - 继承分页参数（page_index, page_size）
 */
export const WindowListParamsSchema = PaginatedParamsSchema.extend({
	workspaceId: z.union([z.number(), z.string()]).describe("工作空间 ID（必需）"),
	dirIds: z.string().optional().describe("窗口 ID 列表（逗号分隔）"),
	windowName: z.string().optional().describe("窗口名称（模糊搜索）"),
	sortNums: z.string().optional().describe("窗口序号列表（逗号分隔）"),
	os: z.string().optional().describe("操作系统类型"),
	projectIds: z.string().optional().describe("项目 ID 列表（逗号分隔）"),
	windowRemark: z.string().optional().describe("窗口备注（模糊搜索）"),
	status: z.number().int().min(0).max(1).optional().describe("窗口状态（0: 未启动, 1: 运行中）"),
	labelIds: z.string().optional().describe("标签 ID 列表（逗号分隔）"),
	softDeleted: z
		.number()
		.int()
		.min(0)
		.max(1)
		.optional()
		.describe("软删除状态（0: 正常, 1: 已删除）"),
	createTimeBegin: z.string().optional().describe("创建时间起始（ISO 8601 格式）"),
	createTimeEnd: z.string().optional().describe("创建时间结束（ISO 8601 格式）"),
	isMultiLogin: z
		.number()
		.int()
		.min(0)
		.max(1)
		.optional()
		.describe("是否多账号登录（0: 否, 1: 是）"),
	is_not_proxy: z
		.number()
		.int()
		.min(0)
		.max(1)
		.optional()
		.describe("是否无代理（0: 有代理, 1: 无代理）"),
});

/**
 * 从 Schema 推断的窗口列表查询参数类型
 */
export type WindowListParams = z.infer<typeof WindowListParamsSchema>;

/**
 * 窗口列表响应 Schema
 *
 * @remarks
 * data 字段可以为 null（当用户没有窗口时）
 */
export const WindowListResponseSchema = ApiResponseSchema(
	PaginatedResponseSchema(WindowSchema).nullable(),
);

/**
 * 从 Schema 推断的窗口列表响应类型
 */
export type WindowListResponse = z.infer<typeof WindowListResponseSchema>;

/**
 * 创建窗口请求参数 Schema
 *
 * @remarks
 * 验证规则：
 * - workspaceId: 必需，可以是数字或字符串
 * - 其他配置参数都是可选的
 * - proxyInfo: 使用 ProxyConfigSchema 验证代理配置
 * - fingerInfo: 使用 FingerprintConfigSchema 验证指纹配置
 */
export const WindowCreateRequestSchema = z.object({
	workspaceId: z.union([z.number(), z.string()]).describe("工作空间 ID（必需）"),
	windowName: z.string().min(1).optional().describe("窗口名称"),
	coreVersion: z.string().optional().describe("内核版本"),
	os: z.string().optional().describe("操作系统类型"),
	osVersion: z.string().optional().describe("操作系统版本"),
	userAgent: z.string().optional().describe("User-Agent 字符串"),
	cookie: z.string().optional().describe("Cookie 字符串"),
	searchEngine: z.string().optional().describe("搜索引擎（google, bing, baidu 等）"),
	labelIds: z.array(z.number()).optional().describe("标签 ID 列表（数组）"),
	defaultOpenUrl: z.string().url().optional().describe("默认打开的 URL"),
	windowRemark: z.string().optional().describe("窗口备注"),
	projectId: z.number().int().positive().optional().describe("项目 ID"),
	proxyInfo: ProxyConfigSchema.optional().describe("代理配置（详见 ProxyConfig）"),
	fingerInfo: FingerprintConfigSchema.optional().describe("指纹配置（详见 FingerprintConfig）"),
});

/**
 * 从 Schema 推断的创建窗口请求类型
 */
export type WindowCreateRequest = z.infer<typeof WindowCreateRequestSchema>;

/**
 * 创建窗口响应 Schema
 */
export const WindowCreateResponseSchema = ApiResponseSchema(WindowSchema);

/**
 * 从 Schema 推断的创建窗口响应类型
 */
export type WindowCreateResponse = z.infer<typeof WindowCreateResponseSchema>;

/**
 * 窗口详情查询参数 Schema
 *
 * @remarks
 * 验证规则：
 * - workspaceId 和 dirId 都是必需的
 */
export const WindowDetailParamsSchema = z.object({
	workspaceId: z.union([z.number(), z.string()]).describe("工作空间 ID（必需）"),
	dirId: z.string().min(1).describe("窗口 ID（必需）"),
});

/**
 * 从 Schema 推断的窗口详情查询参数类型
 */
export type WindowDetailParams = z.infer<typeof WindowDetailParamsSchema>;

/**
 * 窗口详情响应 Schema
 */
export const WindowDetailResponseSchema = ApiResponseSchema(WindowSchema);

/**
 * 从 Schema 推断的窗口详情响应类型
 */
export type WindowDetailResponse = z.infer<typeof WindowDetailResponseSchema>;
