/* 中文注释：环境自检脚本
 * - 依据 src/config/schema.ts 做 Zod 校验
 * - 输出 JSON 摘要，便于管道使用
 * - 可选探活 Roxy /health（通过环境变量 CHECK_ROXY_HEALTH=true 开启）
 */
import { ConfigProvider } from "../src/config/ConfigProvider.js";
import { ConfigSchema } from "../src/config/schema.js";
import { readFile } from "node:fs/promises";
import { existsSync } from "node:fs";
import { join } from "node:path";

async function main() {
  const result: any = { ok: false, checks: [] as any[], env: {}, notes: [] as string[] };

  // 1) 读取 .env（如果存在，仅作为提示；正式加载由 dotenv 完成）
  const hasEnv = existsSync(".env");
  result.checks.push({ key: "env.dotenv", exists: hasEnv });

  // 2) 变量校验
  const parsed = ConfigSchema.safeParse(process.env);
  if (!parsed.success) {
    result.checks.push({ key: "env.schema", ok: false, issues: parsed.error.issues.map(i => ({ path: i.path.join("."), message: i.message })) });
  } else {
    result.checks.push({ key: "env.schema", ok: true });
  }

  // 3) 生成运行时配置，输出关键字段
  try {
    const provider = ConfigProvider.load();
    const cfg = provider.getConfig();
    result.env = {
      roxyBaseURL: cfg.roxy.baseURL,
      concurrency: cfg.MAX_CONCURRENCY,
      timeoutMs: cfg.TIMEOUT_MS,
      defaultUrl: cfg.DEFAULT_URL,
      policy: cfg.policy
    };
  } catch (e: any) {
    result.checks.push({ key: "config.load", ok: false, error: String(e?.message || e) });
    console.log(JSON.stringify(result, null, 2));
    process.exit(1);
    return;
  }

  // 4) 示例文件存在性
  const files = [
    "docs/examples/publish-payload.json",
    "docs/browserhost-design.md",
    "docs/WORKSPACE_ID_ANALYSIS.md"
  ];
  for (const f of files) {
    result.checks.push({ key: `file:${f}`, exists: existsSync(f) });
  }

  // 5) 可选探活 Roxy /health
  if (String(process.env.CHECK_ROXY_HEALTH || "").toLowerCase() === "true") {
    try {
      const { ServiceContainer } = await import("../src/core/container.js");
      const provider = ConfigProvider.load();
      const container = new ServiceContainer(provider.getConfig());
      const roxy = container.createRoxyClient();
      const msg = await roxy.health();
      result.checks.push({ key: "roxy.health", ok: true, msg });
      await container.cleanup();
    } catch (e: any) {
      result.checks.push({ key: "roxy.health", ok: false, error: String(e?.message || e) });
    }
  }

  result.ok = result.checks.every(c => c.ok !== false && c.exists !== false);
  console.log(JSON.stringify(result, null, 2));
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
