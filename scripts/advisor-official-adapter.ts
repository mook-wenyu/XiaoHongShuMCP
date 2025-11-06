/**
 * 官方桥接包安装顾问（提示与指导，首选策略）
 *
 * 用法：npm run advisor:official
 * 行为：
 * - 检测是否已安装官方 Playwright MCP 桥接包
 * - 如缺失：根据包管理器打印一键安装命令与注意事项
 * - 退出码：已安装=0；缺失=1
 */

const CANDIDATES = [
  "@roxybrowser/playwright-mcp",
  "@roxybrowserlabs/playwright-mcp",
  "roxybrowser-playwright-mcp",
  "roxybrowser-mcp-playwright",
];

function stdout(s: string){ process.stdout.write(s.endsWith("\n") ? s : s + "\n"); }
function stderr(s: string){ process.stderr.write(s.endsWith("\n") ? s : s + "\n"); }

async function hasOfficial(): Promise<{ name?: string; version?: string } | undefined> {
  for (const name of CANDIDATES) {
    try {
      const m = await import(name).catch(() => undefined) as any;
      if (m && (m.getContext || m.openContext)) {
        let version: string | undefined;
        try {
          const { createRequire } = await import("node:module");
          const require = createRequire(import.meta.url);
          const pkg = require(name + "/package.json");
          version = typeof pkg?.version === "string" ? pkg.version : undefined;
        } catch {}
        return { name, version };
      }
    } catch {}
  }
  return undefined;
}

function detectPM(): "pnpm" | "yarn" | "bun" | "npm" {
  const ua = process.env.npm_config_user_agent || "";
  if (/pnpm/i.test(ua)) return "pnpm";
  if (/yarn/i.test(ua)) return "yarn";
  if (/bun/i.test(ua)) return "bun";
  try {
    const { existsSync } = require("node:fs");
    if (existsSync("pnpm-lock.yaml")) return "pnpm";
    if (existsSync("yarn.lock")) return "yarn";
    if (existsSync("bun.lockb")) return "bun";
  } catch {}
  return "npm";
}

(async () => {
  const found = await hasOfficial();
  if (found) {
    stdout(`✅ 已检测到官方桥：${found.name}${found.version ? "@"+found.version : ""}`);
    stdout("无需安装。");
    process.exit(0);
  }

  const pm = detectPM();
  const pkg = "@roxybrowser/playwright-mcp"; // 仅白名单展示

  stdout("⚠️ 未检测到 roxybrowser 官方 Playwright MCP 桥接包。");
  stdout("请按您的包管理器执行以下命令（二选一）：");
  switch (pm) {
    case "pnpm": stdout(`  pnpm add ${pkg}`); break;
    case "yarn": stdout(`  yarn add ${pkg}`); break;
    case "bun":  stdout(`  bun add ${pkg}`); break;
    default:      stdout(`  npm i -S ${pkg}`); break;
  }
  stdout("");
  stdout("安装完成后，可执行：");
  stdout("  npm run check:tools    # 列出 MCP 工具，确认 bridge 生效");
  stdout("  npm run mcp            # 启动 MCP（stdio），上游客户端可连接");
  stdout("");
  stdout("注意事项：");
  stdout("- 安装阶段可能触发依赖包的 install/postinstall 脚本，属于供应链风险面，请在受控环境操作。");
  stdout("- 若 Playwright 未就绪，可执行：npx playwright install chromium。");
  stdout("- 若仍需先启动 MCP，请将 OFFICIAL_ADAPTER_REQUIRED=false（不推荐生产启用）。");
  stdout("");
  stdout("环境变量提醒：必须正确设置 ROXY_API_TOKEN、ROXY_API_BASEURL 或 HOST/PORT。");
  process.exit(1);
})().catch((e) => { stderr(String(e?.message || e)); process.exit(1); });

