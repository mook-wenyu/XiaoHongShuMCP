/* 中文：直接使用容器与测试 Harness 调用 xhs_shortcuts 搜索链路
 * 功能：
 * - 连接已有 Roxy 窗口（使用环境变量 ROXY_DIR_IDS 第一个值）
 * - 安装合成夹带路由（tests/fixtures/xhs），稳定执行
 * - 调用 xhs_search_keyword 与 xhs_collect_search_results 并打印结果
 * 用法：
 *   npx tsx scripts/run-xhs-shortcuts-search.ts --keyword=美食 --limit=5
 */
import "dotenv/config";
import { resolve as pathResolve } from "node:path";
import { getContainerAndManager, pickDirId } from "../tests/helpers/roxyHarness.js";
import { createMcpHarness } from "../tests/helpers/mcpHarness.js";
import { installXhsRoutes } from "../tests/helpers/routeFulfillKit.js";

function getArg(name: string, def?: string) {
	const pair = process.argv.find((a) => a.startsWith(`--${name}=`));
	return pair ? pair.split("=", 2)[1] : def;
}

(async () => {
	const { container, manager } = await getContainerAndManager();
	const harness = createMcpHarness();
	harness.registerAll(container as any, manager as any);
	const dirId = (process.env.ROXY_DIR_IDS || "").split(",")[0] || (await pickDirId("xhs_shortcuts_cli"));
	const keyword = getArg("keyword", "美食")!;
	const limit = Number(getArg("limit", "5"));
	const workspaceId = getArg("workspaceId");
	// 可选：安装 fixtures 路由（默认安装；当传入 --no-routes=true 时跳过，直连真实站点）
	const ctx = await manager.getContext(dirId, workspaceId ? { workspaceId } : {});
	const noRoutes = getArg("no-routes");
	if (String(noRoutes || "false").toLowerCase() !== "true") {
		const fixturesRoot = pathResolve(process.cwd(), "tests/fixtures/xhs");
		await installXhsRoutes(ctx, { root: fixturesRoot });
	}

	const search = harness.getHandler("xhs_search_keyword");
	const collect = harness.getHandler("xhs_collect_search_results");
	const searchRes = await search(workspaceId ? { dirId, keyword, workspaceId } : { dirId, keyword });
	const collectRes = await collect(workspaceId ? { dirId, keyword, limit, workspaceId } : { dirId, keyword, limit });
	// 打印结果（stderr 便于与其他输出区分）
	const outText = [
		"xhs_search_keyword =>\n" + searchRes.content[0].text,
		"xhs_collect_search_results =>\n" + collectRes.content[0].text,
	].join("\n");
	const outFile = process.env.OUT_FILE;
	if (outFile) {
		try {
			const { writeFile } = await import("node:fs/promises");
			await writeFile(outFile, outText, { encoding: "utf-8" });
		} catch {}
	}
	process.stderr.write(outText + "\n");
	await container.cleanup();
	process.exit(0);
})();
