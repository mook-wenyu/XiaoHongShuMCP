/* 中文注释：通用重试 withRetry（指数退避+抖动） */
import { setTimeout as delay } from "node:timers/promises";

export interface RetryOptions { attempts?: number; baseMs?: number; maxMs?: number; jitter?: boolean }

export async function withRetry<T>(fn: (i: number) => Promise<T>, opts: RetryOptions = {}): Promise<T> {
	const attempts = Math.max(1, opts.attempts ?? 5);
	const base = opts.baseMs ?? 200;
	const max = opts.maxMs ?? 8000;
	let lastErr: unknown;
	for (let i = 0; i < attempts; i++) {
		try { return await fn(i); } catch (e) {
			lastErr = e;
			const backoff = Math.min(max, base * 2 ** i) * (opts.jitter ? (0.5 + Math.random()) : 1);
			if (i < attempts - 1) await delay(backoff);
		}
	}
	throw lastErr;
}
