/* 中文：尝试通过 /browser/open 直接打开窗口并打印 ws */
import { ConfigProvider } from "../src/config/ConfigProvider.js";
import { ServiceContainer } from "../src/core/container.js";

function getArg(name: string) {
  const key = `--${name}=`;
  const arg = process.argv.find((a) => a.startsWith(key));
  return arg ? arg.slice(key.length) : undefined;
}

(async () => {
  const provider = ConfigProvider.load();
  const cfg = provider.getConfig();
  const container = new ServiceContainer(cfg);
  const roxy = container.createRoxyClient();
  const workspaceId = getArg("workspaceId") || process.env.ROXY_DEFAULT_WORKSPACE_ID;
  const dirId = getArg("dirId") || `codex_e2e_${Date.now()}`;
  if (!workspaceId) {
    console.error(JSON.stringify({ ok: false, error: "workspaceId is required" }));
    process.exit(1);
  }
  try {
    const info = await roxy.open(String(dirId), undefined, String(workspaceId));
    console.error(JSON.stringify({ ok: true, dirId, info }, null, 2));
  } catch (e: any) {
    console.error(JSON.stringify({ ok: false, dirId, error: String(e?.message || e) }));
  }
  await container.cleanup();
})();
