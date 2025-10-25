// 实机烟测（版本2）：使用域导航逻辑（小滚动）打开一条笔记→探测/可选点击主点赞按钮
// 用法：node scripts/smoke-like-open.js --dirId=<dirId> [--click]

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
  const Nav = await import("../dist/domain/xhs/navigation.js");

  const cfg = ConfigProvider.load().getConfig();
  const container = new ServiceContainer(cfg);
  const manager = container.createConnectionManager();

  let result = { ok: false };
  try {
    const t0 = Date.now();
    const { context } = await manager.getHealthy(dirId, { workspaceId: process.env.ROXY_DEFAULT_WORKSPACE_ID });
    const page = await Pages.ensurePage(context, {});
    console.log(JSON.stringify({ step: "connected", url: page.url() }));

    await Nav.closeModalIfOpen(page).catch(()=>{});
    await Nav.ensureDiscoverPage(page);
    console.log(JSON.stringify({ step: "discover", url: page.url() }));

    const openRes = await Nav.findAndOpenNoteByKeywords(page, ["AI","模型"], { maxScrolls: Number(process.env.XHS_SELECT_MAX_SCROLLS || 3), settleMs: 200, useApiAfterScroll: false, preferApiAnchors: false });
    console.log(JSON.stringify({ step: "opened", opened: openRes.ok, feedVerified: openRes.feedVerified }));

    const shell = page.locator('.note-detail-mask:visible, [role="dialog"]:visible, [aria-modal="true"]:visible, #noteContainer:visible, .note-container:visible').first();
    await shell.waitFor({ state: 'visible', timeout: 8000 });

    const bar = shell.locator('.interactions.engage-bar, .engage-bar, .engage-bar-container, .buttons.engage-bar-style').first();
    await bar.waitFor({ state: 'visible', timeout: 5000 }).catch(()=>{});
    const like = bar.locator('.left .like-wrapper:visible:has(svg[width="24"])').first();
    const visible = await like.isVisible().catch(() => false);

    if (visible && click) {
      await like.click({ delay: 30 });
      console.log(JSON.stringify({ step: "clicked" }));
    }

    result = { ok: visible === true, tookMs: Date.now() - t0, url: page.url() };
    console.log(JSON.stringify(result));
  } catch (e) {
    console.error(JSON.stringify({ ok: false, error: String(e?.message || e) }));
    result = { ok: false };
  } finally {
    try { await container.cleanup(); } catch {}
  }

  process.exit(result.ok ? 0 : 1);
})();
