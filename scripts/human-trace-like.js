// 实机：拟人化轨迹验证（主点赞按钮）。生成路径 JSON + 叠加渲染截图
// 用法：node scripts/human-trace-like.js --dirId=<dirId> [--click]

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
  const { dirId, click, steps, randomness, overshoot, micro, microCount } = parseArgs();
  if (!dirId) { console.error("dirId is required"); process.exit(2); }

  const { ConfigProvider } = await import("../dist/config/ConfigProvider.js");
  const { ServiceContainer } = await import("../dist/core/container.js");
  const Pages = await import("../dist/services/pages.js");
  const { planMousePath } = await import("../dist/humanization/plans/mousePlan.js");
  const { ensureDir } = await import("../dist/services/artifacts.js");

  const cfg = ConfigProvider.load().getConfig();
  const container = new ServiceContainer(cfg);
  const manager = container.createConnectionManager();

  const outRoot = `artifacts/${dirId}/human-trace-like/${Date.now()}`;
  await ensureDir(outRoot);

  let ok = false;
  try {
    const { context } = await manager.getHealthy(dirId, { workspaceId: process.env.ROXY_DEFAULT_WORKSPACE_ID });
    const page = await Pages.ensurePage(context, {});

    // 若不存在模态，尽量快速打开一条（超时较短，避免拖长）
    const shellProbe = page.locator('.note-detail-mask:visible, [role="dialog"]:visible, [aria-modal="true"]:visible, #noteContainer:visible, .note-container:visible').first();
    try {
      if (!await shellProbe.count()) {
        const link = page.locator('a:has([elementtiming="note-cover"])').first();
        if (await link.isVisible({ timeout: 3000 }).catch(()=>false)) {
          await link.click({ timeout: 5000 });
          await shellProbe.waitFor({ state: 'visible', timeout: 8000 });
        }
      }
    } catch {}

    const shell = shellProbe;
    const bar = shell.locator('.interactions.engage-bar, .engage-bar, .engage-bar-container, .buttons.engage-bar-style').first();
    await bar.waitFor({ state: 'visible', timeout: 5000 }).catch(()=>{});
    const like = bar.locator('.left .like-wrapper:visible:has(svg[width="24"])').first();
    const box = await like.boundingBox();
    if (!box) throw new Error('like-button-not-found');

    // 生成拟人化路径（与 moveMouseCubic 同源）：默认 steps=30, randomness=0.2, overshoot=true, amount=10, microJitterPx=0.6, count=4
    const to = { x: box.x + box.width / 2, y: box.y + box.height / 2 };
    const from = { x: Math.max(0, to.x - 60 + Math.random() * 20), y: Math.max(0, to.y - 40 + Math.random() * 20) };
    const path = planMousePath(from, to, {
      steps: steps ? Number(steps) : undefined,
      randomness: randomness ? Number(randomness) : undefined,
      overshoot: typeof overshoot === 'string' ? overshoot !== 'false' : undefined,
      microJitterPx: micro ? Number(micro) : undefined,
      microJitterCount: microCount ? Number(microCount) : undefined,
    });

    // 执行移动并记录
    for (const p of path) {
      await page.mouse.move(p.x, p.y, { steps: 1 });
      await page.waitForTimeout(8 + Math.floor(Math.random() * 8));
    }
    if (click) {
      await like.click({ delay: 30 + Math.floor(Math.random() * 80) });
    }

    // 在页面叠加渲染路径（红线+终点圈），便于截图核对
    await page.evaluate((pts) => {
      const id = 'human-trace-overlay';
      let cnv = document.getElementById(id);
      const w = window.innerWidth, h = window.innerHeight;
      if (!cnv) {
        cnv = document.createElement('canvas');
        cnv.id = id; cnv.width = w; cnv.height = h;
        cnv.style.cssText = 'position:fixed;left:0;top:0;width:100%;height:100%;pointer-events:none;z-index:999999;';
        document.body.appendChild(cnv);
      }
      const ctx = (cnv instanceof HTMLCanvasElement ? cnv.getContext('2d') : null);
      if (!ctx) return;
      ctx.clearRect(0,0,cnv.width,cnv.height);
      ctx.lineWidth = 2; ctx.strokeStyle = 'rgba(255,0,0,0.85)'; ctx.fillStyle = 'rgba(255,0,0,0.85)';
      ctx.beginPath();
      for (let i=0;i<pts.length;i++) {
        const p = pts[i]; if (i===0) ctx.moveTo(p.x, p.y); else ctx.lineTo(p.x, p.y);
      }
      ctx.stroke();
      const last = pts[pts.length-1];
      ctx.beginPath(); ctx.arc(last.x, last.y, 6, 0, Math.PI*2); ctx.stroke();
    }, path);

    const { writeFile } = await import('node:fs/promises');
    await writeFile(`${outRoot}/path.json`, JSON.stringify({ from, to, points: path }, null, 2), 'utf-8');
    await page.screenshot({ path: `${outRoot}/trace.png`, fullPage: true });

    ok = true;
    console.log(JSON.stringify({ ok: true, outDir: outRoot, points: path.length }));
  } catch (e) {
    console.error(JSON.stringify({ ok: false, error: String(e?.message || e) }));
  } finally {
    try { await container.cleanup(); } catch {}
  }
  process.exit(ok ? 0 : 1);
})();
