/* 中文：最小复现脚本（Roxy/CDP 实机）
 * - 输入：--dirId=... [--workspaceId=...] [--keywords="..." or @file] [--url=...]
 * - 行为：
 *   1) 连接 Roxy → 复用/打开窗口
 *   2) 打开 URL（默认 Explore）
 *   3) 按 keywords:string[] 任意一个命中策略执行 findAndOpenNoteByKeywords（含重叠滚动/回退/自适应）
 *   4) 产物与日志：选择结果写入 artifacts/<dirId>/keywords-repro/<runId>/result.json
 */
import { ConfigProvider } from "../src/config/ConfigProvider.js";
import { ServiceContainer } from "../src/core/container.js";
import { ConnectionManager } from "../src/services/connectionManager.js";
import * as Pages from "../src/services/pages.js";
import { ensureDiscoverPage, findAndOpenNoteByKeywords, closeModalIfOpen } from "../src/domain/xhs/navigation.js";
import { ensureDir } from "../src/services/artifacts.js";
import { writeFile, readFile } from 'node:fs/promises';
import { join } from 'node:path';

function getArg(name: string, def?: string) {
  const ent = process.argv.find(a => a.startsWith(`--${name}=`));
  return ent ? ent.split('=')[1] : def;
}
async function parseKeywords(): Promise<string[]> {
  const fromArg = getArg('keywords');
  if (!fromArg) return [];
  try {
    if (fromArg.startsWith('@')) {
      const p = fromArg.slice(1);
      const txt = await readFile(p, 'utf-8');
      try {
        const arr = JSON.parse(txt);
        if (Array.isArray(arr)) return arr.map(String);
      } catch {}
      return [String(txt).trim()];
    }
    if (fromArg.includes(',')) return fromArg.split(',').map((s) => s.trim()).filter(Boolean);
    return [fromArg];
  } catch { return [fromArg]; }
}

(async () => {
  const dirId = getArg('dirId');
  const workspaceId = getArg('workspaceId') ?? process.env.ROXY_DEFAULT_WORKSPACE_ID;
  const url = getArg('url', 'https://www.xiaohongshu.com/explore');
  const keywords = await parseKeywords();
  if (!dirId) { throw new Error('missing --dirId'); }

  // 调试开关（滚动日志与指标）
  process.env.XHS_SCROLL_DEBUG = process.env.XHS_SCROLL_DEBUG ?? 'true';
  process.env.XHS_SCROLL_METRICS = process.env.XHS_SCROLL_METRICS ?? 'true';

  const conf = ConfigProvider.load().getConfig();
  const container = new ServiceContainer(conf, { loggerSilent: true });
  const cm: ConnectionManager = container.createConnectionManager() as any;
  const { context } = await cm.getHealthy(dirId, { workspaceId });
  const page = await Pages.ensurePage(context, {});

  await page.goto(url, { waitUntil: 'domcontentloaded' });
  await closeModalIfOpen(page as any);
  await ensureDiscoverPage(page as any);

  const runId = String(Date.now());
  const defaultKw = ['独立', '游戏'];
  const res = await findAndOpenNoteByKeywords(page as any, (keywords.length ? keywords : defaultKw), {
    maxScrolls: Number(process.env.XHS_SELECT_MAX_SCROLLS || 18),
    settleMs: 220,
    useApiAfterScroll: true,
    preferApiAnchors: true,
  });

  const root = join('artifacts', dirId, 'keywords-repro', runId);
  await ensureDir(root);
  await writeFile(join(root, 'result.json'), JSON.stringify({ ok: res.ok, ...res, url: page.url(), keywords }, null, 2), 'utf-8');
  try { await page.screenshot({ path: join(root, 'final.png'), fullPage: true }); } catch {}

  process.stderr.write(`[keywords-repro] done ok=${res.ok} url=${page.url()} out=${root}\n`);
  await container.cleanup();
  process.exit(0);
})().catch((e) => {
  process.stderr.write(`[keywords-repro] FAILED ${String(e?.message || e)}\n`);
  process.exit(1);
});
