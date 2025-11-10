/* 中文注释：当指定窗口未打开且无法获取 ws 时，工具应返回 NO_WS_ENDPOINT */
import { describe, it, expect, beforeAll } from "vitest";
import { getContainerAndManager } from "../../../helpers/roxyHarness.js";
import { createMcpHarness } from "../../../helpers/mcpHarness.js";
import { roxySupportsOpen } from "../../../helpers/roxy.js";

const supports = await roxySupportsOpen();
const describeIf = supports ? (describe.skip as typeof describe) : describe;

describeIf("NO_WS_ENDPOINT 统一错误返回（无前置开窗）", () => {
  let harness: ReturnType<typeof createMcpHarness>;
  let dirId: string;

  beforeAll(async () => {
    const { container, manager } = await getContainerAndManager();
    harness = createMcpHarness();
    harness.registerAll(container, manager);
    // 使用一个极可能未运行且后端拒绝开窗的随机窗口 ID
    dirId = `no_ws_${Date.now()}`;
  });

  it("page_navigate 返回 NO_WS_ENDPOINT", async () => {
    const navigate = harness.getHandler("page_navigate");
    const res = await navigate({ dirId, url: "https://example.com" });
    const payload = JSON.parse(res.content[0].text);
    expect(payload.ok).toBe(false);
    expect(payload.code).toBe("NO_WS_ENDPOINT");
    expect(Array.isArray(payload.data?.suggest)).toBe(true);
  }, 120000);

  it("xhs_navigate_home 返回 NO_WS_ENDPOINT", async () => {
    const navHome = harness.getHandler("xhs_navigate_home");
    const res = await navHome({ dirId });
    const payload = JSON.parse(res.content[0].text);
    expect(payload.ok).toBe(false);
    expect(payload.code).toBe("NO_WS_ENDPOINT");
  }, 120000);
});
