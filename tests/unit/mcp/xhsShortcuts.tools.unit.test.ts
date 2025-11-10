/* 中文注释：xhsShortcuts 工具（单元）——通过 vi.mock 模拟页面与导航/网络模块，覆盖成功与失败路径 */
import { describe, it, expect, vi, beforeAll } from "vitest";
import type { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { resolve as pathResolve } from "node:path";

function createStubServer() {
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

describe("xhsShortcuts 工具（单元）", () => {
  it("navigate_discover 成功 + close_modal 返回 closed=false", async () => {
    vi.resetModules();
    const actions: any[] = [];
    const page = {
      goto: vi.fn(async (_: string) => actions.push("goto")),
      url: vi.fn(() => "https://www.xiaohongshu.com/explore"),
      waitForSelector: vi.fn(async () => {}),
      screenshot: vi.fn(async () => {}),
    };

    const root = process.cwd();
    const M_PAGES = pathResolve(root, "src/services/pages.js");
    const M_NAV = pathResolve(root, "src/domain/xhs/navigation.js");
    const M_NET = pathResolve(root, "src/domain/xhs/netwatch.js");
    const M_TOOLS = pathResolve(root, "src/mcp/tools/xhsShortcuts.js");

    vi.doMock(M_PAGES, () => ({ ensurePage: async () => page }));
    vi.doMock(M_NAV, () => ({ ensureDiscoverPage: async () => {}, closeModalIfOpen: async () => false }));
    vi.doMock(M_NET, () => ({
      waitHomefeed: (_p: any, _t: number) => ({ promise: Promise.resolve({ ok: true, data: { items: [{}, {}, {}] }, ttfbMs: 120 }) }),
      waitSearchNotes: (_p: any, _t: number) => ({ promise: Promise.resolve({ ok: true, data: { items: [] }, ttfbMs: 100 }) }),
    }));

    const { registerXhsShortcutsTools } = await import(M_TOOLS);

    const { server, get } = createStubServer();
    const manager = fakeManagerWithPage(page);
    // 容器仅用于一致的签名，内部不用
    const container = {} as any;
    registerXhsShortcutsTools(server as any, container as any, manager as any);

    const nav = get("xhs_navigate_discover");
    const res = await nav({ dirId: "d1" });
    const payload = JSON.parse(res.content[0].text);
    expect(payload.ok).toBe(true);
    expect(payload.value?.target).toBe("discover");

    const close = get("xhs_close_modal");
    const res2 = await close({ dirId: "d1" });
    const p2 = JSON.parse(res2.content[0].text);
    expect(p2.ok).toBe(true);
    expect(p2.value?.closed).toBe(false);
  });

  it("navigate_discover 失败：应生成截图占位路径", async () => {
    vi.resetModules();
    const page = {
      goto: vi.fn(async () => { throw new Error("NAV_ERR"); }),
      url: vi.fn(() => "about:blank"),
      screenshot: vi.fn(async () => { throw new Error("shot-fail"); }),
    };

    const root = process.cwd();
    const M_PAGES = pathResolve(root, "src/services/pages.js");
    const M_NAV = pathResolve(root, "src/domain/xhs/navigation.js");
    const M_TOOLS = pathResolve(root, "src/mcp/tools/xhsShortcuts.js");

    vi.doMock(M_PAGES, () => ({ ensurePage: async () => page }));
    vi.doMock(M_NAV, () => ({ ensureDiscoverPage: async () => { throw new Error("NAV_ERR"); } }));

    const { registerXhsShortcutsTools } = await import(M_TOOLS);

    const { server, get } = createStubServer();
    const manager = fakeManagerWithPage(page);
    const container = {} as any;
    registerXhsShortcutsTools(server as any, container as any, manager as any);

    const nav = get("xhs_navigate_discover");
    const res = await nav({ dirId: "d1" });
    const payload = JSON.parse(res.content[0].text);
    expect(payload.ok).toBe(false);
    expect((payload.error?.screenshotPath ?? payload.data?.screenshotPath)).toBeDefined();
  });
});

