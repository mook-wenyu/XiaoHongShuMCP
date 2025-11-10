/* 中文注释：xhs 工具（handlers 覆盖）——通过 vi.mock 模拟 domain 层 */
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

describe("xhs 工具（handlers 覆盖）", () => {
  it("xhs_session_check / xhs_navigate_home / xhs_open_context", async () => {
    vi.resetModules();
    const { server, get } = stubServer();
    const fakeContainer = {} as any;
    const fakePage = {
      goto: vi.fn(async (_: string) => {}),
      url: vi.fn(() => "https://www.xiaohongshu.com/explore"),
      waitForSelector: vi.fn(async () => {}),
    } as any;
    const fakeManager = { getContext: async () => ({ pages: () => [fakePage] }) } as any;

    const root = process.cwd();
    const M_SESSION = pathResolve(root, "src/domain/xhs/session.js");
    const M_PAGES = pathResolve(root, "src/services/pages.js");
    const M_XHS = pathResolve(root, "src/mcp/tools/xhs.js");

    vi.doMock(M_SESSION, () => ({ checkSession: async () => ({ loggedIn: true }) }));
    vi.doMock(M_PAGES, () => ({ ensurePage: async () => fakePage }));

    const { registerXhsTools } = await import(M_XHS);

    registerXhsTools(server as any, fakeContainer as any, fakeManager as any);

    const check = get("xhs_session_check");
    const navHome = get("xhs_navigate_home");
    const openCtx = get("xhs_open_context");

    let r = await check({ dirId: "d1" });
    expect(JSON.parse(r.content[0].text).ok).toBe(true);

    r = await navHome({ dirId: "d1" });
    expect(JSON.parse(r.content[0].text).ok).toBe(true);

    r = await openCtx({ dirId: "d1" });
    expect(JSON.parse(r.content[0].text).ok).toBe(true);
  });
});

