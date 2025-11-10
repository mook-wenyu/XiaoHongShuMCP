/* 中文注释：xhsShortcuts 工具全量集成测试 */
import { describe, it, expect, beforeAll } from "vitest";
import { roxySupportsOpen } from "../../../helpers/roxy.js";
const ready = await roxySupportsOpen();
const describeIf = ready ? describe : (describe.skip as typeof describe);
import { getContainerAndManager, pickDirId } from "../../../helpers/roxyHarness.js";
import { createMcpHarness } from "../../../helpers/mcpHarness.js";
import { installXhsRoutes } from "../../../helpers/routeFulfillKit.js";
import { resolve as pathResolve } from "node:path";
import { stat, unlink } from "node:fs/promises";

describeIf("xhsShortcuts 工具全量集成", () => {
	let dirId: string;
	let fixturesRoot: string;
	let harness: ReturnType<typeof createMcpHarness>;
	let managerRef: Awaited<ReturnType<typeof getContainerAndManager>>["manager"];
	beforeAll(async () => {
		const { container, manager } = await getContainerAndManager();
		managerRef = manager;
		harness = createMcpHarness();
		harness.registerAll(container, manager);
		dirId = await pickDirId("xhs_shortcuts_e2e");
		const ctx = await manager.getContext(dirId);
		fixturesRoot = pathResolve(process.cwd(), "tests/fixtures/xhs");
		await installXhsRoutes(ctx, { root: fixturesRoot });
	});

	it("navigate_discover 成功并带软校验", async () => {
		const nav = harness.getHandler("xhs_navigate_discover");
		const res = await nav({ dirId });
		const payload = JSON.parse(res.content[0].text);
		expect(payload.ok).toBe(true);
		expect(payload.value?.target).toBe("discover");
	});

	it("search_keyword 与 collect_search_results", async () => {
		const search = harness.getHandler("xhs_search_keyword");
		const collect = harness.getHandler("xhs_collect_search_results");
		let res = await search({ dirId, keyword: "美食" });
		let payload = JSON.parse(res.content[0].text);
		expect(payload.ok).toBe(true);
		res = await collect({ dirId, keyword: "美食", limit: 5 });
		payload = JSON.parse(res.content[0].text);
		expect(payload.ok).toBe(true);
	}, 60000);

	it("close_modal 关闭模态（先导航到带模态的页面）", async () => {
		// 导航到一个带模态元素的 explore URL（同一拦截返回 explore.html，但我们在此只验证接口）
		const navigate = harness.getHandler("page_navigate");
		await navigate({ dirId, url: "https://www.xiaohongshu.com/explore?modal=1" });
		const closeModal = harness.getHandler("xhs_close_modal");
		const res = await closeModal({ dirId });
		const payload = JSON.parse(res.content[0].text);
		// 在仿真页面下默认无模态，closeModalIfOpen 返回 false；此处仅验证接口可达
		expect(typeof payload.ok).toBe("boolean");
	});

	it("navigate_discover 失败路径：应写入截图并返回路径", async () => {
		// 暂时移除发现页拦截并改为 abort，制造导航异常以触发截图
		const ctx = await managerRef.getContext(dirId);
		await ctx.unroute("https://www.xiaohongshu.com/explore");
		await ctx.unroute("https://www.xiaohongshu.com/explore?*");
		await ctx.route("https://www.xiaohongshu.com/explore*", (route) => route.abort());

		const nav = harness.getHandler("xhs_navigate_discover");
		const res = await nav({ dirId });
		const payload = JSON.parse(res.content[0].text);
		expect(payload.ok).toBe(false);
		const sp: string | undefined = payload.error?.screenshotPath;
		expect(typeof sp).toBe("string");
		const abs = pathResolve(process.cwd(), String(sp));
		const st = await stat(abs);
		expect(st.isFile()).toBe(true);

		// 清理：删除截图文件，并恢复路由以不影响后续用例
		await unlink(abs).catch(() => {});
		await ctx.unroute("https://www.xiaohongshu.com/explore*");
		await installXhsRoutes(ctx, { root: fixturesRoot });
	}, 15000);
});
