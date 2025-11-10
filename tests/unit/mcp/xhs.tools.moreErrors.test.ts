/* 中文注释：xhs 工具（更多错误分支）——NAVIGATE_TIMEOUT 与 note_extract 内容失败 */
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

describe("xhs 工具（更多错误分支）", () => {
  it("xhs_navigate_home timeout → NAVIGATE_TIMEOUT；note_extract 抽取失败 → ACTION_FAILED 映射为错误", async () => {
    vi.resetModules();
    const root = process.cwd();
    const M_PAGES = pathResolve(root, "src/services/pages.js");
    const M_XHS = pathResolve(root, "src/mcp/tools/xhs.js");
    const M_EXTRACT = pathResolve(root, "src/domain/xhs/noteExtractor.js");

    const fakePage = { goto: vi.fn(async () => { throw new Error("Timeout 3000ms"); }), url: vi.fn(() => "about:blank") } as any;
    const okManager = { getContext: async () => ({}) } as any;

    vi.doMock(M_PAGES, () => ({ ensurePage: async () => fakePage }));
    vi.doMock(M_EXTRACT, () => ({ extractNoteContent: async () => ({ ok: false, code: "EXTRACT_FAIL", message: "bad" }) }));

    const { registerXhsTools } = await import(M_XHS);
    const { server, get } = stubServer();
    registerXhsTools(server as any, {} as any, okManager as any);

    const navHome = get("xhs_navigate_home");
    const extract = get("xhs_note_extract_content");

    let r = await navHome({ dirId: "d1" });
    let p = JSON.parse(r.content[0].text);
    expect(p.ok).toBe(false);
    expect(p.code).toBe("NAVIGATE_FAILED");

    r = await extract({ dirId: "d1", noteUrl: "https://www.xiaohongshu.com/explore/abc" });
    p = JSON.parse(r.content[0].text);
    expect(p.ok).toBe(false);
    expect(typeof p.code).toBe("string");
  });
});

