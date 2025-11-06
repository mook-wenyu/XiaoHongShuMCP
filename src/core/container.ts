/**
 * 依赖注入容器
 *
 * - 单例管理：同一服务多次请求返回同一实例
 * - 延迟初始化：首次使用时创建
 * - 可选静默日志（MCP 模式下禁止 stdout）
 */
import type { AppConfig } from "../config/schema.js";
import type { IRoxyClient } from "../contracts/IRoxyClient.js";
import type { ILogger } from "../contracts/ILogger.js";
import { RoxyClient } from "../clients/roxyClient.js";
import { PolicyEnforcer, type PolicyOptions } from "../services/policy.js";
import { createLogger } from "../logging/createLogger.js";

export class ServiceContainer {
	private singletons = new Map<string, unknown>();
	private options: { loggerSilent?: boolean };

	constructor(private config: AppConfig, options?: { loggerSilent?: boolean }) {
		this.options = options ?? {};
	}

	private getSingleton<T>(key: string, factory: () => T): T {
		if (!this.singletons.has(key)) this.singletons.set(key, factory());
		return this.singletons.get(key) as T;
	}

	// Roxy API 客户端（单例）
	createRoxyClient(): IRoxyClient {
		return this.getSingleton("roxyClient", () => {
			const { baseURL, token } = this.config.roxy;
			const logger = this.createLogger({ module: "roxyClient", useSilent: this.options.loggerSilent === true });
			return new RoxyClient(baseURL, token, logger);
		}) as unknown as IRoxyClient;
	}

	// 日志（支持静默/转 stderr）
	createLogger(bindings?: Record<string, unknown> & { useSilent?: boolean; toStderr?: boolean }): ILogger {
		const useSilent = bindings?.useSilent === true || this.options.loggerSilent === true;
		if (useSilent) {
			const silent = createLogger({ useSilent: true });
			const b = { ...(bindings || {}) } as Record<string, unknown>;
			delete (b as any).useSilent;
			return Object.keys(b).length > 0 ? silent.child(b) : silent;
		}
		const base = this.getSingleton("logger", () => createLogger({ toStderr: process.env.MCP_LOG_STDERR === "true" })) as ILogger;
		return bindings ? base.child(bindings) : base;
	}

	// 策略执行器（QPS/熔断）（单例）
	createPolicyEnforcer(): PolicyEnforcer {
		return this.getSingleton("policyEnforcer", () => new PolicyEnforcer(this.config.policy as PolicyOptions));
	}

	// 资源清理：清空单例缓存（单例自身若需要 close/flush，应在未来扩展统一调用）
	async cleanup(): Promise<void> {
		this.singletons.clear();
	}

	// 拟人化：全局节律档位（default/cautious/rapid）
	getHumanizationProfileKey(): "default" | "cautious" | "rapid" {
		return this.config.HUMAN_PROFILE || "default";
	}
}
