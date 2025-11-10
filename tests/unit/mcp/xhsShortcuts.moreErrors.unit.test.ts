/* 中文注释：xhsShortcuts 更多错误分支——search_keyword 抛错、collect_search_results 抛错、keyword_browse 抛错 */
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

function fakeManagerWithPage(page: any) {
  return { getContext: async () => ({ pages: () => [page], newPage: async () => page }) } as any;
}

describe("xhsShortcuts 更多错误分支", () => {
  it("search_keyword/collect/keyword_browse 抛错 → INTERNAL_ERROR", async () => {
    vi.resetModules();
    const page = { screenshot: vi.fn(async () => { throw new Error("shot-fail"); }) } as any;
    const root = process.cwd();
    const M_PAGES = pathResolve(root, "src/services/pages.js");
    const M_SEARCH = pathResolve(root, "src/domain/xhs/search.js");
    const M_NET = pathResolve(root, "src/domain/xhs/netwatch.js");
    const M_ACTIONS = pathResolve(root, "src/humanization/actions.js");
    const M_TOOLS = pathResolve(root, "src/mcp/tools/xhsShortcuts.js");
    vi.doMock(M_PAGES, () => ({ ensurePage: async () => page }));
    vi.doMock(M_SEARCH, () => ({ searchKeyword: async () => { throw new Error("boom"); } }));
    vi.doMock(M_NET, () => ({ waitSearchNotes: (_p: any, _t: number) => ({ promise: Promise.reject(new Error("net")) }) }));
    vi.doMock(M_ACTIONS, () => ({ scrollHuman: async () => { throw new Error("scroll"); } }));
    const { registerXhsShortcutsTools } = await import(M_TOOLS);
    const { server, get } = stubServer();
    const manager = fakeManagerWithPage(page);
    registerXhsShortcutsTools(server as any, {} as any, manager as any);
    const search = get("xhs_search_keyword");
    const collect = get("xhs_collect_search_results");
    const browse = get("xhs_keyword_browse");
    for (const h of [search, collect, browse]) {
      const r = await h({ dirId: "d1", keyword: "美食", keywords: ["美食"] });
      const p = JSON.parse(r.content[0].text);
      expect(p.ok).toBe(false);
      expect(p.code).toBe("INTERNAL_ERROR");
    }
  });
});

