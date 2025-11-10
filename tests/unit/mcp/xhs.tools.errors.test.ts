/* 中文注释：xhs 工具（错误与输入校验覆盖）
 * 目标：覆盖 xhs_navigate_home 失败、xhs_session_check NO_WS、xhs_note_extract_content INVALID_INPUT。
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

describe("xhs 工具（错误与输入校验覆盖）", () => {
  it("xhs_navigate_home 失败 → NAVIGATE_FAILED；xhs_session_check → NO_WS_ENDPOINT；note_extract invalid input", async () => {
    vi.resetModules();
    const root = process.cwd();
    const M_PAGES = pathResolve(root, "src/services/pages.js");
    const M_XHS = pathResolve(root, "src/mcp/tools/xhs.js");

    const fakePage = { goto: vi.fn(async () => { throw new Error("net::ERR_ABORTED"); }), url: vi.fn(() => "about:blank") } as any;
    const fakeManager = {
      getContext: async (_: string) => { throw new Error("connectOverCDP failed"); },
    } as any;

    vi.doMock(M_PAGES, () => ({ ensurePage: async () => fakePage }));

    const { registerXhsTools } = await import(M_XHS);
    const { server, get } = stubServer();
    const fakeContainer = {} as any;
    // 注意：第一次注册用能 throw 的 manager；随后为 navHome 失败需要 manager 能 getContext 成功
    registerXhsTools(server as any, fakeContainer as any, fakeManager as any);

    // session_check → NO_WS_ENDPOINT
    const sess = get("xhs_session_check");
    let r = await sess({ dirId: "d1" });
    let payload = JSON.parse(r.content[0].text);
    expect(payload.ok).toBe(false);
    expect(payload.code).toBe("NO_WS_ENDPOINT");

    // 重新注册一次，仅用于 navHome 失败与 note_extract invalid input（manager 不抛错）
    const okManager = { getContext: async () => ({}) } as any;
    const { server: server2, get: get2 } = stubServer();
    registerXhsTools(server2 as any, fakeContainer as any, okManager as any);
    const navHome = get2("xhs_navigate_home");
    const extract = get2("xhs_note_extract_content");

    // navHome → NAVIGATE_FAILED（因为 ensurePage 的 page.goto 会抛错）
    r = await navHome({ dirId: "d1" });
    payload = JSON.parse(r.content[0].text);
    expect(payload.ok).toBe(false);
    expect(payload.code).toBe("NAVIGATE_FAILED");

    // invalid input
    r = await extract({ dirId: "d1" });
    payload = JSON.parse(r.content[0].text);
    expect(payload.ok).toBe(false);
    expect(payload.code).toBe("INVALID_INPUT");
  });
});

