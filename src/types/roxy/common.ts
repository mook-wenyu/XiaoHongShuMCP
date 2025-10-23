/**
 * Roxy API 通用类型定义
 *
 * 包含分页参数、API 响应结构等通用类型。
 *
 * @remarks
 * 所有 Roxy API 响应都遵循统一的 ApiResponse<T> 结构。
 * 分页查询使用 PaginatedParams 参数和 PaginatedResponse<T> 响应。
 *
 * @packageDocumentation
 */

/**
 * 分页查询参数
 *
 * @remarks
 * 用于所有支持分页的 API 请求，如工作空间列表、窗口列表等。
 * page_index 从 1 开始计数。
 */
export interface PaginatedParams {
	/** 页码（从 1 开始） */
	page_index?: number;
	/** 每页条数 */
	page_size?: number;
}

/**
 * API 统一响应结构
 *
 * @typeParam T - 响应数据类型
 *
 * @remarks
 * 所有 Roxy API 响应都遵循此结构：
 * - code: 响应码（0 表示成功）
 * - msg: 响应消息
 * - data: 实际数据（类型由泛型参数 T 指定）
 */
export interface ApiResponse<T = unknown> {
	/** 响应码（0 表示成功） */
	code: number;
	/** 响应消息 */
	msg: string;
	/** 响应数据 */
	data: T;
}

/**
 * 分页响应数据结构
 *
 * @typeParam T - 列表项类型
 *
 * @remarks
 * 用于所有分页查询的 data 字段，包含总数和当前页数据列表。
 */
export interface PaginatedResponse<T> {
	/** 总记录数 */
	total: number;
	/** 当前页数据列表 */
	rows: T[];
}
