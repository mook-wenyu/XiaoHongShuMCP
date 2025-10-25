/* 中文：端到端演示脚本（小红书·笔记详情动作）
 * 用法（PowerShell）：
 *   set ROXY_API_TOKEN=...
 *   npx tsx scripts/xhs-note-actions-repro.ts --dirId=<dirId> --keywords=独立,游戏 --comment="[微笑R]"
 */
import { ServiceContainer } from '../src/core/container.js'
import { ConfigProvider } from '../src/config/ConfigProvider.js'
import { getParams } from '../src/mcp/utils/params.js'

function parseArgs() {
  const args: any = {}
  for (let i=2;i<process.argv.length;i++){
    const [k,v] = process.argv[i].split('=')
    const key = k.replace(/^--/,'')
    args[key] = v
  }
  return args
}

async function main(){
  const { dirId, keywords, comment } = getParams(parseArgs()) as { dirId: string; keywords?: string; comment?: string }
  if (!dirId) throw new Error('dirId is required')
  const kws = (keywords ? String(keywords).split(',').map(s=>s.trim()).filter(Boolean) : ['独立','游戏'])

  const config = ConfigProvider.load().getConfig()
  const container = new ServiceContainer(config)
  const roxy = container.createRoxyClient()
  const connector = container.createPlaywrightConnector()
  const manager = container.createConnectionManager()

  const { context } = await manager.getHealthy(dirId, { workspaceId: process.env.ROXY_DEFAULT_WORKSPACE_ID })
  const page = await (await import('../src/services/pages.js')).ensurePage(context, {})

  // 1) 确保进入发现页
  const { ensureDiscoverPage, closeModalIfOpen, findAndOpenNoteByKeywords } = await import('../src/domain/xhs/navigation.js')
  await closeModalIfOpen(page)
  await ensureDiscoverPage(page)

  // 2) 打开一条命中的笔记（模态）
  const r = await findAndOpenNoteByKeywords(page, kws, { maxScrolls: Number(process.env.XHS_SELECT_MAX_SCROLLS || 12), settleMs: 200, useApiAfterScroll: true, preferApiAnchors: true })
  if (!r.ok) throw new Error('NOTE_NOT_FOUND')

  // 3) 执行动作（点赞→收藏→评论→关注）
  const { likeCurrent, collectCurrent, commentCurrent, followAuthor } = await import('../src/domain/xhs/noteActions.js')
  const like = await likeCurrent(page)
  const collect = await collectCurrent(page)
  const commentRes = comment ? await commentCurrent(page, String(comment)) : { ok: true }
  const follow = await followAuthor(page)

  const fs = await import('node:fs/promises')
  const outDir = `artifacts/${dirId}/note-actions-repro/${Date.now()}`
  await (await import('../src/services/artifacts.js')).ensureDir(outDir)
  const json = { ok: true, matched: r.matched, like, collect, comment: commentRes, follow, url: page.url() }
  await fs.writeFile(`${outDir}/result.json`, JSON.stringify(json, null, 2), 'utf-8')
  await page.screenshot({ path: `${outDir}/final.png`, fullPage: true })
  process.stdout.write(JSON.stringify(json)+"\n")

  await container.cleanup()
}

main().catch(async (e)=>{ process.stderr.write(String(e?.stack || e)+'\n'); process.exit(1) })
