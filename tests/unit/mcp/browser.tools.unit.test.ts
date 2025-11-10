/* 中文注释：browser 工具（单元）——使用假 manager 覆盖无需 CDP 的分支 */
import { describe, it, expect } from "vitest";
import type { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { registerBrowserToolsWithPrefix } from "../../../src/mcp/tools/browser.js";

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
  return {} as any;
}

function fakeManager() {
  return {
    getContext: async () => ({ pages: () => [ { url: () => "about:blank" } ] }),
    closeContext: async () => {},
  } as any;
}

describe("browser 工具（单元）", () => {
  it("open/close 正常返回", async () => {
    const { server, get } = stubServer();
    const container = fakeContainer();
    const manager = fakeManager();
    registerBrowserToolsWithPrefix(server, container as any, manager as any, "browser");

    const open = get("browser_open");
    const close = get("browser_close");

    const r1 = await open({ dirId: "d1" });
    expect(JSON.parse(r1.content[0].text).ok).toBe(true);

    const r2 = await close({ dirId: "d1" });
    expect(JSON.parse(r2.content[0].text).ok).toBe(true);
  });
});

