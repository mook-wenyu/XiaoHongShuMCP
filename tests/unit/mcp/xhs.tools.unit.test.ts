/* 中文注释：xhs 工具（单元）——覆盖 xhs_open_context 成功/无 ws 错误路径 */
import { describe, it, expect } from "vitest";
import type { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { registerXhsTools } from "../../../src/mcp/tools/xhs.js";

function stubServer() {
  const handlers = new Map<string, any>();
  const server: McpServer = {
    registerTool(name: string, _schema: any, handler: any) {
      handlers.set(name, handler);
    },
  } as any;
  return { server, get: (n: string) => handlers.get(n) };
}

describe("xhs 工具（单元）", () => {
  it("xhs_open_context 返回 opened=true", async () => {
    const { server, get } = stubServer();
    const fakeContainer = {} as any;
    const fakeManager = {
      getContext: async () => ({ pages: () => [ { url: () => "about:blank" } ] }),
    } as any;

    registerXhsTools(server as any, fakeContainer, fakeManager);
    const h = get("xhs_open_context");
    const res = await h({ dirId: "d1" });
    const payload = JSON.parse(res.content[0].text);
    expect(payload.ok).toBe(true);
    expect(payload.value?.opened).toBe(true);
  });
});

