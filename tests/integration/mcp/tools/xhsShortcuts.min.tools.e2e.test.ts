/* 中文注释：xhsShortcuts 工具（最小路径）——仅覆盖 search_keyword 正常路径 */
import { describe, it, expect, beforeAll } from "vitest";
import { roxySupportsOpen } from "../../../helpers/roxy.js";
const ready = await roxySupportsOpen();
const describeIf = ready ? describe : (describe.skip as typeof describe);
import { getContainerAndManager, pickDirId } from "../../../helpers/roxyHarness.js";
import { createMcpHarness } from "../../../helpers/mcpHarness.js";
import { installXhsRoutes } from "../../../helpers/routeFulfillKit.js";
import { resolve as pathResolve } from "node:path";

describeIf("xhsShortcuts 工具（最小路径）", () => {
  let dirId: string;
  let harness: ReturnType<typeof createMcpHarness>;
  beforeAll(async () => {
    const { container, manager } = await getContainerAndManager();
    harness = createMcpHarness();
    harness.registerAll(container, manager);
    dirId = await pickDirId("xhs_shortcuts_min_e2e");
    const ctx = await manager.getContext(dirId);
    const fixturesRoot = pathResolve(process.cwd(), "tests/fixtures/xhs");
    await installXhsRoutes(ctx, { root: fixturesRoot });
  });

  it("search_keyword 正常返回", async () => {
    const search = harness.getHandler("xhs_search_keyword");
    const res = await search({ dirId, keyword: "美食" });
    const payload = JSON.parse(res.content[0].text);
    expect(payload.ok).toBe(true);
  }, 20000);
});

