/**
 * Preflight 自检（首选：提示与指导安装）
 * - 校验关键环境变量与 Config 解析
 * - 检测官方 Playwright MCP 桥接包是否已安装
 * - 提供包管理器对应的一键安装命令与安全提醒
 * - 检测 Playwright 模块是否可用（不强制拉起浏览器），提示安装浏览器命令
 *
 * 退出码：存在阻塞项（缺少官方桥/环境变量不全）时返回 1，否则 0
 */

const CANDIDATES = [
  "@roxybrowser/playwright-mcp",
  "@roxybrowserlabs/playwright-mcp",
  "roxybrowser-playwright-mcp",
  "roxybrowser-mcp-playwright",
];

function out(s: string){ process.stdout.write(s.endsWith("\n") ? s : s + "\n"); }
function err(s: string){ process.stderr.write(s.endsWith("\n") ? s : s + "\n"); }

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
  out("== Preflight 自检 ==");

  // 1) 环境变量与配置解析
  let envOk = true;
  if (!process.env.ROXY_API_TOKEN) { err("[缺失] ROXY_API_TOKEN"); envOk = false; }
  try {
    const { ConfigProvider } = await import("../src/config/ConfigProvider.js");
    ConfigProvider.load();
    out("[OK] 配置解析通过");
  } catch (e) {
    err(`[错误] 配置解析失败: ${String((e as any)?.message || e)}`);
    envOk = false;
  }

  // 2) 官方桥接包检测
  const found = await hasOfficial();
  if (found) {
    out(`[OK] 官方桥：${found.name}${found.version ? "@"+found.version : ""}`);
  } else {
    const pm = detectPM();
    err("[缺失] 官方 Playwright MCP 桥接包");
    out("安装命令（择一）：");
    switch (pm) {
      case "pnpm": out("  pnpm add @roxybrowser/playwright-mcp"); break;
      case "yarn": out("  yarn add @roxybrowser/playwright-mcp"); break;
      case "bun":  out("  bun add @roxybrowser/playwright-mcp"); break;
      default:      out("  npm i -S @roxybrowser/playwright-mcp"); break;
    }
    out("安全提醒：安装阶段可能触发 install/postinstall 脚本，请在受控环境操作。");
  }

  // 3) Playwright 模块检测（不强制安装浏览器）
  try {
    await import("playwright");
    out("[OK] Playwright 模块已安装（如首跑失败，执行 npx playwright install chromium）");
  } catch {
    err("[警告] 未检测到 Playwright 模块，请执行 npm i -S playwright，并安装浏览器 npx playwright install chromium");
  }

  // 4) 结果
  const ok = envOk && !!found;
  out(ok ? "== 自检通过 ==" : "== 自检存在问题，请按提示修正 ==");
  process.exit(ok ? 0 : 1);
})().catch((e) => { err(String((e as any)?.message || e)); process.exit(1); });

