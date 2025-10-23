/* 中文注释：MCP 工具层的截图辅助（受环境变量控制） */
import { ensureDir } from "../../services/artifacts.js"
import { join } from "node:path"

/**
 * 失败时保存页面截图（fullPage），用于排障取证。
 * 由环境变量 `ACTION_SNAP_ON_ERROR` 控制，默认开启。
 */
export async function screenshotIfEnabled(page: any, dirId: string, tag: string) {
  try {
    const flag = String(process.env.ACTION_SNAP_ON_ERROR ?? "true").toLowerCase()
    if (flag === "false" || flag === "0") return undefined
    const outRoot = "artifacts/" + dirId + "/actions"
    await ensureDir(outRoot)
    const path = join(outRoot, `${tag}-${Date.now()}.png`)
    await page.screenshot({ path, fullPage: true })
    return path
  } catch {
    return undefined
  }
}
