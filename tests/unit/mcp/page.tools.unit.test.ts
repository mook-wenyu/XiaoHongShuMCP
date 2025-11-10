/* 中文注释：page 工具（单元）——使用假 manager 覆盖无需 CDP 的分支
 * 覆盖 create/list/navigate/close/screenshot（成功/失败）
 */
import { describe, it, expect } from "vitest";
import type { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { registerPageToolsWithPrefix } from "../../../src/mcp/tools/page.js";

function createStubServer() {
  const handlers = new Map<string, any>();
  const server: McpServer = {
    registerTool(name: string, _schema: any, handler: any) {
      handlers.set(name, handler);
    },
  } as any;
  return { server, get: (n: string) => handlers.get(n) };
}

function createFakeContainer() {
  return {
    getHumanizationProfileKey() {
      return "rapid";
    },
  } as any;
}

function createFakeManager() {
  return {
    listPages: async () => ({ pages: [{ url: "about:blank" }] }),
    createPage: async (_dirId: string, url?: string) => ({ index: 0, url: url || "about:blank" }),
    closePage: async () => true,
    navigate: async (_dirId: string, url: string) => ({ url }),
    screenshot: async () => ({ path: "artifacts/fake/s1.png", buffer: Buffer.from([1, 2, 3]) }),
  } as any;
}

describe("page 工具（单元）", () => {
  it("create/list/navigate/close/screenshot 成功路径", async () => {
    const { server, get } = createStubServer();
    const container = createFakeContainer();
    const manager = createFakeManager();
    registerPageToolsWithPrefix(server, container as any, manager as any, "page");

    const create = get("page_create");
    const list = get("page_list");
    const navigate = get("page_navigate");
    const close = get("page_close");
    const shot = get("page_screenshot");

    let r = await create({ dirId: "d1", url: "https://example.com" });
    expect(JSON.parse(r.content[0].text).ok).toBe(true);

    r = await list({ dirId: "d1" });
    expect(JSON.parse(r.content[0].text).ok).toBe(true);
    expect(Array.isArray(JSON.parse(r.content[0].text).data?.pages)).toBe(true);

    r = await navigate({ dirId: "d1", url: "https://example.com" });
    expect(JSON.parse(r.content[0].text).ok).toBe(true);

    r = await close({ dirId: "d1" });
    expect(JSON.parse(r.content[0].text).ok).toBe(true);

    r = await shot({ dirId: "d1", fullPage: true });
    const payload = JSON.parse(r.content[0].text);
    expect(payload.ok).toBe(true);
    expect(typeof payload.value?.path).toBe("string");
  });

  it("screenshot 失败路径（映射为 SCREENSHOT_FAILED）", async () => {
    const { server, get } = createStubServer();
    const container = createFakeContainer();
    const manager = createFakeManager();
    manager.screenshot = async () => { throw new Error("disk error"); };
    registerPageToolsWithPrefix(server, container as any, manager as any, "page");

    const shot = get("page_screenshot");
    const r = await shot({ dirId: "d1", fullPage: false });
    const payload = JSON.parse(r.content[0].text);
    expect(payload.ok).toBe(false);
    expect(payload.code).toBe("SCREENSHOT_FAILED");
  });
});

