/* 中文注释：page 工具（定位失败分支）——覆盖 LOCATOR_NOT_FOUND/TIMEOUT */
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

describe("page 工具（定位失败分支）", () => {
  it("click 定位失败 → LOCATOR_NOT_FOUND；type 定位超时 → TIMEOUT", async () => {
    vi.resetModules();
    const root = process.cwd();
    const M_PAGES = pathResolve(root, "src/services/pages.js");
    const M_SELECTORS = pathResolve(root, "src/selectors/index.js");
    const M_PAGE = pathResolve(root, "src/mcp/tools/page.js");
    vi.doMock(M_PAGES, () => ({ ensurePage: async () => ({}) }));
    let once = true;
    vi.doMock(M_SELECTORS, () => ({
      resolveLocatorAsync: async () => {
        if (once) { once = false; throw new Error("not found"); }
        throw new Error("timeout");
      },
    }));
    const { registerPageToolsWithPrefix } = await import(M_PAGE);
    const { server, get } = stubServer();
    registerPageToolsWithPrefix(server as any, {} as any, { getContext: async () => ({}) } as any, "page");
    const click = get("page_click");
    const type = get("page_type");
    let r = await click({ dirId: "d1", target: { selector: "#a" } });
    let p = JSON.parse(r.content[0].text);
    expect(p.ok).toBe(false);
    expect(p.error?.code).toBe("LOCATOR_NOT_FOUND");
    r = await type({ dirId: "d1", target: { selector: "#b" }, text: "t" });
    p = JSON.parse(r.content[0].text);
    expect(p.ok).toBe(false);
    expect(p.code).toBe("TIMEOUT");
  });
});

