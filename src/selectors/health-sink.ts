import { appendFile, mkdir } from "node:fs/promises";
import { dirname } from "node:path";

const DISABLED = /^true$/i.test(process.env.SELECTOR_HEALTH_DISABLED || "false");
const OUT_PATH = process.env.SELECTOR_HEALTH_PATH || "artifacts/selector-health.ndjson";
const FLUSH_MS = Math.max(50, Number(process.env.SELECTOR_HEALTH_FLUSH_MS || 500));
const BATCH_MAX = Math.max(1, Number(process.env.SELECTOR_HEALTH_BATCH_MAX || 100));

export type SelectorHealthRecord = {
	ts: number;
	slug?: string;
	url?: string;
	selectorId: string;
	ok: boolean;
	durationMs: number;
	retries?: number;
	errorCode?: string;
	metric?: {
		round?: number;
		anchors?: number;
		visited?: number;
		progressed?: boolean;
	};
};

async function ensureParent(file: string) {
	try {
		await mkdir(dirname(file), { recursive: true });
	} catch {}
}

let buffer: string[] = [];
let scheduled = false;
let backoffMs = 0;

async function flushNow() {
	if (DISABLED) return;
	if (buffer.length === 0) return;
	const data = buffer.join("");
	buffer = [];
	try {
		await ensureParent(OUT_PATH);
		await appendFile(OUT_PATH, data, { encoding: "utf-8" });
		backoffMs = 0; // reset on success
	} catch (e) {
		// 回退：简单指数退避并把数据放回缓冲
		buffer.unshift(data);
		backoffMs = Math.min(
			5000,
			backoffMs === 0 ? FLUSH_MS : Math.min(5000, Math.floor(backoffMs * 1.7)),
		);
		try {
			process.stderr.write(
				`[selector-health-sink] flush error: ${String((e as any)?.message || e)}\n`,
			);
		} catch {}
	}
}

function scheduleFlush() {
	if (scheduled) return;
	scheduled = true;
	const delay = backoffMs > 0 ? backoffMs : FLUSH_MS;
	setTimeout(async () => {
		scheduled = false;
		await flushNow();
		if (buffer.length >= BATCH_MAX) scheduleFlush();
	}, delay).unref?.();
}

export async function appendHealth(record: SelectorHealthRecord): Promise<void> {
	if (DISABLED) return;
	try {
		const line = JSON.stringify(record) + "\n";
		buffer.push(line);
		if (buffer.length >= BATCH_MAX) {
			await flushNow();
		} else {
			scheduleFlush();
		}
	} catch (e) {
		// 降级：不影响主流程，记录到 stderr 以便诊断
		try {
			process.stderr.write(`[selector-health-sink] ${String((e as any)?.message || e)}\n`);
		} catch {}
	}
}

// 导航滚动进度指标（写入同一 NDJSON，便于统一报表）
export async function appendNavProgress(rec: {
	url?: string;
	slug?: string;
	round: number;
	anchors: number;
	visited: number;
	progressed: boolean;
}): Promise<void> {
	await appendHealth({
		ts: Date.now(),
		slug: rec.slug,
		url: rec.url,
		selectorId: "nav-progress",
		ok: true,
		durationMs: 0,
		metric: {
			round: rec.round,
			anchors: rec.anchors,
			visited: rec.visited,
			progressed: rec.progressed,
		},
	});
}

// 在进程结束前尽量冲刷缓冲，减少丢失
try {
	const drain = async () => {
		try {
			await (flushNow as any)();
		} catch {}
	};
	(process as any).on("beforeExit", drain);
	(process as any).on("exit", () => {
		/* 同步上下文，尽力而为 */
	});
	(process as any).on("SIGINT", async () => {
		await drain();
		process.exit(0);
	});
	(process as any).on("SIGTERM", async () => {
		await drain();
		process.exit(0);
	});
} catch {}
