/* 中文注释：xhsShortcuts 收集（DOM 兜底路径）
 * 目标：当 API 无数据时走 DOM fallback，确保分支覆盖。
 */
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

describe("xhsShortcuts 收集（DOM 兜底）", () => {
  it("API 空数组 → DOM evaluate anchors 兜底", async () => {
    vi.resetModules();
    const page = {
      url: vi.fn(() => "https://www.xiaohongshu.com/search_result?keyword=%E7%BE%8E%E9%A3%9F"),
      evaluate: vi.fn(async (fn: any) => {
        // 模拟页面中的链接节点
        return [
          { id: "a1", title: "T1", href: "/explore/a1" },
          { id: "a2", title: "T2", href: "/explore/a2" },
        ];
      }),
    } as any;

    const root = process.cwd();
    const M_PAGES = pathResolve(root, "src/services/pages.js");
    const M_SEARCH = pathResolve(root, "src/domain/xhs/search.js");
    const M_NET = pathResolve(root, "src/domain/xhs/netwatch.js");
    const M_TOOLS = pathResolve(root, "src/mcp/tools/xhsShortcuts.js");

    vi.doMock(M_PAGES, () => ({ ensurePage: async () => page }));
    vi.doMock(M_SEARCH, () => ({ searchKeyword: async () => ({ ok: true, url: page.url(), verified: false }) }));
    vi.doMock(M_NET, () => ({
      waitSearchNotes: (_p: any, _t: number) => ({ promise: Promise.resolve({ ok: true, data: { items: [] } }) })
    }));

    const { registerXhsShortcutsTools } = await import(M_TOOLS);
    const { server, get } = stubServer();
    const manager = fakeManagerWithPage(page);
    registerXhsShortcutsTools(server as any, {} as any, manager as any);

    const collect = get("xhs_collect_search_results");
    const r = await collect({ dirId: "d1", keyword: "美食", limit: 3 });
    const payload = JSON.parse(r.content[0].text);
    expect(payload.ok).toBe(true);
    expect((payload.value?.items || []).length).toBeGreaterThan(0);
    // 由于 API 返回 0 条，诊断应反映 apiCount=0
    expect(payload.value?.diagnostics?.apiCount).toBe(0);
  });
});

