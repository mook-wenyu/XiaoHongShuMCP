/* 中文注释：page 工具（额外场景）全量集成测试
 * 目标：覆盖 type/input.clear/screenshot 分支，提高 page.ts 覆盖率
 */
import { describe, it, expect, beforeAll } from "vitest";
import { roxySupportsOpen } from "../../../helpers/roxy.js";
const ready = await roxySupportsOpen();
const describeIf = ready ? describe : (describe.skip as typeof describe);
import { getContainerAndManager, pickDirId } from "../../../helpers/roxyHarness.js";
import { createMcpHarness } from "../../../helpers/mcpHarness.js";
import { installXhsRoutes } from "../../../helpers/routeFulfillKit.js";
import { resolve as pathResolve } from "node:path";
import { stat } from "node:fs/promises";

describeIf("page 工具（额外场景）全量集成", () => {
  let dirId: string;
  let fixturesRoot: string;
  let harness: ReturnType<typeof createMcpHarness>;

  beforeAll(async () => {
    const { container, manager } = await getContainerAndManager();
    harness = createMcpHarness();
    harness.registerAll(container, manager);
    dirId = await pickDirId("page_extra_e2e");
    const ctx = await manager.getContext(dirId);
    fixturesRoot = pathResolve(process.cwd(), "tests/fixtures/xhs");
    await installXhsRoutes(ctx, { root: fixturesRoot });
    // 确保在发现页
    const navigate = harness.getHandler("page_navigate");
    await navigate({ dirId, url: "https://www.xiaohongshu.com/explore" });
  });

  it("type(human=false) → input.clear(human=false) → type(human=true)", async () => {
    const type = harness.getHandler("page_type");
    const clear = harness.getHandler("page_input_clear");

    let res = await type({
      dirId,
      target: { placeholder: "搜索小红书" },
      text: "咖啡",
      human: false,
    });
    let payload = JSON.parse(res.content[0].text);
    expect(payload.ok).toBe(true);

    res = await clear({ dirId, target: { placeholder: "搜索小红书" }, human: false });
    payload = JSON.parse(res.content[0].text);
    expect(payload.ok).toBe(true);

    res = await type({
      dirId,
      target: { placeholder: "搜索小红书" },
      text: "奶茶",
      human: true, // human=true 触发快速档
    });
    payload = JSON.parse(res.content[0].text);
    expect(payload.ok).toBe(true);
  }, 30000);

  it("screenshot 正常返回路径并存在文件", async () => {
    const shot = harness.getHandler("page_screenshot");
    const res = await shot({ dirId, fullPage: true, returnImage: false });
    const payload = JSON.parse(res.content[0].text);
    expect(payload.ok).toBe(true);
    const abs = pathResolve(process.cwd(), String(payload.value?.path));
    const st = await stat(abs);
    expect(st.isFile()).toBe(true);
  }, 30000);
});

