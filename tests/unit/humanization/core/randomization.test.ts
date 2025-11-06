import { describe, it, expect } from "vitest"
import { jitter, randomVariance, getNeighborKey, QWERTY_NEIGHBORS } from "../../../../src/humanization/core/randomization.js"

describe("randomization", () => {
  it("jitter bounds", () => {
    for (let i = 0; i < 50; i++) {
      const v = jitter(100, 10, 80, 120)
      expect(v).toBeGreaterThanOrEqual(80)
      expect(v).toBeLessThanOrEqual(120)
    }
  })
  it("randomVariance percent", () => {
    for (let i = 0; i < 20; i++) {
      const v = randomVariance(100, 0.1)
      expect(v).toBeGreaterThanOrEqual(90)
      expect(v).toBeLessThanOrEqual(110)
    }
  })
  it("neighbor keys", () => {
    expect(QWERTY_NEIGHBORS["a"]).toContain("q")
    const k = getNeighborKey("E")
    if (k) {
      expect(QWERTY_NEIGHBORS["e"]).toContain(k)
    }
  })
})
