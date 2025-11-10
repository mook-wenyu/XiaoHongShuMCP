/* 中文注释：page 工具（错误与分支覆盖）
 * 目标：覆盖 hover 失败（skipped=true）、click 动作拦截、type 非人类化、input.clear 人类化分支。
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

function fakeContainer() {
  return { getHumanizationProfileKey: () => "rapid" } as any;
}

describe("page 工具（错误与分支覆盖）", () => {
  it("hover 失败 → skipped=true；click 动作被拦截 → ACTION_INTERCEPTED；type 非人类化；clear 人类化", async () => {
    vi.resetModules();
    const root = process.cwd();
    const M_PAGES = pathResolve(root, "src/services/pages.js");
    const M_SELECTORS = pathResolve(root, "src/selectors/index.js");
    const M_ACTIONS = pathResolve(root, "src/humanization/actions.js");
    const M_OPTIONS = pathResolve(root, "src/humanization/options.js");
    const M_TRACE = pathResolve(root, "src/humanization/trace.js");
    const M_PAGE = pathResolve(root, "src/mcp/tools/page.js");

    const fakePage: any = {
      mouse: { wheel: vi.fn(async () => {}) },
    };

    vi.doMock(M_PAGES, () => ({ ensurePage: async () => fakePage }));
    vi.doMock(M_SELECTORS, () => ({
      resolveLocatorAsync: async (_page: any, target: any) => {
        // 针对不同 selectorId 构造不同行为
        const clickLoc = {
          waitFor: async () => {},
          click: async () => { throw new Error("blocked overlay"); }, // 将映射为 ACTION_INTERCEPTED
          first: () => clickLoc,
        } as any;
        const hoverLoc = {
          waitFor: async () => {},
          hover: async () => { throw new Error("hover failed"); }, // 将触发 skipped=true 路径
          first: () => hoverLoc,
        } as any;
        const inputLoc = {
          waitFor: async () => {},
          click: async () => {},
          fill: async (_: string) => {},
          first: () => inputLoc,
        } as any;
        const clearLoc = {
          first: () => clearLoc,
        } as any;
        const id = (target?.id || target?.selector || target?.text || target?.role || "").toString();
        if (/click/.test(id)) return { first: () => clickLoc } as any;
        if (/hover/.test(id)) return { first: () => hoverLoc } as any;
        if (/input/.test(id)) return { first: () => inputLoc } as any;
        if (/clear/.test(id)) return { first: () => clearLoc } as any;
        return { first: () => inputLoc } as any;
      },
    }));
    vi.doMock(M_ACTIONS, () => ({
      moveMouseTo: async () => {},
      clickHuman: async () => {},
      hoverHuman: async () => {},
      scrollHuman: async () => {},
      typeHuman: async () => {},
      clearInputHuman: async () => {},
    }));
    vi.doMock(M_OPTIONS, () => ({ buildMouseMoveOptions: () => ({}), buildScrollOptions: () => ({}) }));
    vi.doMock(M_TRACE, () => ({ logHumanTrace: async () => {} }));

    const { registerPageToolsWithPrefix } = await import(M_PAGE);
    const { server, get } = stubServer();
    const container = fakeContainer();
    const manager = { getContext: async () => ({}) } as any;
    registerPageToolsWithPrefix(server as any, container as any, manager as any, "page");

    const hover = get("page_hover");
    const click = get("page_click");
    const type = get("page_type");
    const clear = get("page_input_clear");

    // hover 失败 → skipped
    let r = await hover({ dirId: "d1", target: { id: "hover-target" }, human: false });
    let payload = JSON.parse(r.content[0].text);
    expect(payload.ok).toBe(true);
    expect(payload.value?.skipped).toBe(true);

    // click 被遮挡 → ACTION_INTERCEPTED
    r = await click({ dirId: "d1", target: { id: "click-target" }, human: false });
    payload = JSON.parse(r.content[0].text);
    expect(payload.ok).toBe(false);
    expect(payload.error?.code).toBe("ACTION_INTERCEPTED");

    // type 非人类化（fill 路径）
    r = await type({ dirId: "d1", target: { id: "input-target" }, text: "abc", human: false });
    expect(JSON.parse(r.content[0].text).ok).toBe(true);

    // clear 人类化（clearInputHuman 分支）
    r = await clear({ dirId: "d1", target: { id: "clear-target" }, human: true });
    expect(JSON.parse(r.content[0].text).ok).toBe(true);
  });
});

