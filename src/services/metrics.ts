/* 中文注释：运行指标汇总工具 */
import { pathJoin, ensureDir, writeJson } from "./artifacts.js";

export interface RunMetrics {
	startedAt: string;
	finishedAt: string;
	durationMs: number;
	success: string[];
	failed: { dirId: string; error: string }[];
}

export async function writeMetrics(root = "artifacts", data: RunMetrics) {
	const dir = pathJoin(root);
	await ensureDir(dir);
	const file = pathJoin(dir, `metrics-${Date.now()}.json`);
	await writeJson(file, data);
	return file;
}
