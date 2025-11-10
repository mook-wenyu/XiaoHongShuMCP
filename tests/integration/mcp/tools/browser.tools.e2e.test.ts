/* 中文注释：browser 工具全量集成测试 */
import { describe, it, expect, beforeAll } from "vitest";
import { roxySupportsOpen } from "../../../helpers/roxy.js";
const ready = await roxySupportsOpen();
const describeIf = ready ? describe : (describe.skip as typeof describe);
import { getContainerAndManager, pickDirId } from "../../../helpers/roxyHarness.js";
import { createMcpHarness } from "../../../helpers/mcpHarness.js";

describeIf("browser 工具全量集成", () => {
	let dirId: string;
	let harness: ReturnType<typeof createMcpHarness>;
	beforeAll(async () => {
		const { container, manager } = await getContainerAndManager();
		harness = createMcpHarness();
		harness.registerAll(container, manager);
		dirId = await pickDirId("browser_e2e");
	});

	it("open 正常返回", async () => {
		const open = harness.getHandler("browser_open");
		const res = await open({ dirId });
		const payload = JSON.parse(res.content[0].text);
		expect(payload.ok).toBe(true);
		expect(payload.value?.dirId).toBe(dirId);
	});

	it("close 正常返回", async () => {
		const envIds = (process.env.ROXY_DIR_IDS || "")
			.split(",")
			.map((s) => s.trim())
			.filter(Boolean);
		if (envIds.includes(dirId)) {
			// 避免关闭正在使用的环境窗口
			expect(true).toBe(true);
			return;
		}
		const close = harness.getHandler("browser_close");
		const res = await close({ dirId });
		const payload = JSON.parse(res.content[0].text);
		expect(payload.ok).toBe(true);
		expect(payload.value?.dirId).toBe(dirId);
	});
});
