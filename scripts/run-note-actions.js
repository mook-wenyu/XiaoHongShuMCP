// Plain JS runner using built dist modules (avoids tsx overhead)
// Usage: node scripts/run-note-actions.js --dirId=<dirId> --keywords=独立,游戏 --comment='[微笑R]'

function parseArgs() {
  const args = {}
  for (let i = 2; i < process.argv.length; i++) {
    const seg = process.argv[i]
    const eq = seg.indexOf('=')
    if (eq === -1) continue
    const k = seg.slice(0, eq).replace(/^--/, '')
    const v = seg.slice(eq + 1)
    args[k] = v
  }
  return args
}

async function main() {
  const { dirId, keywords, comment } = parseArgs()
  if (!dirId) throw new Error('dirId is required')
  const kwList = (keywords ? String(keywords).split(',').map(s=>s.trim()).filter(Boolean) : ['独立','游戏'])

  const { ConfigProvider } = await import('../dist/config/ConfigProvider.js')
  const { ServiceContainer } = await import('../dist/core/container.js')
  const { ensurePage } = await import('../dist/services/pages.js')
  const { ensureDiscoverPage, closeModalIfOpen, findAndOpenNoteByKeywords } = await import('../dist/domain/xhs/navigation.js')
  const { likeCurrent, collectCurrent, commentCurrent, followAuthor } = await import('../dist/domain/xhs/noteActions.js')
  const { ensureDir } = await import('../dist/services/artifacts.js')
  const fs = await import('node:fs/promises')

  const config = ConfigProvider.load().getConfig()
  const container = new ServiceContainer(config)
  try {
    const manager = container.createConnectionManager()
    const { context } = await manager.getHealthy(dirId, { workspaceId: process.env.ROXY_DEFAULT_WORKSPACE_ID })
    const page = await ensurePage(context, {})

    // 1) 进入发现页，确保无模态
    await closeModalIfOpen(page)
    await ensureDiscoverPage(page)

    // 2) 打开匹配的笔记
    const r = await findAndOpenNoteByKeywords(page, kwList, { maxScrolls: Number(process.env.XHS_SELECT_MAX_SCROLLS || 12), settleMs: 200, useApiAfterScroll: true, preferApiAnchors: true })
    if (!r.ok) throw new Error('NOTE_NOT_FOUND')

    // 3) 模态内动作
    const like = await likeCurrent(page)
    const collect = await collectCurrent(page)
    const commentRes = comment ? await commentCurrent(page, String(comment)) : { ok: true }
    const follow = await followAuthor(page)

    // 4) 产物
    const outDir = `artifacts/${dirId}/note-actions-repro/${Date.now()}`
    await ensureDir(outDir)
    const json = { ok: true, matched: r.matched, like, collect, comment: commentRes, follow, url: page.url() }
    await fs.writeFile(`${outDir}/result.json`, JSON.stringify(json, null, 2), 'utf-8')
    await page.screenshot({ path: `${outDir}/final.png`, fullPage: true })
    process.stdout.write(JSON.stringify(json) + '\n')
  } finally {
    await container.cleanup()
  }
}

main().catch(err => { console.error(err?.stack || err); process.exit(1) })
