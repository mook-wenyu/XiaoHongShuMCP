/* 中文注释：从环境变量读取并校验配置；支持 baseURL 或 host+port，导出 roxy.baseURL 与 token */
import "dotenv/config";
import { ConfigSchema, type AppConfig } from "./schema.js";

export function loadConfig(): AppConfig {
	const parsed = ConfigSchema.safeParse(process.env);
	if (!parsed.success) {
		const msg = parsed.error.issues.map((i) => `${i.path.join(".")}: ${i.message}`).join("; ");
		throw new Error(`配置错误：${msg}`);
	}
	const env = parsed.data as any;
	const host = env.ROXY_API_HOST || "127.0.0.1";
	const port = env.ROXY_API_PORT || "50000";
	const baseURL: string = env.ROXY_API_BASEURL || `http://${host}:${port}`;
	return {
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
}
