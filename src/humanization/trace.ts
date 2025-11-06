/* 中文注释：人性化轨迹/事件可选落盘（NDJSON），默认关闭 */
import { promises as fs } from 'node:fs'
import { join } from 'node:path'
import { ensureDir } from '../services/artifacts.js'

export async function logHumanTrace(dirId: string, name: string, data: any){
  if (process.env.HUMAN_TRACE_LOG !== 'true') return
  try{
    const root = join('artifacts', String(dirId))
    await ensureDir(root)
    const file = join(root, 'human-trace.ndjson')
    const rec = { ts: Date.now(), name, ...sanitize(data) }
    await fs.appendFile(file, JSON.stringify(rec) + '\n')
  }catch{}
}

function sanitize(v: any){
  try{
    return JSON.parse(JSON.stringify(v ?? {}))
  }catch{ return {} }
}

