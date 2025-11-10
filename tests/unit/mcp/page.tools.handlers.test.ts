/* 中文注释：page 工具（handlers 覆盖）——通过 vi.mock 模拟底层依赖，覆盖 click/hover/scroll/type/clear */
import { describe, it, expect, vi, beforeAll, beforeEach } from "vitest";
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

function createFakeContainer() {
  return { getHumanizationProfileKey: () => "rapid" } as any;
}

function makeFakePage() {
  const actions: any[] = [];
  const locImpl = {
    click: vi.fn(async () => { actions.push("click"); }),
    hover: vi.fn(async () => { actions.push("hover"); }),
    fill: vi.fn(async (_: string) => { actions.push("fill"); }),
    type: vi.fn(async (_: string, __?: any) => { actions.push("type"); }),
    waitFor: vi.fn(async (_?: any) => { actions.push("waitFor"); }),
    first: () => locImpl,
  } as any;
  const fake = {
    url: () => "about:blank",
    mouse: { wheel: vi.fn(async (_x: number, _y: number) => actions.push("wheel")) },
    locator: vi.fn(() => ({ first: () => locImpl })),
  } as any;
  return { fake, actions, locImpl };
}

beforeEach(() => {
  vi.clearAllMocks();
});

describe("page 工具（handlers 覆盖）", () => {
  it("click/hover/scroll/type/clear 跑通", async () => {
    vi.resetModules();
    const { fake, actions } = makeFakePage();

    const root = process.cwd();
    const M_PAGES = pathResolve(root, "src/services/pages.js");
    const M_SELECTORS = pathResolve(root, "src/selectors/index.js");
    const M_ACTIONS = pathResolve(root, "src/humanization/actions.js");
    const M_OPTIONS = pathResolve(root, "src/humanization/options.js");
    const M_TRACE = pathResolve(root, "src/humanization/trace.js");
    const M_PAGE_TOOLS = pathResolve(root, "src/mcp/tools/page.js");

    vi.doMock(M_PAGES, () => ({ ensurePage: async () => fake }));
    vi.doMock(M_SELECTORS, () => {
      return {
        resolveLocatorAsync: async () => {
          return { first: () => ({
            click: async () => actions.push("click"),
            hover: async () => actions.push("hover"),
            fill: async (_: string) => actions.push("fill"),
            type: async (_: string, __?: any) => actions.push("type"),
            waitFor: async (_?: any) => actions.push("waitFor"),
          }) };
        },
      };
    });
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

    const { registerPageToolsWithPrefix } = await import(M_PAGE_TOOLS);

    const { server, get } = createStubServer();
    const container = createFakeContainer();
    const manager = { getContext: async () => ({ pages: () => [fake] }) } as any;
    registerPageToolsWithPrefix(server, container as any, manager as any, "page");

    const click = get("page_click");
    const hover = get("page_hover");
    const scroll = get("page_scroll");
    const type = get("page_type");
    const clear = get("page_input_clear");

    let r = await click({ dirId: "d1", target: { selector: "#btn" }, human: true });
    expect(JSON.parse(r.content[0].text).ok).toBe(true);

    r = await hover({ dirId: "d1", target: { selector: "#btn" }, human: true });
    expect(JSON.parse(r.content[0].text).ok).toBe(true);

    r = await scroll({ dirId: "d1", human: false, deltaY: 400 });
    expect(JSON.parse(r.content[0].text).ok).toBe(true);

    r = await type({ dirId: "d1", target: { selector: "#input" }, text: "abc", human: true });
    expect(JSON.parse(r.content[0].text).ok).toBe(true);

    r = await clear({ dirId: "d1", target: { selector: "#input" }, human: false });
    expect(JSON.parse(r.content[0].text).ok).toBe(true);
  });
});

