import { describe, it, expect, beforeAll, afterAll } from "vitest"
import { Client } from "@modelcontextprotocol/sdk/client/index.js"
import { StdioClientTransport } from "@modelcontextprotocol/sdk/client/stdio.js"

let client: Client
let SKIP = process.env.ENABLE_SNAPSHOT_TEST !== "true"
const dirId = process.env.DIR_ID || "user"

beforeAll(async () => {
  if (SKIP) return
  if (!process.env.ROXY_API_TOKEN) SKIP = true

  try {
    const transport = new StdioClientTransport({
      command: process.execPath,
      args: ["dist/mcp/server.js"]
    })
    client = new Client(transport)
    await client.connect()
  } catch {
    SKIP = true
  }
})

afterAll(async () => {
  try { await client.close() } catch {}
})

describe("page.snapshot (optional, requires Roxy)", () => {
  it("returns a11y summary", async () => {
    if (SKIP) { expect(true).toBe(true); return }
    // ensure window exists
    await client.callTool({ name: "browser.open", arguments: { dirId } })
    await client.callTool({ name: "page.navigate", arguments: { dirId, url: "https://example.com" } })
    const res = await client.callTool({ name: "page.snapshot", arguments: { dirId, maxNodes: 100 } })
    const text = (res?.content?.find(c => (c as any).type === "text") as any)?.text as string
    const obj = JSON.parse(text)
    expect(obj.ok).toBe(true)
    expect(obj.data.url).toContain("example.com")
  })
})

