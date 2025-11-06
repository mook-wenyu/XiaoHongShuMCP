import { describe, it, expect } from "vitest"
import { selectorIdFromTarget } from "../../../src/selectors/id.js"

describe("selectorIdFromTarget", () => {
  it("stable hashing for string", () => {
    const a = selectorIdFromTarget("#root")
    const b = selectorIdFromTarget("#root")
    expect(a).toBe(b)
  })
  it("works for object", () => {
    const id = selectorIdFromTarget({ role: "button", name: "Search" })
    expect(id.startsWith("sel:")).toBe(true)
  })
})
