/**
 * 依赖注入容器
 *
 * 轻量级服务容器，管理所有核心服务的创建和生命周期。
 * 使用单例模式优化性能，避免重复创建开销。
 *
 * @remarks
 * 核心特性：
 * - 延迟初始化：服务仅在首次使用时创建
 * - 单例管理：同一服务多次请求返回同一实例
 * - 依赖注入：自动管理服务间依赖关系
 * - 工厂方法：返回接口类型，不暴露具体实现
 * - 生命周期管理：支持资源清理
 *
 * @packageDocumentation
 */

import type { AppConfig } from "../config/schema.js";
import type { IRoxyClient } from "../contracts/IRoxyClient.js";
import type { IPlaywrightConnector } from "../contracts/IPlaywrightConnector.js";
import type { IConnectionManager } from "../contracts/IConnectionManager.js";
import type { ILogger } from "../contracts/ILogger.js";
import { RoxyClient } from "../clients/roxyClient.js";
import { PlaywrightConnector } from "../services/playwrightConnector.js";
import { ConnectionManager } from "../services/connectionManager.js";
import { PolicyEnforcer, type PolicyOptions } from "../services/policy.js";
import { createLogger } from "../logging/createLogger.js";

/**
 * 服务容器
 *
 * 管理所有核心服务的创建、依赖注入和生命周期。
 * 使用单例模式确保服务实例唯一性，优化性能。
 *
 * @remarks
 * 使用示例：
 * ```typescript
 * const config = ConfigProvider.load();
 * const container = new ServiceContainer(config.getConfig());
 *
 * // 获取服务（首次创建，后续返回同一实例）
 * const roxy = container.createRoxyClient();
 * const connector = container.createPlaywrightConnector();
 * const cm = container.createConnectionManager();
 *
 * // 创建子日志记录器（每次创建新实例）
 * const logger = container.createLogger({ module: 'auth' });
 * ```
 *
 * 依赖关系：
 * - PlaywrightConnector 依赖 IRoxyClient
 * - ConnectionManager 依赖 IPlaywrightConnector
 * - PolicyEnforcer 独立，使用配置创建
 */
export class ServiceContainer {
	/**
	 * 单例服务缓存
	 * @private
	 */
	private singletons = new Map<string, unknown>();

	/**
	 * 运行时选项（仅容器内部使用）
	 */
	private options: { loggerSilent?: boolean };

	/**
	 * 构造函数
	 * @param config 应用配置
	 * @param options 运行时选项（如在 MCP 模式下静默日志）
	 */
	constructor(private config: AppConfig, options?: { loggerSilent?: boolean }) {
		this.options = options ?? {};
	}

	/**
	 * 获取单例服务
	 *
	 * 延迟初始化：服务仅在首次调用时创建，后续返回缓存实例。
	 *
	 * @param key 服务唯一键
	 * @param factory 服务工厂函数
	 * @returns 服务实例
	 * @private
	 */
	private getSingleton<T>(key: string, factory: () => T): T {
		if (!this.singletons.has(key)) {
			this.singletons.set(key, factory());
		}
		return this.singletons.get(key) as T;
	}

	/**
	 * 创建 Roxy API 客户端（单例）
	 *
	 * 封装 Roxy Browser API 的所有方法，包括健康检查、窗口管理、连接信息查询等。
	 * 自动注入配置中的 baseURL 和 token，以及日志记录器。
	 *
	 * @returns Roxy 客户端接口
	 *
	 * @example
	 * ```typescript
	 * const roxy = container.createRoxyClient();
	 * await roxy.health();
	 * const { ws } = await roxy.open('dirId123');
	 * ```
	 */
	createRoxyClient(): IRoxyClient {
		return this.getSingleton("roxyClient", () => {
			const { baseURL, token } = this.config.roxy;
			const logger = this.createLogger({ module: "roxyClient", useSilent: this.options.loggerSilent === true });
			return new RoxyClient(baseURL, token, logger);
		});
	}

	/**
	 * 创建 Playwright 连接器（单例）
	 *
	 * 管理 Playwright 浏览器连接，通过 CDP 协议连接到 Roxy 打开的浏览器窗口。
	 * 自动注入 IRoxyClient 依赖。
	 *
	 * @returns Playwright 连接器接口
	 *
	 * @example
	 * ```typescript
	 * const connector = container.createPlaywrightConnector();
	 * await connector.withContext('dirId123', async (ctx) => {
	 *   const page = await ctx.newPage();
	 *   await page.goto('https://example.com');
	 * });
	 * ```
	 */
	createPlaywrightConnector(): IPlaywrightConnector {
		return this.getSingleton("playwrightConnector", () => {
			const roxy = this.createRoxyClient();
			const logger = this.createLogger({ module: "playwrightConnector", useSilent: this.options.loggerSilent === true });
			return new PlaywrightConnector(roxy, logger);
		});
	}

