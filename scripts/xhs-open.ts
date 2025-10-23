/* 中文：通过 Roxy+Playwright 打开小红书主页（非 MCP 客户端，直连 CDP） */
import { ConfigProvider } from "../src/config/ConfigProvider.js";
import { ServiceContainer } from "../src/core/container.js";
import { parseDirIds, parseArg } from "../src/utils/cliParser.js";
import * as Pages from "../src/services/pages.js";

(async () => {
  const provider = ConfigProvider.load();
  const cfg = provider.getConfig();
  const container = new ServiceContainer(cfg);
  const logger = container.createLogger({ module: "xhs-open" });

  const url = parseArg("url", process.argv, "https://www.xiaohongshu.com/explore")!;
  const workspaceId = parseArg("workspaceId", process.argv);
  const dirIds = parseDirIds(process.argv);
  const dirId = dirIds[0] || "user";

  try {
    const connectionManager = container.createConnectionManager();
    const { context } = await connectionManager.get(dirId, { workspaceId });
    const page = await Pages.ensurePage(context, {});
    await page.goto(url, { waitUntil: "domcontentloaded" });
    logger.info({ dirId, url: page.url() }, "已打开小红书页面");
    // 进程立刻退出，Roxy 窗口会继续保留（连接已建立，可复用）
  } catch (e) {
    console.error("xhs-open 失败:", e);
    process.exit(1);
  }
})();
