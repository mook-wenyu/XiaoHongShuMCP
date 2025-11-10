/* 中文注释：xhsShortcuts 笔记动作失败分支（ACTION_FAILED） */
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

describe("xhsShortcuts 笔记动作失败分支", () => {
  it("unlike 返回 ok=false → ACTION_FAILED", async () => {
    vi.resetModules();
    const page = {} as any;
    const root = process.cwd();
    const M_PAGES = pathResolve(root, "src/services/pages.js");
    const M_NOTE_ACT = pathResolve(root, "src/domain/xhs/noteActions.js");
    const M_TOOLS = pathResolve(root, "src/mcp/tools/xhsShortcuts.js");
    vi.doMock(M_PAGES, () => ({ ensurePage: async () => page }));
    vi.doMock(M_NOTE_ACT, () => ({ unlikeCurrent: async () => ({ ok: false, message: "deny" }) }));
    const { registerXhsShortcutsTools } = await import(M_TOOLS);
    const { server, get } = stubServer();
    const manager = fakeManagerWithPage(page);
    registerXhsShortcutsTools(server as any, {} as any, manager as any);
    const unlike = get("xhs_note_unlike");
    const r = await unlike({ dirId: "d1" });
    const payload = JSON.parse(r.content[0].text);
    expect(payload.ok).toBe(false);
    expect(payload.code).toBe("ACTION_FAILED");
  });
});

