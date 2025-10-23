import { describe, it, expect, vi } from "vitest"
import { typeHumanized } from "../../../../src/humanization/actions/keyboard.js"

describe("keyboard actions", () => {
  it("types with wpm delays and autocorrect", async () => {
    const calls: string[] = []
    const loc: any = {
      type: vi.fn(async (ch: string) => { calls.push(ch) }),
      press: vi.fn(async (_: string) => { calls.push("<backspace>") }),
    }
    await typeHumanized(loc, "abc", { wpm: 200, mistakeRate: 0.5, autocorrect: true })
    expect(calls.length).toBeGreaterThanOrEqual(3)
    expect(loc.type).toHaveBeenCalled()
  })
})
