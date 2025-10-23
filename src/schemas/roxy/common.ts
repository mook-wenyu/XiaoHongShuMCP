/**
 * Roxy API 通用类型的 Zod Schema 定义
 *
 * 提供运行时验证和类型推断能力。
 *
 * @remarks
 * 使用 Zod 的优势：
 * - 运行时类型验证，防止无效数据
 * - 通过 z.infer 自动推断 TypeScript 类型
 * - 提供友好的错误消息
 *
 * @packageDocumentation
 */

import { z } from "zod";

/**
 * 分页查询参数 Schema
 *
 * @remarks
 * - page_index 和 page_size 都是可选的
 * - page_index 必须 >= 1
 * - page_size 必须 >= 1
 */
export const PaginatedParamsSchema = z.object({
	page_index: z.number().int().min(1, "页码必须 >= 1").optional(),
	page_size: z.number().int().min(1, "每页条数必须 >= 1").optional(),
});

/**
 * 从 Schema 推断的分页参数类型
 *
 * @remarks
 * 通过 z.infer 自动推断类型，确保类型定义与 Schema 一致。
 */
export type PaginatedParams = z.infer<typeof PaginatedParamsSchema>;

/**
 * API 统一响应结构 Schema
 *
 * @remarks
 * 泛型函数，接受数据类型的 Schema 作为参数。
 * 使用示例：`ApiResponseSchema(WorkspaceSchema)`
 */
export const ApiResponseSchema = <T extends z.ZodTypeAny>(dataSchema: T) =>
	z.object({
		code: z.number().describe("响应码（0 表示成功）"),
		msg: z.string().describe("响应消息"),
		data: dataSchema.describe("响应数据"),
	});

/**
 * 从 Schema 推断的 API 响应类型
 *
 * @typeParam T - 数据类型的 Schema
 *
 * @remarks
 * 通过 z.infer 自动推断类型。
 */
export type ApiResponse<T extends z.ZodTypeAny> = z.infer<ReturnType<typeof ApiResponseSchema<T>>>;

/**
 * 分页响应数据结构 Schema
 *
 * @remarks
 * 泛型函数，接受列表项类型的 Schema 作为参数。
 * 使用示例：`PaginatedResponseSchema(WindowSchema)`
 */
export const PaginatedResponseSchema = <T extends z.ZodTypeAny>(itemSchema: T) =>
	z.object({
		total: z.number().int().min(0).describe("总记录数"),
		rows: z.array(itemSchema).describe("当前页数据列表"),
	});

/**
 * 从 Schema 推断的分页响应类型
 *
 * @typeParam T - 列表项类型的 Schema
 *
 * @remarks
 * 通过 z.infer 自动推断类型。
 */
export type PaginatedResponse<T extends z.ZodTypeAny> = z.infer<
	ReturnType<typeof PaginatedResponseSchema<T>>
>;
