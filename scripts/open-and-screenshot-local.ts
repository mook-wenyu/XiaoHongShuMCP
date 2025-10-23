/* 中文：本地直连 Playwright 烟测（无头）
 * - 不经 MCP 与 Roxy，适合在 Roxy 不可用时做真实环境快速验证
 * - 进入 URL，等待 domcontentloaded，再等待 1s 稳定，截图到 artifacts/local/
 * 用法：npm run smoke:local -- --url=https://example.com
 */
import { chromium } from "playwright";
import { mkdirSync, writeFileSync } from "node:fs";
import { join } from "node:path";

function getArg(name: string, def?: string) {
  const key = `--${name}=`;
  const found = process.argv.find(a => a.startsWith(key));
  return found ? found.slice(key.length) : def;
}

(async () => {
  const url = getArg("url", "https://example.com")!;
  const outDir = join("artifacts", "local");
  mkdirSync(outDir, { recursive: true });

  const browser = await chromium.launch({ headless: true });
  try {
    const context = await browser.newContext({ viewport: { width: 1366, height: 900 } });
    const page = await context.newPage();
    const startedAt = Date.now();
    await page.goto(url, { waitUntil: "domcontentloaded", timeout: 45000 });
    await page.waitForTimeout(1000);
    const file = join(outDir, `shot-${startedAt}.png`);
    await page.screenshot({ path: file, fullPage: true });
    const meta = { ok: true, url: page.url(), file, startedAt, finishedAt: Date.now() };
    writeFileSync(join(outDir, `shot-${startedAt}.json`), JSON.stringify(meta, null, 2));
    process.stderr.write(JSON.stringify(meta) + "\n");
  } finally {
    await browser.close();
  }
})();
