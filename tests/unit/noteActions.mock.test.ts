import { describe, it, expect, vi, beforeEach } from 'vitest'
import { __test } from '../../src/domain/xhs/noteActions.js'

// Mock humanization/actions to observe calls when testing clickHumanScoped
vi.mock('../../src/humanization/actions.js', () => ({
  clickHuman: vi.fn(async () => {}),
  hoverHuman: vi.fn(async () => {}),
  typeHuman: vi.fn(async () => {}),
}))

function makePage(styleOk = true, hitOk = true) {
  return {
    // evaluate: decide by source function body content
    evaluate: vi.fn(async (fn: any, args: any) => {
      const body = String(fn)
      if (body.includes('getComputedStyle')) return styleOk
      // elementFromPoint probe
      return hitOk
    }),
    waitForTimeout: vi.fn(async () => {}),
  } as any
}

function makeLocator({ visible = true, box = { x: 100, y: 100, width: 40, height: 20 } } = {}) {
  return {
    isVisible: vi.fn(async () => visible),
    boundingBox: vi.fn(async () => (visible ? box : null)),
    elementHandle: vi.fn(async () => ({ __el: true } as any)),
  } as any
}

describe('noteActions __test helpers', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('isClickable: true when visible + box valid + style ok + hit ok', async () => {
    const page = makePage(true, true)
    const loc = makeLocator()
    const ok = await __test.isClickable(page, loc)
    expect(ok).toBe(true)
  })

  it('isClickable: false when style not ok', async () => {
    const page = makePage(false, true)
    const loc = makeLocator()
    const ok = await __test.isClickable(page, loc)
    expect(ok).toBe(false)
  })

  it('isClickable: false when hit test fails', async () => {
    const page = makePage(true, false)
    const loc = makeLocator()
    const ok = await __test.isClickable(page, loc)
    expect(ok).toBe(false)
  })

  it('clickHumanScoped: hover+soft-wait then click when initially not clickable', async () => {
    const actions = await import('../../src/humanization/actions.js')
    const page = makePage(true, false) // first hit fails
    const loc = makeLocator()

    // first call to evaluate returns false (hit fail), second should return true
    let call = 0
    ;(page.evaluate as any).mockImplementation(async (fn: any, args: any) => {
      const body = String(fn)
      if (body.includes('getComputedStyle')) return true
      call++
      return call >= 2 // second time becomes clickable
    })

    await __test.clickHumanScoped(page, loc)
    expect(actions.hoverHuman).toHaveBeenCalled()
    expect(actions.clickHuman).toHaveBeenCalled()
  })
})
