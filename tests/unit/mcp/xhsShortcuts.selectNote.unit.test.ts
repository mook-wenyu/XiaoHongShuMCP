/* 中文注释：xhsShortcuts 选择笔记（select_note）——覆盖 invalid params 与成功路径（导航/匹配模拟） */
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

describe("xhsShortcuts select_note（单元）", () => {
  it("invalid params & success 路径", async () => {
    vi.resetModules();
    const page = { url: () => "https://www.xiaohongshu.com/explore" } as any;

    const root = process.cwd();
    const M_PAGES = pathResolve(root, "src/services/pages.js");
    const M_NAV = pathResolve(root, "src/domain/xhs/navigation.js");
    const M_TOOLS = pathResolve(root, "src/mcp/tools/xhsShortcuts.js");

    vi.doMock(M_PAGES, () => ({ ensurePage: async () => page }));
    vi.doMock(M_NAV, () => ({
      PageType: { ExploreHome: 1, Discover: 2, Search: 3 },
      detectPageType: async () => 2,
      ensureDiscoverPage: async () => {},
      findAndOpenNoteByKeywords: async () => ({ matched: true, modalOpen: true, openedPath: "dom", feedVerified: true, feedItems: 10, feedType: "api", feedTtfbMs: 100 }),
    }));

    const { registerXhsShortcutsTools } = await import(M_TOOLS);
    const { server, get } = stubServer();
    const manager = fakeManagerWithPage(page);
    registerXhsShortcutsTools(server as any, {} as any, manager as any);

    const sel = get("xhs_select_note");

    // invalid params（空 keywords）
    let r = await sel({ dirId: "d1", keywords: [] });
    let payload = JSON.parse(r.content[0].text);
    expect(payload.ok).toBe(false);
    expect(payload.code).toBe("INVALID_PARAMS");

    // success
    r = await sel({ dirId: "d1", keywords: ["美食"] });
    payload = JSON.parse(r.content[0].text);
    expect(payload.ok).toBe(true);
    expect(payload.value?.opened).toBe(true);
  });
});

