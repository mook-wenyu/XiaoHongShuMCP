/* 中文注释：xhsShortcuts 笔记/用户动作（单元）——覆盖 like/unlike/collect/uncollect/follow/unfollow 与 keyword_browse */
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

describe("xhsShortcuts 笔记/用户动作与浏览（单元）", () => {
  it("like/unlike/collect/uncollect/follow/unfollow 返回 ok；keyword_browse 返回 ok", async () => {
    vi.resetModules();
    const page = {
      url: vi.fn(() => "https://www.xiaohongshu.com/search_result?keyword=%E7%BE%8E%E9%A3%9F"),
      waitForTimeout: vi.fn(async () => {}),
    } as any;

    const root = process.cwd();
    const M_PAGES = pathResolve(root, "src/services/pages.js");
    const M_ACTIONS = pathResolve(root, "src/humanization/actions.js");
    const M_SEARCH = pathResolve(root, "src/domain/xhs/search.js");
    const M_NOTE_ACT = pathResolve(root, "src/domain/xhs/noteActions.js");
    const M_TOOLS = pathResolve(root, "src/mcp/tools/xhsShortcuts.js");

    vi.doMock(M_PAGES, () => ({ ensurePage: async () => page }));
    vi.doMock(M_ACTIONS, () => ({ scrollHuman: async () => {} }));
    vi.doMock(M_SEARCH, () => ({ searchKeyword: async () => ({ ok: true, url: page.url(), verified: true, matchedCount: 2 }) }));
    vi.doMock(M_NOTE_ACT, () => ({
      likeCurrent: async () => ({ ok: true }),
      unlikeCurrent: async () => ({ ok: true }),
      collectCurrent: async () => ({ ok: true }),
      uncollectCurrent: async () => ({ ok: true }),
      followAuthor: async () => ({ ok: true }),
      unfollowAuthor: async () => ({ ok: true }),
    }));

    const { registerXhsShortcutsTools } = await import(M_TOOLS);
    const { server, get } = stubServer();
    const manager = fakeManagerWithPage(page);
    registerXhsShortcutsTools(server as any, {} as any, manager as any);

    const like = get("xhs_note_like");
    const unlike = get("xhs_note_unlike");
    const collect = get("xhs_note_collect");
    const uncollect = get("xhs_note_uncollect");
    const follow = get("xhs_user_follow");
    const unfollow = get("xhs_user_unfollow");
    const browse = get("xhs_keyword_browse");

    for (const h of [like, unlike, collect, uncollect, follow, unfollow]) {
      const r = await h({ dirId: "d1" });
      expect(JSON.parse(r.content[0].text).ok).toBe(true);
    }

    const br = await browse({ dirId: "d1", keywords: ["美食", "穿搭"] });
    const payload = JSON.parse(br.content[0].text);
    expect(payload.ok).toBe(true);
    expect(payload.value?.browsed).toBe(true);
  });
});

