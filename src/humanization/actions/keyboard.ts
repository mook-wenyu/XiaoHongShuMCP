/* 中文注释：键盘动作（逐字延迟、误触与自修正） */
import type { Locator, Page } from "playwright"
import { charDelayByWPM } from "../delays.js"
import { getNeighborKey } from "../core/randomization.js"

export interface TypeOptions { wpm?: number; mistakeRate?: number; autocorrect?: boolean }

export async function typeHumanized(loc: Locator, text: string, opts: TypeOptions = {}) {
  const wpm = opts.wpm ?? 180
  const perChar = charDelayByWPM(wpm)
  const mistakeRate = Math.max(0, Math.min(0.3, opts.mistakeRate ?? 0))
  const autocorrect = opts.autocorrect ?? false

  for (const raw of text.split("")) {
    let ch = raw
    // 邻键误触（仅限字母）
    if (/^[a-zA-Z]$/.test(ch) && Math.random() < mistakeRate) {
      const n = getNeighborKey(ch)
      if (n) ch = n
    }
    await loc.type(ch, { delay: perChar(ch) })
    if (autocorrect && ch !== raw) {
      await loc.press("Backspace")
      await loc.type(raw, { delay: perChar(raw) })
    }
  }
}

export async function pressHuman(page: Page, key: string, baseDelay = 50) {
  await page.keyboard.press(key, { delay: Math.max(10, Math.min(200, baseDelay + (Math.random() * 40 - 20))) })
}
