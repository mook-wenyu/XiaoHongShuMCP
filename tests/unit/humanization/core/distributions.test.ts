import { describe, it, expect } from "vitest"
import { randomNormal, randomUniform, randomPoisson } from "../../../../src/humanization/core/distributions.js"

describe("distributions", () => {
  it("normal stats", () => {
    const samples = Array.from({ length: 5000 }, () => randomNormal(100, 15))
    const mean = samples.reduce((a, b) => a + b, 0) / samples.length
    const variance = samples.reduce((a, b) => a + (b - mean) ** 2, 0) / samples.length
    expect(mean).toBeGreaterThan(95)
    expect(mean).toBeLessThan(105)
    expect(Math.sqrt(variance)).toBeGreaterThan(12)
    expect(Math.sqrt(variance)).toBeLessThan(18)
  })
  it("uniform bounds", () => {
    const samples = Array.from({ length: 1000 }, () => randomUniform(10, 20))
    expect(Math.min(...samples)).toBeGreaterThanOrEqual(10)
    expect(Math.max(...samples)).toBeLessThanOrEqual(20)
  })
  it("poisson mean", () => {
    const samples = Array.from({ length: 5000 }, () => randomPoisson(3))
    const mean = samples.reduce((a, b) => a + b, 0) / samples.length
    expect(mean).toBeGreaterThan(2.5)
    expect(mean).toBeLessThan(3.5)
  })
})
