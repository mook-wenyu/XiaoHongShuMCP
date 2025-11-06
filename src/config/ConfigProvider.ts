import "dotenv/config";
import type { AppConfig } from "./schema.js";
import { ConfigSchema } from "./schema.js";

/**
 * 配置提供者
 *
 * 统一配置访问，提供类型安全的配置访问器，支持 Zod 验证和默认值处理。
 *
 * @remarks
 * 设计特性：
 * - 单例模式：通过静态 load() 方法创建实例
 * - 类型安全：所有配置访问器都有明确类型
 * - 验证机制：使用 Zod schema 验证环境变量
 * - 默认值处理：自动处理缺失配置项的默认值
 *
 * @example
 * ```typescript
 * // 加载配置
 * const config = ConfigProvider.load();
 *
 * // 访问配置
 * const baseURL = config.roxy.baseURL;
 * const concurrency = config.concurrency;
 * const qps = config.policy.qps;
 * ```
 */
export class ConfigProvider {
	/**
	 * 私有构造函数（使用静态 load() 方法创建实例）
	 * @param config 应用配置对象
	 */
	private constructor(private config: AppConfig) {}

	/**
	 * 加载配置（从环境变量）
	 *
	 * @returns ConfigProvider 实例
	 * @throws 配置验证失败时抛出错误
	 */
	static load(): ConfigProvider {
		const parsed = ConfigSchema.safeParse(process.env);
		if (!parsed.success) {
			const msg = parsed.error.issues.map((i) => `${i.path.join(".")}: ${i.message}`).join("; ");
			throw new Error(
				`配置错误：${msg}。请在项目根目录配置 .env（参考 .env.example）或设置系统环境变量。`,
			);
		}

		const env = parsed.data as any;
		const host = env.ROXY_API_HOST || "127.0.0.1";
		const port = env.ROXY_API_PORT || "50000";
		const baseURL: string = env.ROXY_API_BASEURL || `http://${host}:${port}`;

		const config: AppConfig = {
			roxy: { baseURL, token: env.ROXY_API_TOKEN },
			MAX_CONCURRENCY: env.MAX_CONCURRENCY,
			TIMEOUT_MS: env.TIMEOUT_MS,
			DEFAULT_URL: env.DEFAULT_URL,
			policy: {
				qps: env.POLICY_QPS,
				failureThreshold: env.POLICY_FAILURES,
				openSeconds: env.POLICY_OPEN_SECONDS,
			},
			HUMAN_PROFILE: env.HUMAN_PROFILE,
		};

		return new ConfigProvider(config);
	}

	/**
	 * Roxy API 配置
	 */
	get roxy() {
		return this.config.roxy;
	}

	/**
	 * 最大并发数
	 */
	get concurrency() {
		return this.config.MAX_CONCURRENCY;
	}

	/**
	 * 超时时间（毫秒）
	 */
	get timeout() {
		return this.config.TIMEOUT_MS;
	}

	/**
	 * 策略配置（QPS、熔断）
	 */
	get policy() {
		return this.config.policy;
	}

	/**
	 * 默认 URL
	 */
	get defaultUrl() {
		return this.config.DEFAULT_URL;
	}

	/**
	 * 获取完整配置对象（仅供内部使用）
	 * @internal
	 */
	getConfig(): AppConfig {
		return this.config;
	}
}
