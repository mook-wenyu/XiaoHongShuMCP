import type { Browser, BrowserContext } from "playwright";

/**
 * 托管连接信息
 *
 * 包含浏览器实例、上下文、工作空间 ID 和最后使用时间。
 */
export interface ManagedConnection {
	/** 浏览器实例 */
	browser: Browser;
	/** 浏览器上下文 */
	context: BrowserContext;
	/** 工作空间 ID */
	workspaceId?: string | number;
	/** 最后使用时间戳（毫秒） */
	lastUsed: number;
}

/**
 * 连接管理器接口
 *
 * 跨调用持久化连接管理，按 dirId 复用 Browser/Context，支持 TTL 清理。
 *
 * @remarks
 * 功能特性：
 * - 连接池管理：按 dirId 缓存连接，避免重复建立
 * - TTL 清理：自动清理闲置超过 TTL 的连接
 * - 预热机制：批量预建连接，提高首次访问性能
 * - 健康检查：验证连接可用性
 *
 * @example
 * ```typescript
 * const manager: IConnectionManager = new ConnectionManager(connector);
 *
 * // 获取连接（自动复用或创建）
 * const conn = await manager.get('dirId123');
 * const page = await conn.context.newPage();
 *
 * // 预热连接池
 * await manager.warmup(['dirId1', 'dirId2', 'dirId3']);
 *
 * // 健康检查
 * const isHealthy = await manager.healthCheck('dirId123');
 *
 * // 清理
 * await manager.close('dirId123');
 * await manager.closeAll();
 * ```
 */
export interface IConnectionManager {
	/**
	 * 获取或创建连接
	 * @param dirId 窗口标识符
	 * @param opts 连接选项
	 * @returns 托管连接信息
	 */
	get(
		dirId: string,
		opts?: { workspaceId?: string }
	): Promise<ManagedConnection>;

	/**
	 * 检查连接是否存在
	 * @param dirId 窗口标识符
	 * @returns 是否存在
	 */
	has(dirId: string): boolean;

	/**
	 * 列出所有活动连接的 dirId
	 * @returns dirId 列表
	 */
	list(): string[];

	/**
	 * 关闭指定连接
	 * @param dirId 窗口标识符
	 */
	close(dirId: string): Promise<void>;

	/**
	 * 关闭所有连接
	 */
	closeAll(): Promise<void>;

	/**
	 * 预热连接池（批量创建连接）
	 * @param dirIds 窗口标识符列表
	 * @param opts 连接选项
	 * @returns 成功预热的 dirId 列表
	 */
	warmup(
		dirIds: string[],
		opts?: { workspaceId?: string }
	): Promise<string[]>;

	/**
	 * 健康检查（验证连接可用性）
	 * @param dirId 窗口标识符
	 * @returns 是否健康
	 */
	healthCheck(dirId: string): Promise<boolean>;
}
