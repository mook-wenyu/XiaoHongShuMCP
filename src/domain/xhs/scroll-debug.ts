/* 中文注释：滚动调试截图（每步滚动后截图，便于分析是否“跳过”）
 * 开关：XHS_SCROLL_DEBUG_SHOT（默认 true）
 * 目录：artifacts/scroll-steps/<slug>/<runId>/step-<round>-v<visited>-a<anchors>-<progress>.png
 */
import { ensureDir } from "../../services/artifacts.js";
import { join } from "node:path";

const RUN_ID = String(Date.now());

export async function screenshotScrollStep(
	page: any,
	info: {
		round: number;
		progressed: boolean;
		anchors: number;
		visited: number;
		stepPx: number;
		slug?: string;
	},
) {
	const enabled = String(process.env.XHS_SCROLL_DEBUG_SHOT ?? "true").toLowerCase();
	if (enabled === "false" || enabled === "0") return undefined;
	const slug = info.slug || "xhs";
	const outRoot = join("artifacts", "scroll-steps", slug, RUN_ID);
	await ensureDir(outRoot);
	const tag = `step-${info.round}-v${info.visited}-a${info.anchors}-${info.progressed ? "p" : "np"}-s${info.stepPx}`;
	const path = join(outRoot, `${tag}.png`);
	try {
		// 仅截取视口，足够观察是否跳过，同时减小体积
		await page.screenshot({ path, fullPage: false });
		return path;
	} catch {
		return undefined;
	}
}
