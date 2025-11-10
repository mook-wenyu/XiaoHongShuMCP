/* 中文注释：page 工具（更多覆盖）——覆盖 screenshot returnImage 分支与 scroll 人类化分支 */
import { describe, it, expect, vi } from "vitest";
import type { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { resolve as pathResolve } from "node:path";

function stubServer() {
  const handlers = new Map<string, any>();
  const server: McpServer = {
    registerTool(name: string, _schema: any, handler: any) {
      handlers.set(name, handler);
    },
  } as any;
  return { server, get: (n: string) => handlers.get(n) };
}

function fakeContainer() {
  return { getHumanizationProfileKey: () => "rapid" } as any;
}

describe("page 工具（更多覆盖）", () => {
  it("screenshot 返回图片、scroll 人类化", async () => {
    vi.resetModules();
    const root = process.cwd();
    const M_PAGE = pathResolve(root, "src/mcp/tools/page.js");

    const { server, get } = stubServer();
    const container = fakeContainer();
    const manager = {
      screenshot: async () => ({ path: "artifacts/fake/s.png", buffer: Buffer.from([1,2,3]) }),
      getContext: async () => ({}),
    } as any;

    const M_PAGES = pathResolve(root, "src/services/pages.js");
    const M_ACTIONS = pathResolve(root, "src/humanization/actions.js");
    const M_OPTIONS = pathResolve(root, "src/humanization/options.js");
    vi.doMock(M_PAGES, () => ({ ensurePage: async () => ({}) }));
    vi.doMock(M_ACTIONS, () => ({ scrollHuman: async () => {}, moveMouseTo: async () => {} }));
    vi.doMock(M_OPTIONS, () => ({ buildScrollOptions: () => ({}) }));

    const { registerPageToolsWithPrefix } = await import(M_PAGE);
    registerPageToolsWithPrefix(server as any, container as any, manager as any, "page");

    const shot = get("page_screenshot");
    const scr = get("page_scroll");

    const r1 = await shot({ dirId: "d1", returnImage: true });
    const p1 = r1.content.find((c: any) => c.type === "image");
    expect(p1).toBeTruthy();

    const r2 = await scr({ dirId: "d1", human: true, deltaY: 300 });
    expect(JSON.parse(r2.content[0].text).ok).toBe(true);
  });
});

