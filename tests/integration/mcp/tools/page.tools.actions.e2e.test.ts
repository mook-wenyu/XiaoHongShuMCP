/* 中文注释：page 工具（交互）全量集成测试 */
import { describe, it, expect, beforeAll } from "vitest";
import { roxySupportsOpen } from "../../../helpers/roxy.js";
const ready = await roxySupportsOpen();
const describeIf = ready ? describe : (describe.skip as typeof describe);
import { getContainerAndManager, pickDirId } from "../../../helpers/roxyHarness.js";
import { createMcpHarness } from "../../../helpers/mcpHarness.js";
import { installXhsRoutes } from "../../../helpers/routeFulfillKit.js";
import { resolve as pathResolve } from "node:path";

describeIf("page 工具（交互）全量集成", () => {
	let dirId: string;
	let fixturesRoot: string;
	let harness: ReturnType<typeof createMcpHarness>;
	beforeAll(async () => {
		const { container, manager } = await getContainerAndManager();
		harness = createMcpHarness();
		harness.registerAll(container, manager);
		dirId = await pickDirId("page_act_e2e");
		const ctx = await manager.getContext(dirId);
		fixturesRoot = pathResolve(process.cwd(), "tests/fixtures/xhs");
		await installXhsRoutes(ctx, { root: fixturesRoot });
		// 确保在发现页
		const navigate = harness.getHandler("page_navigate");
		await navigate({ dirId, url: "https://www.xiaohongshu.com/explore" });
	});

	it("type + click（human=false）", async () => {
		const type = harness.getHandler("page_type");
		const click = harness.getHandler("page_click");
		const res1 = await type({
			dirId,
			target: { placeholder: "搜索小红书" },
			text: "美食",
			human: false,
		});
		const p1 = JSON.parse(res1.content[0].text);
		expect(p1.ok).toBe(true);
		const res2 = await click({ dirId, target: { role: "button", name: { contains: "搜索" } }, human: false });
		expect(JSON.parse(res2.content[0].text).ok).toBe(true);
	});

	it("hover + scroll（human=true）", async () => {
		const hover = harness.getHandler("page_hover");
		const scroll = harness.getHandler("page_scroll");
		const r1 = await hover({ dirId, target: { selector: "a[href*='channel_id=homefeed_recommend']" }, human: true });
		expect(JSON.parse(r1.content[0].text).ok).toBe(true);
		const r2 = await scroll({ dirId, human: true, deltaY: 400 });
		expect(JSON.parse(r2.content[0].text).ok).toBe(true);
	});

	it("定位失败路径", async () => {
		const click = harness.getHandler("page_click");
		const res = await click({ dirId, target: { selector: "#not-exists" } });
		const payload = JSON.parse(res.content[0].text);
		expect(payload.ok).toBe(false);
		expect(payload.error?.code).toBeDefined();
	});
});

