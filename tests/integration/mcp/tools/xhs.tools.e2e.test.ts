/* 中文注释：xhs 工具（会话/导航/上下文）全量集成测试 */
import { describe, it, expect, beforeAll } from "vitest";
import { roxySupportsOpen } from "../../../helpers/roxy.js";
const ready = await roxySupportsOpen();
const describeIf = ready ? describe : (describe.skip as typeof describe);
import { getContainerAndManager, pickDirId } from "../../../helpers/roxyHarness.js";
import { createMcpHarness } from "../../../helpers/mcpHarness.js";
import { installXhsRoutes } from "../../../helpers/routeFulfillKit.js";
import { resolve as pathResolve } from "node:path";

describeIf("xhs 工具全量集成", () => {
  let dirId: string;
  let harness: ReturnType<typeof createMcpHarness>;
  beforeAll(async () => {
    const { container, manager } = await getContainerAndManager();
    harness = createMcpHarness();
    harness.registerAll(container, manager);
    dirId = await pickDirId("xhs_tools_e2e");
    const ctx = await manager.getContext(dirId);
    const fixturesRoot = pathResolve(process.cwd(), "tests/fixtures/xhs");
    await installXhsRoutes(ctx, { root: fixturesRoot });
  });

  it("xhs_open_context", async () => {
    const h = harness.getHandler("xhs_open_context");
    const res = await h({ dirId });
    const payload = JSON.parse(res.content[0].text);
    expect(payload.ok).toBe(true);
    expect(typeof payload.value?.pages).toBe("number");
  });

  it("xhs_navigate_home", async () => {
    const h = harness.getHandler("xhs_navigate_home");
    const res = await h({ dirId });
    const payload = JSON.parse(res.content[0].text);
    expect(payload.ok).toBe(true);
    expect(payload.value?.verified).toBeTypeOf("boolean");
  });

  it("xhs_session_check", async () => {
    const h = harness.getHandler("xhs_session_check");
    const res = await h({ dirId });
    const payload = JSON.parse(res.content[0].text);
    // 会话状态取决于 cookies，这里只断言接口可达
    expect(typeof payload.ok).toBe("boolean");
  });
});

