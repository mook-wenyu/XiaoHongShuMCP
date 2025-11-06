import { describe, it, expect } from "vitest"
import { ok as okRes, fail as failRes } from "../src/mcp/utils/result.js"

describe("ActionResult utils", () => {
  it("ok() returns ok: true with value", () => {
    const r = okRes({ a: 1 })
    expect(r.ok).toBe(true)
    expect(r.value?.a).toBe(1)
  })

  it("fail() returns ok: false with error", () => {
    const r = failRes({ code: "X", message: "msg" })
    expect(r.ok).toBe(false)
    expect(r.error?.code).toBe("X")
  })
})
