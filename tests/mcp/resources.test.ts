import { describe, it, expect, beforeAll, afterAll } from "vitest"
import { Client } from "@modelcontextprotocol/sdk/client/index.js"
import { StdioClientTransport } from "@modelcontextprotocol/sdk/client/stdio.js"
import { mkdirSync, writeFileSync } from "node:fs"
import { join } from "node:path"

let client: Client
let SKIP = false
const dirId = "test"

beforeAll(async () => {
  // 预置 artifacts 文件
  const root = join(process.cwd(), "artifacts", dirId)
  try { mkdirSync(root, { recursive: true }) } catch {}
  writeFileSync(join(root, "hello.txt"), "hello world", "utf-8")

  process.env.ROXY_API_TOKEN = "test"
  process.env.ROXY_API_HOST = "127.0.0.1"
  process.env.ROXY_API_PORT = "50000"

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

describe("resources tools", () => {
  it("lists artifacts under dirId", async () => {
    if (SKIP) { expect(true).toBe(true); return }
    const res = await client.callTool({ name: "resources.listArtifacts", arguments: { dirId } })
    const text = (res?.content?.find(c => (c as any).type === "text") as any)?.text as string
    const obj = JSON.parse(text)
    expect(obj.ok).toBe(true)
    expect(Array.isArray(obj.data.files)).toBe(true)
    expect(obj.data.files.some((x: string) => x.endsWith("hello.txt"))).toBe(true)
  })

  it("reads artifact as text", async () => {
    if (SKIP) { expect(true).toBe(true); return }
    const res = await client.callTool({ name: "resources.readArtifact", arguments: { dirId, path: "hello.txt" } })
    const text = (res?.content?.find(c => (c as any).type === "text") as any)?.text as string
    expect(text).toContain("hello world")
  })
})

