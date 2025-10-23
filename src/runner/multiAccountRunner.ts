/* 中文注释：多账号运行器，并发单位=账号上下文（dirId=窗口），同账号多页=同Context内多个Page */
import PQueue from "p-queue";
import { createLogger } from "../logging/index.js";
const log = createLogger();
import type { IPlaywrightConnector } from "../contracts/IPlaywrightConnector.js";
import type { BrowserContext } from "playwright";


export interface RunOptions { concurrency: number; timeoutMs: number; policy?: import("../services/policy.js").PolicyEnforcer; taskName?: string; openOptions?: import("../services/playwrightConnector.js").OpenOptions }
export interface RunResult { success: string[]; failed: { dirId: string; error: string }[]; durationMs: number; metricsPath?: string }

export type ContextTask<T = unknown> = (ctx: BrowserContext, dirId: string) => Promise<T>;

export async function runDirIds<T>(
	dirIds: string[],
	connector: IPlaywrightConnector,
	task: ContextTask<T>,
	opts: RunOptions
): Promise<RunResult> {
	const start = Date.now();
	const pq = new PQueue({ concurrency: opts.concurrency });
	const success: string[] = [];
	const failed: { dirId: string; error: string }[] = [];

	await Promise.all(
		dirIds.map((id) =>
			pq.add(async () => {
				try {
					if (opts.policy) await opts.policy.acquire(id);
					const started = Date.now();
					const result = await connector.withContext(id, async (ctx) => {
						// 由业务自定义，在同 Context 内可开多页
						return task(ctx, id);
					}, opts.openOptions);
					success.push(id);
					opts.policy?.success(id);
					try {
						const { writeManifest } = await import("../services/artifacts.js");
						await writeManifest("artifacts", id, {
							task: opts.taskName ?? "unknown",
							dirId: id,
							startedAt: new Date(started).toISOString(),
							finishedAt: new Date().toISOString(),
							result
						});
					} catch {}
					return result;
				} catch (err: any) {
					failed.push({ dirId: id, error: String(err?.message || err) });
					opts.policy?.fail(id, err);
					log.error({ err, dirId: id }, "任务失败");
				}
			}, { timeout: opts.timeoutMs })
		)
	);

	const duration = Date.now() - start;
	let metricsPath: string | undefined;
	try {
		const { writeMetrics } = await import("../services/metrics.js");
		metricsPath = await writeMetrics("artifacts", { startedAt: new Date(start).toISOString(), finishedAt: new Date().toISOString(), durationMs: duration, success, failed });
	} catch {}
	return { success, failed, durationMs: duration, metricsPath };
}
