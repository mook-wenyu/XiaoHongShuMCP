/* 中文注释：page 工具（生命周期）全量集成测试 */
import { describe, it, expect, beforeAll } from "vitest";
import { roxySupportsOpen } from "../../../helpers/roxy.js";
const ready = await roxySupportsOpen();
const describeIf = ready ? describe : (describe.skip as typeof describe);
import { getContainerAndManager, pickDirId } from "../../../helpers/roxyHarness.js";
import { createMcpHarness } from "../../../helpers/mcpHarness.js";
import { installXhsRoutes } from "../../../helpers/routeFulfillKit.js";
import { resolve as pathResolve } from "node:path";

describeIf("page 工具（生命周期）全量集成", () => {
	let dirId: string;
	let fixturesRoot: string;
	let harness: ReturnType<typeof createMcpHarness>;
	beforeAll(async () => {
		const { container, manager } = await getContainerAndManager();
		harness = createMcpHarness();
		harness.registerAll(container, manager);
		dirId = await pickDirId("page_life_e2e");
		const ctx = await manager.getContext(dirId);
		fixturesRoot = pathResolve(process.cwd(), "tests/fixtures/xhs");
		await installXhsRoutes(ctx, { root: fixturesRoot });
	});

	it("create → navigate → list → close", async () => {
		const create = harness.getHandler("page_create");
		const navigate = harness.getHandler("page_navigate");
		const list = harness.getHandler("page_list");
		const close = harness.getHandler("page_close");

		let res = await create({ dirId, url: "https://www.xiaohongshu.com/explore" });
		let payload = JSON.parse(res.content[0].text);
		expect(payload.ok).toBe(true);

		res = await navigate({ dirId, url: "https://www.xiaohongshu.com/explore" });
		payload = JSON.parse(res.content[0].text);
		expect(payload.ok).toBe(true);

		res = await list({ dirId });
		payload = JSON.parse(res.content[0].text);
		expect(payload.ok).toBe(true);
		expect(Array.isArray(payload.data?.pages)).toBe(true);

		res = await close({ dirId });
		payload = JSON.parse(res.content[0].text);
		expect(payload.ok).toBe(true);
	});
});

