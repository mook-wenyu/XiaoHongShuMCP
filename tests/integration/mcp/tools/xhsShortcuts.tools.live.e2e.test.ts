/* 中文注释：xhsShortcuts 工具（Live 直连、不拦截）集成测试
 * 说明：
 * - 仅在 XHS_LIVE_E2E=true 且 Roxy/CDP 可用、会话有效时运行
 * - 不安装任何路由拦截（不使用 route.fulfill），真实访问站点
 */
import { describe, it, expect, beforeAll } from "vitest";
import { roxySupportsOpen } from "../../../helpers/roxy.js";
import { getContainerAndManager, pickDirId } from "../../../helpers/roxyHarness.js";
import { createMcpHarness } from "../../../helpers/mcpHarness.js";

const liveEnabled = String(process.env.XHS_LIVE_E2E || "").toLowerCase() === "true";

async function sessionReady(): Promise<boolean> {
  try {
    const { container, manager } = await getContainerAndManager();
    const harness = createMcpHarness();
    harness.registerAll(container, manager);
    const dirId = await pickDirId("xhs_live");
    const sessionCheck = harness.getHandler("xhs_session_check");
    const res = await sessionCheck({ dirId, workspaceId: process.env.ROXY_DEFAULT_WORKSPACE_ID });
    const payload = JSON.parse(res.content[0].text);
    await container.cleanup().catch(() => {});
    return !!payload?.ok && !!payload?.value?.loggedIn;
  } catch {
    return false;
  }
}

const ready = await roxySupportsOpen();
const loggedIn = liveEnabled ? await sessionReady() : false;
const describeIf = liveEnabled && ready && loggedIn ? describe : (describe.skip as typeof describe);

describeIf("xhsShortcuts Live 直连集成（不拦截）", () => {
  let dirId: string;
  let harness: ReturnType<typeof createMcpHarness>;
  let managerRef: Awaited<ReturnType<typeof getContainerAndManager>>["manager"];

  beforeAll(async () => {
    const { container, manager } = await getContainerAndManager();
    managerRef = manager;
    harness = createMcpHarness();
    harness.registerAll(container, manager);
    dirId = await pickDirId("xhs_live_e2e");
    // 不安装任何路由拦截（真实直连）
  });

  it("navigate_discover 真实环境", async () => {
    const nav = harness.getHandler("xhs_navigate_discover");
    const res = await nav({ dirId, workspaceId: process.env.ROXY_DEFAULT_WORKSPACE_ID });
    const payload = JSON.parse(res.content[0].text);
    expect(payload.ok).toBe(true);
    const url: string = String(payload.value?.url || "");
    // 允许中间跳转参数，但应指向 /explore（或其推荐流 channel）
    expect(url.includes("/explore")).toBe(true);
    expect(url.includes("website-login/captcha")).toBe(false);
  }, 60000);

  it("search_keyword + collect_search_results 真实环境", async () => {
    const search = harness.getHandler("xhs_search_keyword");
    const collect = harness.getHandler("xhs_collect_search_results");

    const res1 = await search({ dirId, keyword: "美食", workspaceId: process.env.ROXY_DEFAULT_WORKSPACE_ID });
    const p1 = JSON.parse(res1.content[0].text);
    expect(p1.ok).toBe(true);
    // URL 应进入搜索结果页；verified 依赖接口可用性，不做强制断言
    expect(String(p1.value?.url || "")).toContain("/search_result?keyword=");

    const res2 = await collect({ dirId, keyword: "美食", limit: 5, workspaceId: process.env.ROXY_DEFAULT_WORKSPACE_ID });
    const p2 = JSON.parse(res2.content[0].text);
    expect(p2.ok).toBe(true);
    expect(Array.isArray(p2.value?.items)).toBe(true);
    expect((p2.value?.items || []).length).toBeGreaterThan(0);
  }, 120000);
});
