import { describe, it, expect } from "vitest"
import { cubicBezier, makeCurvePath } from "../../../../src/humanization/core/curves.js"

describe("cubic curves", () => {
  it("endpoints", () => {
    const p0 = { x: 0, y: 0 }
    const p1 = { x: 10, y: 0 }
    const p2 = { x: 90, y: 0 }
    const p3 = { x: 100, y: 0 }
    expect(cubicBezier(p0, p1, p2, p3, 0)).toEqual(p0)
    expect(cubicBezier(p0, p1, p2, p3, 1)).toEqual(p3)
  })
  it("path smoothness", () => {
    const path = makeCurvePath({ x: 0, y: 0 }, { x: 100, y: 0 }, { steps: 30, randomness: 0.1 })
    expect(path.length).toBeGreaterThan(20)
    for (let i = 1; i < path.length; i++) {
      const dx = path[i].x - path[i - 1].x
      const dy = path[i].y - path[i - 1].y
      expect(Math.hypot(dx, dy)).toBeLessThan(20)
    }
  })
})
