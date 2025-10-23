/* 中文注释：跨调用持久连接管理（按 dirId 复用 Browser/Context） */
import type { Browser, BrowserContext } from "playwright";
import type { IPlaywrightConnector } from "../contracts/IPlaywrightConnector.js";
import type { IConnectionManager } from "../contracts/IConnectionManager.js";
import type { ILogger } from "../contracts/ILogger.js";
import type { OpenOptions } from "./playwrightConnector.js";

export interface ManagedConn {
	browser: Browser;
	context: BrowserContext;
	workspaceId?: string | number;
	lastUsed: number;
}

/**
 * 连接管理器
 *
 * 管理浏览器连接池，支持连接复用、TTL 自动清理、预热和健康检查。
 * 实现 IConnectionManager 接口，支持依赖注入。
 *
 * @remarks
 * 核心特性：
 * - 连接复用：同一 dirId 多次请求返回同一连接
 * - TTL 自动清理：超过 TTL 时间的闲置连接自动关闭
 * - 预热：批量预连接提升性能
 * - 健康检查：检测连接是否可用
 * - 定期清理：后台定时任务清理过期连接
 *
 * 使用示例：
 * ```typescript
 * const cm = new ConnectionManager(connector, 5 * 60_000, logger);
 *
 * // 获取连接（自动复用）
 * const conn = await cm.get('dirId123');
 * const page = await conn.context.newPage();
 *
 * // 预热连接池
 * await cm.warmup(['dirId1', 'dirId2', 'dirId3']);
 *
 * // 健康检查
 * const healthy = await cm.healthCheck('dirId123');
 * ```
 */
export class ConnectionManager implements IConnectionManager {
	private map = new Map<string, ManagedConn>();
	private sweeperTimer: NodeJS.Timeout;

	/**
	 * 构造函数
	 * @param connector Playwright 连接器接口
	 * @param ttlMs TTL 时间（毫秒），默认 5 分钟
	 * @param logger 日志记录器（可选）
	 */
	constructor(
		private connector: IPlaywrightConnector,
		private ttlMs: number = Number(process.env.CONNECTION_TTL_MS || 5 * 60_000),
		private logger?: ILogger
	) {
		// 定期清理超时连接（闲置超过 ttlMs）
		const interval = Number(
			process.env.CONNECTION_SWEEP_MS || Math.max(30_000, Math.floor(this.ttlMs / 2))
		);
		this.logger?.debug({ ttlMs, sweepInterval: interval }, "启动连接管理器");

		this.sweeperTimer = setInterval(() => {
			this.sweep();
		}, interval);

		// 允许进程退出时清理定时器
		this.sweeperTimer.unref?.();
	}

	/**
	 * 定期清理过期连接
	 * @private
	 */
	private async sweep() {
		const now = Date.now();
		const expiredIds: string[] = [];

		for (const [id, mc] of this.map.entries()) {
			if (now - mc.lastUsed > this.ttlMs) {
				expiredIds.push(id);
			}
		}

		if (expiredIds.length > 0) {
			this.logger?.info({ count: expiredIds.length, ids: expiredIds }, "清理过期连接");
			await Promise.all(expiredIds.map((id) => this.close(id).catch(() => {})));
		}
	}

	async get(dirId: string, opts?: OpenOptions): Promise<ManagedConn> {
		const existing = this.map.get(dirId);
		if (existing) {
			existing.lastUsed = Date.now();
			this.logger?.debug({ dirId }, "复用已存在的连接");
			return existing;
		}

		this.logger?.debug({ dirId, workspaceId: opts?.workspaceId }, "创建新连接");
		const { browser, context } = await this.connector.connect(dirId, opts);
		const mc: ManagedConn = {
			browser,
			context,
			workspaceId: opts?.workspaceId,
			lastUsed: Date.now(),
		};
		this.map.set(dirId, mc);
		this.logger?.info({ dirId, totalConnections: this.map.size }, "连接已创建");
		return mc;
	}

	has(dirId: string) {
		return this.map.has(dirId);
	}

	list() {
		return Array.from(this.map.keys());
	}

	async close(dirId: string) {
		const mc = this.map.get(dirId);
		if (!mc) return;

		this.map.delete(dirId);
		this.logger?.debug({ dirId }, "关闭连接");

		try {
			await mc.browser.close();
			this.logger?.info({ dirId, remainingConnections: this.map.size }, "连接已关闭");
		} catch (e) {
			this.logger?.warn({ dirId, err: e }, "关闭浏览器失败，忽略");
		}
	}

	async closeAll() {
		const ids = this.list();
		this.logger?.info({ count: ids.length }, "关闭所有连接");
		await Promise.all(ids.map((id) => this.close(id)));

		// 清理定时器
		clearInterval(this.sweeperTimer);
		this.logger?.debug("已清理定时器");
	}

	/**
	 * 预热连接池
	 *
	 * 批量预连接指定的 dirId，提升后续访问性能。
	 * 失败的连接会记录日志但不抛出错误，返回成功预热的 dirId 列表。
	 *
	 * @param dirIds 要预热的 dirId 列表
	 * @param opts 打开选项（可选）
	 * @returns 成功预热的 dirId 列表
	 *
	 * @example
	 * ```typescript
	 * const warmed = await cm.warmup(['dirId1', 'dirId2', 'dirId3']);
	 * console.log(`成功预热 ${warmed.length} 个连接`);
	 * ```
	 */
	async warmup(dirIds: string[], opts?: OpenOptions): Promise<string[]> {
		this.logger?.info({ count: dirIds.length, dirIds }, "开始预热连接池");

		const results = await Promise.allSettled(
			dirIds.map(async (dirId) => {
				try {
					await this.get(dirId, opts);
					return dirId;
				} catch (e) {
					this.logger?.warn({ dirId, err: e }, "预热连接失败");
					throw e;
				}
			})
		);

		const succeeded = results
			.filter((r): r is PromiseFulfilledResult<string> => r.status === "fulfilled")
			.map((r) => r.value);

		const failed = results.filter((r) => r.status === "rejected").length;

		this.logger?.info(
			{ succeeded: succeeded.length, failed, total: dirIds.length },
			"连接池预热完成"
		);

		return succeeded;
	}

	/**
	 * 健康检查
	 *
	 * 检查指定 dirId 的连接是否健康可用。
	 * 尝试在连接的上下文中创建一个页面并立即关闭，以验证连接状态。
	 *
	 * @param dirId 要检查的 dirId
	 * @returns 连接是否健康
	 *
	 * @example
	 * ```typescript
	 * const healthy = await cm.healthCheck('dirId123');
	 * if (!healthy) {
	 *   await cm.close('dirId123'); // 关闭不健康的连接
	 * }
	 * ```
	 */
	async healthCheck(dirId: string): Promise<boolean> {
		const mc = this.map.get(dirId);
		if (!mc) {
			this.logger?.debug({ dirId }, "连接不存在，健康检查失败");
			return false;
		}

		try {
			// 尝试创建并关闭一个页面来验证连接状态
			this.logger?.debug({ dirId }, "执行健康检查");
			const page = await mc.context.newPage();
			await page.close();
			this.logger?.debug({ dirId }, "健康检查通过");
			return true;
		} catch (e) {
			this.logger?.warn({ dirId, err: e }, "健康检查失败");
			return false;
		}
	}
}
