/* 中文注释：xhsShortcuts 工具（handlers 更多覆盖）——mock domain 层让 search/collect/close 覆盖核心分支 */
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

describe("xhsShortcuts 工具（handlers 更多覆盖）", () => {
  it("search_keyword 返回 ok；collect_search_results 返回 ok（API 路径）", async () => {
    vi.resetModules();
    const page = {
      url: vi.fn(() => "https://www.xiaohongshu.com/search_result?keyword=%E7%BE%8E%E9%A3%9F"),
      waitForSelector: vi.fn(async () => {}),
    } as any;

    const root = process.cwd();
    const M_PAGES = pathResolve(root, "src/services/pages.js");
    const M_SEARCH = pathResolve(root, "src/domain/xhs/search.js");
    const M_NET = pathResolve(root, "src/domain/xhs/netwatch.js");
    const M_TOOLS = pathResolve(root, "src/mcp/tools/xhsShortcuts.js");

    vi.doMock(M_PAGES, () => ({ ensurePage: async () => page }));
    vi.doMock(M_SEARCH, () => ({ searchKeyword: async () => ({ ok: true, url: page.url(), verified: true }) }));
    vi.doMock(M_NET, () => ({
      waitSearchNotes: (_p: any, _t: number) => ({ promise: Promise.resolve({ ok: true, data: { items: [{ id: "n1", note_card: { display_title: "t1" } }] } }) })
    }));

    const { registerXhsShortcutsTools } = await import(M_TOOLS);
    const { server, get } = stubServer();
    const manager = fakeManagerWithPage(page);
    const container = {} as any;
    registerXhsShortcutsTools(server as any, container as any, manager as any);

    const search = get("xhs_search_keyword");
    const collect = get("xhs_collect_search_results");

    let r = await search({ dirId: "d1", keyword: "美食" });
    expect(JSON.parse(r.content[0].text).ok).toBe(true);
    r = await collect({ dirId: "d1", keyword: "美食", limit: 3 });
    expect(JSON.parse(r.content[0].text).ok).toBe(true);
  });

  it("close_modal 返回 true", async () => {
    vi.resetModules();
    const page = {} as any;
    const root = process.cwd();
    const M_PAGES = pathResolve(root, "src/services/pages.js");
    const M_NAV = pathResolve(root, "src/domain/xhs/navigation.js");
    const M_TOOLS = pathResolve(root, "src/mcp/tools/xhsShortcuts.js");
    vi.doMock(M_PAGES, () => ({ ensurePage: async () => page }));
    vi.doMock(M_NAV, () => ({ closeModalIfOpen: async () => true }));
    const { registerXhsShortcutsTools } = await import(M_TOOLS);
    const { server, get } = stubServer();
    const manager = fakeManagerWithPage(page);
    registerXhsShortcutsTools(server as any, {} as any, manager as any);
    const close = get("xhs_close_modal");
    const res = await close({ dirId: "d1" });
    const payload = JSON.parse(res.content[0].text);
    expect(payload.ok).toBe(true);
    expect(payload.value?.closed).toBe(true);
  });
});