	/**
	 * 创建连接管理器（单例）
	 *
	 * 管理浏览器连接池，支持连接复用、TTL 自动清理、预热和健康检查。
	 * 自动注入 IPlaywrightConnector 依赖和 TTL 配置。
	 *
	 * @returns 连接管理器接口
	 *
	 * @example
	 * ```typescript
	 * const cm = container.createConnectionManager();
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
	createConnectionManager(): IConnectionManager {
		return this.getSingleton("connectionManager", () => {
			const connector = this.createPlaywrightConnector();
			const logger = this.createLogger({ module: "connectionManager", useSilent: this.options.loggerSilent === true });
			const ttlMs = Number(process.env.CONNECTION_TTL_MS || 5 * 60_000);
			return new ConnectionManager(connector, ttlMs, logger);
		});
	}

	/**
	 * 创建日志记录器
	 *
	 * 创建日志记录器实例，支持上下文绑定（child logger）和静默模式。
	 * 如果提供 bindings 参数，返回绑定上下文的子日志记录器。
	 *
	 * @param bindings 上下文绑定数据（可选）
	 * @param bindings.useSilent 是否静默模式（用于 MCP 服务器）
	 * @returns 日志记录器接口
	 *
	 * @example
	 * ```typescript
	 * // 基础日志记录器（单例）
	 * const logger = container.createLogger();
	 * logger.info('应用启动');
	 *
	 * // 子日志记录器（每次创建新实例）
	 * const authLogger = container.createLogger({ module: 'auth', requestId: 'req-123' });
	 * authLogger.info('用户登录');
	 *
	 * // MCP 静默模式
	 * const mcpLogger = container.createLogger({ module: 'mcp', useSilent: true });
	 * mcpLogger.info('此消息不会输出');
	 * ```
	 */
	createLogger(bindings?: Record<string, unknown> & { useSilent?: boolean; toStderr?: boolean }): ILogger {
		// 检查是否需要静默模式（容器级别 MCP 静默优先）
		const useSilent = bindings?.useSilent === true || this.options.loggerSilent === true;

		if (useSilent) {
			// 创建静默日志记录器（不缓存，因为可能需要多个）
			const silentLogger = createLogger({ useSilent: true });
			const { useSilent: _, ...otherBindings } = bindings || {};
			return Object.keys(otherBindings).length > 0
				? silentLogger.child(otherBindings)
				: silentLogger;
		}

		// 非静默：允许通过 toStderr 选项或 MCP_LOG_STDERR 环境变量将输出重定向至 stderr
		const baseLogger = this.getSingleton(
			"logger",
			() => createLogger({ toStderr: process.env.MCP_LOG_STDERR === "true" })
		);
		return bindings ? baseLogger.child(bindings) : baseLogger;
	}

	/**
	 * 创建策略执行器（单例）
	 *
	 * 管理限流、熔断和半开恢复策略。
	 * 自动注入配置中的 QPS、失败阈值和熔断时长。
	 *
	 * @returns 策略执行器实例
	 *
	 * @example
	 * ```typescript
	 * const policy = container.createPolicyEnforcer();
	 * await policy.use('apiCall', async () => {
	 *   // 执行 API 调用
	 *   return await fetch('https://api.example.com');
	 * });
	 * ```
	 */
	createPolicyEnforcer(): PolicyEnforcer {
		return this.getSingleton("policyEnforcer", () => {
			const options: PolicyOptions = this.config.policy;
			return new PolicyEnforcer(options);
		});
	}

	/**
	 * 清理所有资源
	 *
	 * 关闭所有连接、释放资源。
	 * 建议在应用退出前调用（SIGINT/SIGTERM 处理器）。
	 *
	 * @example
	 * ```typescript
	 * process.on('SIGINT', async () => {
	 *   await container.cleanup();
	 *   process.exit(0);
	 * });
	 * ```
	 */
	async cleanup(): Promise<void> {
		const cm = this.singletons.get("connectionManager") as IConnectionManager | undefined;
		if (cm) {
			await cm.closeAll();
		}
		this.singletons.clear();
	}
}
