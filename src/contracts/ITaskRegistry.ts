import type { BrowserContext } from "playwright";

/**
 * 任务参数类型
 */
export type TaskArgs = Record<string, any>;

/**
 * 任务函数类型
 *
 * @param ctx 浏览器上下文
 * @param dirId 窗口标识符
 * @param args 任务参数
 * @returns 任务执行结果
 */
export type TaskFn<T = unknown> = (
	ctx: BrowserContext,
	dirId: string,
	args?: TaskArgs
) => Promise<T>;

/**
 * 任务元数据
 */
export interface TaskMetadata {
	/** 任务名称 */
	name: string;
	/** 任务描述 */
	description: string;
	/** 参数定义 */
	params: Record<string, any>;
}

/**
 * 任务注册中心接口
 *
 * 统一管理任务注册与调用，所有任务遵循统一签名 (ctx, dirId, args)。
 *
 * @remarks
 * 设计原则：
 * - 统一接口：所有任务使用相同的函数签名
 * - 动态加载：支持按需加载任务实现
 * - 元数据管理：提供任务描述和参数说明
 *
 * @example
 * ```typescript
 * const registry: ITaskRegistry = createRegistry();
 *
 * // 注册任务
 * registry.register('myTask', async (ctx, dirId, args) => {
 *   const page = await ctx.newPage();
 *   await page.goto(args.url);
 *   return { ok: true };
 * }, {
 *   description: '自定义任务',
 *   params: { url: { type: 'string', required: true } }
 * });
 *
 * // 运行任务
 * const task = registry.runTaskByName('myTask');
 * const result = await task(context, 'dirId123', { url: 'https://example.com' });
 *
 * // 列出任务
 * const tasks = registry.listTasks();
 * ```
 */
export interface ITaskRegistry {
	/**
	 * 注册任务
	 * @param name 任务名称
	 * @param fn 任务函数
	 * @param metadata 任务元数据
	 */
	register(
		name: string,
		fn: TaskFn,
		metadata: { description: string; params: Record<string, any> }
	): void;

	/**
	 * 根据名称获取任务函数
	 * @param name 任务名称
	 * @returns 任务函数
	 * @throws 任务不存在时抛出错误
	 */
	runTaskByName(name: string): TaskFn;

	/**
	 * 列出所有已注册任务
	 * @returns 任务元数据列表
	 */
	listTasks(): TaskMetadata[];
}
