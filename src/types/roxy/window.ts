/**
 * Roxy API 窗口管理相关类型定义
 *
 * 基于官方 API 文档定义浏览器窗口列表、创建、详情等类型。
 *
 * @remarks
 * 窗口（Window）是 Roxy Browser 的核心实体，每个窗口对应一个独立的浏览器实例。
 * 窗口通过 dirId 唯一标识，支持自定义指纹、代理、用户代理等配置。
 *
 * @packageDocumentation
 */

import type { PaginatedParams, ApiResponse, PaginatedResponse } from "./common.js";
import type { FingerprintConfig } from "./fingerprint.js";
import type { ProxyConfig } from "./proxy.js";

/**
 * 浏览器窗口信息
 *
 * @remarks
 * 从 API 返回的窗口数据结构，包含窗口的所有基本信息。
 */
export interface Window {
	/** 窗口 ID（唯一标识符） */
	dirId: string;
	/** 窗口序号（可以是字符串或数字） */
	windowSortNum?: string | number;
	/** 窗口名称 */
	windowName: string;
	/** 内核版本 */
	coreVersion?: string;
	/** 操作系统类型（Windows, macOS, Linux 等） */
	os?: string;
	/** 操作系统版本 */
	osVersion?: string;
	/** 窗口备注 */
	windowRemark?: string;
	/** 创建时间（ISO 8601 格式） */
	createTime?: string;
	/** 更新时间（ISO 8601 格式） */
	updateTime?: string;
	/** 用户名 */
	userName?: string;
}

/**
 * 窗口列表查询参数
 *
 * @remarks
 * 支持多种过滤条件的窗口列表查询。
 * workspaceId 是必需参数，其他都是可选的过滤条件。
 */
export interface WindowListParams extends PaginatedParams {
	/** 工作空间 ID（必需） */
	workspaceId: number | string;
	/** 窗口 ID 列表（逗号分隔，如 "dirA,dirB"） */
	dirIds?: string;
	/** 窗口名称（模糊搜索） */
	windowName?: string;
	/** 窗口序号列表（逗号分隔） */
	sortNums?: string;
	/** 操作系统类型 */
	os?: string;
	/** 项目 ID 列表（逗号分隔） */
	projectIds?: string;
	/** 窗口备注（模糊搜索） */
	windowRemark?: string;
	/** 窗口状态（0: 未启动, 1: 运行中） */
	status?: number;
	/** 标签 ID 列表（逗号分隔） */
	labelIds?: string;
	/** 软删除状态（0: 正常, 1: 已删除） */
	softDeleted?: number;
	/** 创建时间起始（ISO 8601 格式） */
	createTimeBegin?: string;
	/** 创建时间结束（ISO 8601 格式） */
	createTimeEnd?: string;
	/** 是否多账号登录（0: 否, 1: 是） */
	isMultiLogin?: number;
	/** 是否无代理（0: 有代理, 1: 无代理） */
	is_not_proxy?: number;
}

/**
 * 窗口列表响应
 *
 * @remarks
 * API 返回格式，data 可能为 null（用户没有窗口时）：
 * ```json
 * {
 *   "code": 0,
 *   "msg": "success",
 *   "data": {
 *     "total": 100,
 *     "rows": [{ "dirId": "dirA", "windowName": "窗口1", ... }]
 *   }
 * }
 * ```
 */
export interface WindowListResponse
	extends ApiResponse<PaginatedResponse<Window> | null> {}

/**
 * 创建窗口请求参数
 *
 * @remarks
 * 创建新浏览器窗口时的完整配置参数。
 * workspaceId 是必需参数，其他参数都是可选的。
 *
 * proxyInfo 和 fingerInfo 使用专门的配置类型（ProxyConfig 和 FingerprintConfig）。
 */
export interface WindowCreateRequest {
	/** 工作空间 ID（必需） */
	workspaceId: number | string;
	/** 窗口名称 */
	windowName?: string;
	/** 内核版本 */
	coreVersion?: string;
	/** 操作系统类型 */
	os?: string;
	/** 操作系统版本 */
	osVersion?: string;
	/** User-Agent 字符串 */
	userAgent?: string;
	/** Cookie 字符串 */
	cookie?: string;
	/** 搜索引擎（google, bing, baidu 等） */
	searchEngine?: string;
	/** 标签 ID 列表（数组） */
	labelIds?: number[];
	/** 默认打开的 URL */
	defaultOpenUrl?: string;
	/** 窗口备注 */
	windowRemark?: string;
	/** 项目 ID */
	projectId?: number;
	/** 代理配置（详见 ProxyConfig） */
	proxyInfo?: ProxyConfig;
	/** 指纹配置（详见 FingerprintConfig） */
	fingerInfo?: FingerprintConfig;
}

/**
 * 创建窗口响应
 *
 * @remarks
 * API 返回格式：
 * ```json
 * {
 *   "code": 0,
 *   "msg": "success",
 *   "data": {
 *     "dirId": "newDirId123",
 *     "windowName": "新窗口",
 *     ...
 *   }
 * }
 * ```
 */
export interface WindowCreateResponse extends ApiResponse<Window> {}

/**
 * 窗口详情查询参数
 *
 * @remarks
 * 查询单个窗口的详细信息。
 * workspaceId 和 dirId 都是必需参数。
 */
export interface WindowDetailParams {
	/** 工作空间 ID（必需） */
	workspaceId: number | string;
	/** 窗口 ID（必需） */
	dirId: string;
}

/**
 * 窗口详情响应
 *
 * @remarks
 * 返回窗口的完整配置信息，包括指纹、代理等。
 */
export interface WindowDetailResponse extends ApiResponse<Window> {}
