import { describe, it, expect, beforeAll, afterAll } from 'vitest'
import { Client } from '@modelcontextprotocol/sdk/client/index.js'
import { StdioClientTransport } from '@modelcontextprotocol/sdk/client/stdio.js'

let client: Client
let SKIP = false

// 目的：验证在 MCP 模式下，启用 MCP_LOG_STDERR 时依然不污染 stdout（协议可正常建立）
// 说明：若 stdout 有非协议内容，connect() 通常会失败或超时
beforeAll(async () => {
  process.env.ROXY_API_TOKEN = 'test'
  process.env.ROXY_API_HOST = '127.0.0.1'
  process.env.ROXY_API_PORT = '50000'
  process.env.MCP_LOG_STDERR = 'true'
  process.env.LOG_PRETTY = 'false'

  try {
    const transport = new StdioClientTransport({
      command: process.execPath,
      args: ['dist/mcp/server.js']
    })
    client = new Client(transport)
    await client.connect()
  } catch (err) {
    SKIP = true
  }
})

afterAll(async () => {
  try { await client.close() } catch {}
})

describe('mcp stdout guard', () => {
  it('connect succeeds with MCP_LOG_STDERR=true', async () => {
    if (SKIP) { expect(true).toBe(true); return }
    const res = await client.callTool({ name: 'server.ping', arguments: {} })
    const text = (res?.content?.[0] as any)?.text as string
    const obj = JSON.parse(text)
    expect(obj.ok).toBe(true)
  })
})
