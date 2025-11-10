/* 中文注释：真实快照（live fixtures）健康断言
 * 目的：验证采集的 HTML 快照是否满足选择器契约，避免快照陈旧导致的“虚假绿灯”。
 */
import { describe, it, expect, beforeAll } from "vitest";
import { getContainerAndManager, pickDirId } from "../../helpers/roxyHarness.js";
import { installXhsRoutes } from "../../helpers/routeFulfillKit.js";
import { resolveLocatorResilient } from "../../../src/selectors/index.js";

const useLive = true;

describe("XHS live fixtures 健康断言", () => {
  let dirId: string;
  let ctx: Awaited<ReturnType<(typeof import("playwright"))>["chromium"]["launchPersistentContext"]>>;
  beforeAll(async () => {
    const { manager } = await getContainerAndManager();
    dirId = await pickDirId("xhs_live_fixture");
    ctx = (await manager.getContext(dirId)) as any;
    await installXhsRoutes(ctx as any, {
      root: "tests/fixtures/xhs",
      variant: useLive ? "live" : "synthetic",
    });
  });

  it("explore.html.live 必须包含搜索输入与提交按钮与发现链接", async () => {
    const page = (await (ctx as any).newPage()) as any;
    await page.goto("https://www.xiaohongshu.com/explore", { waitUntil: "domcontentloaded" });

    const input = await resolveLocatorResilient(page, { alternatives: [
      { role: "textbox", name: { contains: "搜索小红书" } },
      { placeholder: "搜索小红书" },
      { selector: "#search-input.search-input" },
      { selector: ".input-box input.search-input" },
    ]}, { selectorId: "search-input", verifyTimeoutMs: 400, retryAttempts: 1 });
    expect(input).toBeTruthy();

    const submit = await resolveLocatorResilient(page, { alternatives: [
      { role: "button", name: { regex: "^(搜索|查找|Search)$" } },
      { selector: ".input-box .input-button" },
      { selector: ".input-box .search-icon" },
    ]}, { selectorId: "search-submit", verifyTimeoutMs: 400, retryAttempts: 1 });
    expect(submit).toBeTruthy();

    const discover = await resolveLocatorResilient(page, { alternatives: [
      { role: "link", name: { exact: "发现" } },
      { text: { exact: "发现" } },
      { selector: 'a[href*="channel_id=homefeed_recommend"]' },
    ]}, { selectorId: "nav-discover", verifyTimeoutMs: 400, retryAttempts: 1 });
    expect(discover).toBeTruthy();
  }, 20000);
});

