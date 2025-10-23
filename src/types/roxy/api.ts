/**
 * Roxy API 端点常量定义
 *
 * 集中管理所有 API 端点路径，避免硬编码和拼写错误。
 *
 * @remarks
 * 使用 `as const` 确保类型推断为字面量类型而非 string。
 * 按功能模块分组：WORKSPACE（工作空间）、WINDOW（窗口管理）、CONNECTION（连接信息）。
 *
 * @packageDocumentation
 */

/**
 * Roxy API 端点常量
 *
 * @remarks
 * 端点分组：
 * - WORKSPACE: 工作空间相关 API
 * - WINDOW: 浏览器窗口管理 API
 * - CONNECTION: 连接信息查询 API
 */
export const ROXY_API = {
	/** 工作空间 API */
	WORKSPACE: {
		/** 获取工作空间列表 */
		LIST: "/browser/workspace",
	},
	/** 浏览器窗口管理 API */
	WINDOW: {
		/** 获取浏览器窗口列表（v3） */
		LIST: "/browser/list_v3",
		/** 创建浏览器窗口 */
		CREATE: "/browser/create",
		/** 打开浏览器窗口 */
		OPEN: "/browser/open",
		/** 关闭浏览器窗口 */
		CLOSE: "/browser/close",
		/** 获取窗口详情 */
		DETAIL: "/browser/detail",
		/** 生成随机指纹 */
		RANDOM_FINGERPRINT: "/browser/random_env",
	},
	/** 连接信息 API */
	CONNECTION: {
		/** 查询连接信息 */
		INFO: "/browser/connection_info",
	},
} as const;
