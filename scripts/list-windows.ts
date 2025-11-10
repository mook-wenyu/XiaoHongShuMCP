/* 中文：列出指定 workspaceId 的窗口列表 */
import { ConfigProvider } from "../src/config/ConfigProvider.js";
import { ServiceContainer } from "../src/core/container.js";

function getArg(name: string) {
  const key = `--${name}=`;
  const arg = process.argv.find((a) => a.startsWith(key));
  return arg ? arg.slice(key.length) : undefined;
}

(async () => {
  const workspaceId = getArg("workspaceId") || process.env.ROXY_DEFAULT_WORKSPACE_ID;
  if (!workspaceId) {
    console.error(JSON.stringify({ ok: false, error: "workspaceId is required" }));
    process.exit(1);
  }
  const provider = ConfigProvider.load();
  const cfg = provider.getConfig();
  const container = new ServiceContainer(cfg);
  const roxy = container.createRoxyClient();
  const list = await roxy.listWindows({ workspaceId: Number(workspaceId) as any });
  console.error(JSON.stringify(list, null, 2));
  await container.cleanup();
})();

