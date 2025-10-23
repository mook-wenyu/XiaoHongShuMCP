/* 中文注释：策略调度（限速/熔断/半开） */
import { setTimeout as delay } from "node:timers/promises";
import { createLogger } from "../logging/index.js";
const log = createLogger();

export type CircuitState = "closed" | "open" | "half-open";

export interface PolicyOptions {
	qps?: number; // 每秒请求上限（近似）
	openSeconds?: number; // 熔断打开时长
	failureThreshold?: number; // 连续失败阈值
}

export class PolicyEnforcer {
	private readonly opts: Required<PolicyOptions>;
	private lastTick = 0;
	private tokens = 0;
	private perTokenMs: number;
	private failures: Record<string, number> = {};
	private states: Record<string, { state: CircuitState; until?: number }> = {};

	// 便捷包装：acquire + 执行 + success/fail
	async use<T>(key: string, fn: () => Promise<T>): Promise<T> {
		await this.acquire(key);
		try { const r = await fn(); this.success(key); return r; }
		catch (e) { this.fail(key, e); throw e; }
	}

	constructor(private options: PolicyOptions = {}) {
		this.opts = {
			qps: options.qps ?? 5,
			openSeconds: options.openSeconds ?? 15,
			failureThreshold: options.failureThreshold ?? 5
		};
		// 防止 qps 为 0 导致 Infinity
		this.perTokenMs = this.opts.qps > 0 ? 1000 / this.opts.qps : 0;
	}

	private refill(now: number) {
		// 简化：基于时间片补 token
		if (now - this.lastTick >= this.perTokenMs) {
			const add = Math.floor((now - this.lastTick) / this.perTokenMs);
			this.tokens = Math.min(this.opts.qps, this.tokens + add);
			this.lastTick = now;
		}
	}

	async acquire(key: string) {
		const st = this.states[key];
		const now = Date.now();
		// 熔断器状态检查
		if (st?.state === "open") {
			if ((st.until ?? 0) > now) {
				// 仍在熔断期内，等待至半开窗口
				const waitMs = (st.until ?? now) - now;
				log.warn({ key, waitMs }, "熔断打开中，等待半开窗口");
				await delay(waitMs);
				this.states[key] = { state: "half-open" };
			} else {
				// 熔断期已过，直接进入半开状态
				this.states[key] = { state: "half-open" };
			}
		}
		// 限速：令牌桶近似
		if (this.perTokenMs > 0) {
			for (;;) {
				this.refill(Date.now());
				if (this.tokens > 0) { this.tokens--; break; }
				await delay(this.perTokenMs);
			}
		}
	}

	success(key: string) {
		this.failures[key] = 0;
		if (this.states[key]?.state === "half-open") this.states[key] = { state: "closed" };
	}

	fail(key: string, err?: unknown) {
		const count = (this.failures[key] ?? 0) + 1;
		this.failures[key] = count;
		if (count >= this.opts.failureThreshold) {
			const until = Date.now() + this.opts.openSeconds * 1000;
			this.states[key] = { state: "open", until };
			log.error({ key, until, count, err }, "触发熔断");
		}
	}
}
