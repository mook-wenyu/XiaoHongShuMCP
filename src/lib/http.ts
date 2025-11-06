/* 中文注释：HTTP 客户端封装（undici fetch + 退避重试） */
import { fetch } from "undici";
import { setTimeout as delay } from "node:timers/promises";
import type { ILogger } from "../contracts/ILogger.js";
import { NetworkError } from "../core/errors/NetworkError.js";

export interface HttpOptions {
	baseURL: string;
	headers?: Record<string, string>;
	maxRetries?: number;
	logger?: ILogger;
}

/**
 * HTTP 客户端
 *
 * 封装 undici fetch，支持自动重试和退避策略。
 * 5xx 错误自动重试，最多 5 次（可配置）。
 *
 * @remarks
 * 特性：
 * - 自动重试：5xx 错误指数退避重试
 * - 类型安全：泛型支持 JSON 响应类型
 * - 错误处理：使用 NetworkError 统一错误类型
 * - 日志记录：记录重试和失败信息
 */
export class HttpClient {
	private logger?: ILogger;

	constructor(private opts: HttpOptions) {
		this.logger = opts.logger;
	}

	private url(path: string) {
		return new URL(path, this.opts.baseURL).toString();
	}

	async request<T>(path: string, init?: any, attempt = 0): Promise<T> {
		const url = this.url(path);
		try {
			const res = await fetch(url, {
				...init,
				headers: {
					...(this.opts.headers || {}),
					...(init?.headers || {}),
					"content-type": "application/json",
				},
			});

			if (!res.ok) {
				const body = await res.text();
				const retryable = res.status >= 500 && attempt < (this.opts.maxRetries ?? 5);

				if (retryable) {
					const backoff = Math.min(1000 * 2 ** attempt, 8000);
					this.logger?.warn(
						{ status: res.status, attempt, url, method: init?.method },
						`HTTP ${res.status} 失败，退避 ${backoff}ms 后重试`,
					);
					await delay(backoff);
					return this.request<T>(path, init, attempt + 1);
				}

				// 不可重试的错误，抛出 NetworkError
				const error = new NetworkError(`HTTP ${res.status}: ${body}`, {
					status: res.status,
					url,
					method: init?.method || "GET",
					body,
				});
				this.logger?.error({ status: res.status, url }, `HTTP 请求失败: ${body}`);
				throw error;
			}

			const ctype = res.headers.get("content-type") || "";
			return ctype.includes("application/json")
				? ((await res.json()) as T)
				: ((await res.text()) as unknown as T);
		} catch (err) {
			// 网络错误（连接失败、超时等）
			if (err instanceof NetworkError) {
				throw err; // 已经是 NetworkError，直接抛出
			}

			// 网络错误重试逻辑
			const retryable = attempt < (this.opts.maxRetries ?? 5);
			if (retryable) {
				const backoff = Math.min(1000 * 2 ** attempt, 8000);
				this.logger?.warn(
					{ attempt, url, method: init?.method, error: (err as Error).message },
					`网络错误，退避 ${backoff}ms 后重试`,
				);
				await delay(backoff);
				return this.request<T>(path, init, attempt + 1);
			}

			// 将其他错误转换为 NetworkError
			const error = new NetworkError(`网络请求失败: ${String((err as Error).message || err)}`, {
				url,
				method: init?.method || "GET",
				originalError: err,
			});
			this.logger?.error({ url, err }, "HTTP 请求最终失败");
			throw error;
		}
	}

	get<T>(path: string) {
		return this.request<T>(path, { method: "GET" });
	}

	post<T>(path: string, json?: any) {
		return this.request<T>(path, { method: "POST", body: JSON.stringify(json ?? {}) });
	}
}
