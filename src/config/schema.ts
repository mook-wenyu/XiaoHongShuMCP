/* 中文注释：配置 Schema 校验，支持 baseURL 或 host+port，token 必填；多窗口=多上下文模型 */
import { z } from "zod";

const BaseEnv = z.object({
	ROXY_API_BASEURL: z.string().url().optional(),
	ROXY_API_HOST: z.string().optional(),
	ROXY_API_PORT: z.string().optional(),
	ROXY_API_TOKEN: z.string().min(1, "缺少 ROXY_API_TOKEN"),
	MAX_CONCURRENCY: z.string().default("2").transform((s) => Math.max(1, Number(s) || 1)),
	TIMEOUT_MS: z.string().default("60000").transform((s) => Math.max(1000, Number(s) || 60000)),
	DEFAULT_URL: z.string().url().default("https://example.com"),
	POLICY_QPS: z.string().default("5").transform((s) => Math.max(1, Number(s) || 5)),
	POLICY_FAILURES: z.string().default("5").transform((s) => Math.max(1, Number(s) || 5)),
	POLICY_OPEN_SECONDS: z.string().default("15").transform((s) => Math.max(1, Number(s) || 15))
});

export const ConfigSchema = BaseEnv; // baseURL 可空，运行时默认 http://127.0.0.1:50000；也可通过 HOST/PORT 指定

export type AppConfig = {
	roxy: { baseURL: string; token: string };
	MAX_CONCURRENCY: number;
	TIMEOUT_MS: number;
	DEFAULT_URL: string;
	policy: { qps: number; failureThreshold: number; openSeconds: number };
};
