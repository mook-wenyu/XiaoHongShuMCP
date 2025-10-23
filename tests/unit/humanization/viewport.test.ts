import { describe, it, expect, vi } from "vitest"
import { ensureVisible, centerInViewport } from "../../../src/humanization/viewport.js"

function makeFakeLocator() {
  return {
    scrollIntoViewIfNeeded: vi.fn(async () => {}),
    waitFor: vi.fn(async (_: any) => {}),
    boundingBox: vi.fn(async () => ({ x: 100, y: 200, width: 50, height: 40 })),
  }
}

function makeFakePage() {
  return {
    viewportSize: () => ({ width: 800, height: 600 }),
    evaluate: vi.fn(async () => {}),
  }
}

describe("viewport utils", () => {
  it("ensureVisible calls scrollIntoViewIfNeeded and waits visible", async () => {
    const loc: any = makeFakeLocator()
    await ensureVisible(loc)
    expect(loc.scrollIntoViewIfNeeded).toHaveBeenCalledTimes(1)
    expect(loc.waitFor).toHaveBeenCalledTimes(1)
  })

  it("centerInViewport falls back to ensureVisible when no viewport", async () => {
    const loc: any = makeFakeLocator()
    const page: any = { viewportSize: () => undefined }
    await centerInViewport(page, loc)
    expect(loc.waitFor).toHaveBeenCalledTimes(1)
  })

  it("centerInViewport scrolls then ensures visible", async () => {
    const loc: any = makeFakeLocator()
    const page: any = makeFakePage()
    await centerInViewport(page, loc)
    expect(page.evaluate).toHaveBeenCalled()
    expect(loc.waitFor).toHaveBeenCalledTimes(1)
  })
})
