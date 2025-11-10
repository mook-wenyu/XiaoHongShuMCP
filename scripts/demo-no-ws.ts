/* 演示：当窗口未打开且无法获取 ws 时，MCP 工具返回 NO_WS_ENDPOINT */
import { createMcpHarness } from "../tests/helpers/mcpHarness.js";
import { getContainerAndManager } from "../tests/helpers/roxyHarness.js";

async function main() {
  const { container, manager } = await getContainerAndManager();
  const harness = createMcpHarness();
  harness.registerAll(container, manager);

  const dirId = `demo_no_ws_${Date.now()}`;
  const workspaceId = process.env.ROXY_DEFAULT_WORKSPACE_ID;

  const call = async (name: string, input: any) => {
    const t0 = Date.now();
    try {
      const h = harness.getHandler(name);
      const res = await h(input);
      const payload = JSON.parse(res.content[0].text);
      console.log("TOOL:", name, "DURATION_MS:", Date.now() - t0);
      console.log(JSON.stringify(payload, null, 2));
    } catch (e) {
      console.log("TOOL:", name, "FAILED AFTER_MS:", Date.now() - t0, String(e));
    }
  };

  await call("page_navigate", { dirId, workspaceId, url: "https://example.com" });
  await call("xhs_navigate_home", { dirId, workspaceId });
}

main().catch((e) => {
  console.error(e);
  process.exitCode = 1;
});
