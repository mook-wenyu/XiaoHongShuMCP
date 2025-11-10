/* 中文注释：采集小红书页面的高保真快照（HTML），用于稳定的离线集成测试。
 * - 通过 RoxyBrowser + Playwright 连接真实页面
 * - 抓取 explore 与 search_result?keyword=... 的 HTML
 * - 进行最小净化（移除 <script> 等不确定性内容），写入 tests/fixtures/xhs/live/
 * - 可选参数：--dirId=<id>  --keyword=<kw>
 */
import { ConfigProvider } from "../src/config/ConfigProvider.js";
import { ServiceContainer } from "../src/core/container.js";
import type { RoxyBrowserManager } from "../src/services/roxyBrowser.js";
import { ensureDir, pathJoin } from "../src/services/artifacts.js";
import { resolve as pathResolve } from "node:path";
import { writeFile } from "node:fs/promises";

function argOf(name: string, fallback?: string) {
  const p = process.argv.find((a) => a.startsWith(`--${name}=`));
  if (!p) return fallback;
  return p.split("=").slice(1).join("=");
}

function pickEnvDirId(): string | undefined {
  const ids = (process.env.ROXY_DIR_IDS || "")
    .split(",")
    .map((s) => s.trim())
    .filter(Boolean);
  return ids[0];
}

function sanitizeHtml(html: string): string {
  // 最小净化：去除 script/style 中潜在不稳定脚本（保留基础样式可选）
  let out = html;
  try {
    out = out.replace(/<script[\s\S]*?<\/script>/gi, "");
    // 常见预加载/跟踪
    out = out.replace(/<link[^>]+rel=[\"']preload[\"'][^>]*>/gi, "");
  } catch {}
  return out;
}

async function capturePage(manager: RoxyBrowserManager, dirId: string, url: string) {
  const ctx = await manager.getContext(dirId, {
    workspaceId: process.env.ROXY_DEFAULT_WORKSPACE_ID,
  });
  const page = await (await import("../src/services/pages.js")).ensurePage(ctx, {});
  await page.goto(url, { waitUntil: "domcontentloaded" });
  // 给首屏一个缓冲时间，尽可能完成初始注水
  await page.waitForTimeout(Math.max(300, Number(process.env.XHS_OPEN_WAIT_MS || 1500)));
  const html = await page.content();
  return sanitizeHtml(html);
}

async function main() {
  const kw = argOf("keyword", "美食");
  const cliDirId = argOf("dirId");
  const dirId = cliDirId || pickEnvDirId();
  if (!dirId) {
    console.error("未提供 dirId，且 ROXY_DIR_IDS 为空。请通过 --dirId 或设置 ROXY_DIR_IDS。");
    process.exit(1);
    return;
  }

  const provider = ConfigProvider.load();
  const container = new ServiceContainer(provider.getConfig());
  const manager = (await import("../src/services/roxyBrowser.js")).RoxyBrowserManager
    ? new (await import("../src/services/roxyBrowser.js")).RoxyBrowserManager(container)
    : (null as any as RoxyBrowserManager);
  if (!manager) {
    console.error("无法创建 RoxyBrowserManager");
    process.exit(1);
    return;
  }

  const outDir = pathResolve(process.cwd(), "tests/fixtures/xhs/live");
  await ensureDir(outDir);

  // 采集探索页
  const exploreHtml = await capturePage(manager, dirId, "https://www.xiaohongshu.com/explore");
  const exploreOut = pathJoin("tests", "fixtures", "xhs", "live", "explore.live.html");
  await writeFile(pathResolve(process.cwd(), exploreOut), exploreHtml, "utf-8");

  // 采集搜索结果页
  const searchUrl = `https://www.xiaohongshu.com/search_result?keyword=${encodeURIComponent(
    kw || ""
  )}`;
  const srHtml = await capturePage(manager, dirId, searchUrl);
  const srOut = pathJoin("tests", "fixtures", "xhs", "live", "search_result.live.html");
  await writeFile(pathResolve(process.cwd(), srOut), srHtml, "utf-8");

  console.error(
    JSON.stringify(
      {
        ok: true,
        out: { explore: exploreOut, search_result: srOut },
        dirId,
      },
      null,
      2,
    ),
  );
  await container.cleanup().catch(() => {});
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});

