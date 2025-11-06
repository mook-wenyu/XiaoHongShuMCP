/* 中文注释：本地最小演示脚本
 * 作用：不经 MCP，直接通过容器与 Runner 跑一个最小任务并产出 artifacts
 * 用法：
 *   npm run demo:local -- --dir-ids=dirA --url=https://example.com --task=openAndScreenshot
 */
import { ConfigProvider } from "../src/config/ConfigProvider.js";
import { ServiceContainer } from "../src/core/container.js";
import { parseArg, parseDirIds } from "../src/utils/cliParser.js";
import { runDirIds } from "../src/runner/multiAccountRunner.js";
import { runTaskByName } from "../src/tasks/registry.js";
import { OfficialAdapter } from "../src/adapter/OfficialAdapter.js";

(async () => {
  const provider = ConfigProvider.load();
  const cfg = provider.getConfig();
  const container = new ServiceContainer(cfg);
  const logger = container.createLogger({ module: "demo" });

  const url = parseArg("url", process.argv, cfg.DEFAULT_URL)!;
  const taskName = parseArg("task", process.argv, "openAndScreenshot")!;
  const dirIds = parseDirIds(process.argv);
  if (dirIds.length === 0) throw new Error("请通过 --dir-ids 或 --dirId 提供至少一个 dirId");

  const payload: any = { url };
  const task = runTaskByName(taskName);
  const adapter = new OfficialAdapter(container);
  const policy = container.createPolicyEnforcer();

  const res = await runDirIds(
    dirIds,
    { getContext: (id) => adapter.getContext(id) } as any,
    (ctx, id) => task(ctx, id, payload),
    { concurrency: cfg.MAX_CONCURRENCY, timeoutMs: cfg.TIMEOUT_MS, policy, taskName }
  );

  logger.info(res, "demo-local 完成");
  await container.cleanup();
})();
