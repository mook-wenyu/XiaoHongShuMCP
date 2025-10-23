/* 中文注释：最小动作链路烟测（不经 MCP）
 * 流程：打开页面→导航 example.com→等待 networkidle→截图到 artifacts
 * 用法：npm run smoke:actions -- --dir-ids=dirA
 */
import { ConfigProvider } from "../src/config/ConfigProvider.js";
import { ServiceContainer } from "../src/core/container.js";
import { parseDirIds } from "../src/utils/cliParser.js";
import * as Pages from "../src/services/pages.js";
import { ensureDir } from "../src/services/artifacts.js";
import { join } from "node:path";

(async () => {
  const provider = ConfigProvider.load();
  const cfg = provider.getConfig();
  const container = new ServiceContainer(cfg);
  try {
    const connector = container.createPlaywrightConnector();
    const policy = container.createPolicyEnforcer();
    const connectionManager = container.createConnectionManager();

    const dirIds = parseDirIds(process.argv);
    if (dirIds.length === 0) throw new Error("请通过 --dir-ids 或 --dirId 提供至少一个 dirId");
    const dirId = dirIds[0];

    await policy.use(dirId, async () => {
      const { context } = await connectionManager.get(dirId, {});
      const page = await Pages.ensurePage(context, {});
      await page.goto("https://example.com", { waitUntil: "domcontentloaded" });
      await page.waitForLoadState("networkidle");
      const outRoot = join("artifacts", dirId, "actions");
      await ensureDir(outRoot);
      const path = join(outRoot, `smoke-${Date.now()}.png`);
      await page.screenshot({ path, fullPage: true });
      process.stderr.write(JSON.stringify({ ok: true, path }) + "\n");
    });
  } finally {
    await container.cleanup();
  }
})();
