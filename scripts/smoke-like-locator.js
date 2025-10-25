// 实机烟测：连接 Roxy 上下文→快速定位笔记模态的主点赞按钮（engage-bar 左侧 24px）
// 用法：node scripts/smoke-like-locator.js --dirId=<dirId> [--click]

function parseArgs() {
  const args = {};
  for (let i = 2; i < process.argv.length; i++) {
    const seg = process.argv[i];
    const eq = seg.indexOf("=");
    if (eq === -1) { args[seg.replace(/^--/,"")] = true; continue; }
    const k = seg.slice(0, eq).replace(/^--/, "");
    const v = seg.slice(eq + 1);
    args[k] = v;
  }
  return args;
}

(async () => {
  const { dirId, click } = parseArgs();
  if (!dirId) { console.error("dirId is required"); process.exit(2); }

  const { ConfigProvider } = await import("../dist/config/ConfigProvider.js");
  const { ServiceContainer } = await import("../dist/core/container.js");
  const Pages = await import("../dist/services/pages.js");

  const cfg = ConfigProvider.load().getConfig();
  const container = new ServiceContainer(cfg);
  const manager = container.createConnectionManager();

  let result = { ok: false };
  try {
    const t0 = Date.now();
    const { context } = await manager.getHealthy(dirId, { workspaceId: process.env.ROXY_DEFAULT_WORKSPACE_ID });
    const page = await Pages.ensurePage(context, {});
    console.log(JSON.stringify({ step: "connected", url: page.url() }));

    // 快速截图当前状态以便审计
    try {
      const { ensureDir } = await import("../dist/services/artifacts.js");
      const outRoot = `artifacts/${dirId}/smoke-like-locator`;
      await ensureDir(outRoot);
      const snap = `${outRoot}/state-${Date.now()}.png`;
      await page.screenshot({ path: snap, fullPage: true });
      console.log(JSON.stringify({ step: "snap", path: snap }));
    } catch {}

    // 若未在模态内，尝试轻量进入探索页（20s 上限，无滚动）
    const startUrl = page.url();
    if (!/(note|explore|search)/i.test(startUrl)) {
      try { await page.goto("https://www.xiaohongshu.com/explore", { waitUntil: "domcontentloaded", timeout: 20000 }); } catch {}
      console.log(JSON.stringify({ step: "nav", url: page.url() }));
    }

    // 若不在模态内，快速尝试打开首个可见卡片进入详情模态（总预算 < 15s）
    let opened = false;
    try {
      const modalProbe = page.locator('.note-detail-mask:visible, [role="dialog"]:visible, [aria-modal="true"]:visible, #noteContainer:visible, .note-container:visible').first();
      if (!await modalProbe.count()) {
        const candidate = page.locator('a[href*="/explore"], a[href*="/discovery"], a:has([elementtiming="note-cover"])').first();
        if (await candidate.isVisible({ timeout: 3000 }).catch(() => false)) {
          await candidate.click({ timeout: 5000 });
          await modalProbe.waitFor({ state: 'visible', timeout: 8000 });
          opened = true;
          console.log(JSON.stringify({ step: 'opened-modal' }));
        }
      } else {
        opened = true;
      }
    } catch {}

    // 选出模态/容器（存在则优先），否则直接在页面根尝试 engage-bar
    const shell = page.locator('.note-detail-mask:visible, [role="dialog"]:visible, [aria-modal="true"]:visible, #noteContainer:visible, .note-container:visible').first();
    let bar = shell.locator('.interactions.engage-bar, .engage-bar, .engage-bar-container, .buttons.engage-bar-style').first();
    try {
      const hasShell = await shell.count();
      if (!hasShell) { bar = page.locator('.interactions.engage-bar, .engage-bar, .engage-bar-container, .buttons.engage-bar-style').first(); }
      await bar.waitFor({ state: 'visible', timeout: 5000 });
    } catch {}

    const like = bar.locator('.left .like-wrapper:visible:has(svg[width="24"])').first();
    const visible = await like.isVisible().catch(() => false);
    let clickable = false;
    if (visible) {
      // 简单可点性：有几何且中心命中自身（轻量版）
      const box = await like.boundingBox().catch(() => null);
      if (box && box.width > 2 && box.height > 2) {
        const cx = Math.floor(box.x + Math.max(1, box.width / 2));
        const cy = Math.floor(box.y + Math.max(1, box.height / 2));
        clickable = await page.evaluate(([x,y,sel]) => {
          const el = document.elementFromPoint(x, y);
          let n = el; let depth = 0;
          const target = document.querySelector(sel);
          while (n && depth < 8) { if (n === target) return true; n = n.parentElement; depth++; }
          return false;
        }, [cx, cy, '.left .like-wrapper:has(svg[width="24"])']);
      }
    }
    console.log(JSON.stringify({ step: "probe-like", visible, clickable }));

    if (visible && clickable && click) {
      await like.click({ delay: 30 });
      console.log(JSON.stringify({ step: "clicked" }));
    }

    result = { ok: visible && clickable, tookMs: Date.now() - t0, url: page.url() };
    console.log(JSON.stringify(result));
  } catch (e) {
    console.error(JSON.stringify({ ok: false, error: String(e?.message || e) }));
    result = { ok: false };
  } finally {
    try { await container.cleanup(); } catch {}
  }

  process.exit(result.ok ? 0 : 1);
})();
