import { describe, it, expect } from "vitest"
import { __computeScrollStepForTest } from "../../src/domain/xhs/navigation"

// 中文注释：验证视口步长计算逻辑（比例/上限/下限）
describe("xhs scroll step compute", () => {
  it("uses ratio of viewport with caps", () => {
    const confStep = 1400
    expect(__computeScrollStepForTest(1000, undefined, confStep, 0.55)).toBe(550)
    expect(__computeScrollStepForTest(3000, undefined, confStep, 0.8)).toBe(confStep) // 上限为 confStep
    expect(__computeScrollStepForTest(200, undefined, confStep, 0.55)).toBe(160) // 下限 160
  })
  it("prefers explicit opts.scrollStep when provided", () => {
    const confStep = 1400
    expect(__computeScrollStepForTest(1000, 700, confStep, 0.55)).toBe(700)
  })
})
