/* 中文：MCP 客户端调用 runner.runTask 执行 openAndScreenshot（探索页） */
import { Client } from "@modelcontextprotocol/sdk/client/index.js";
import { StdioClientTransport } from "@modelcontextprotocol/sdk/client/stdio.js";

function getArg(name: string, def?: string) {
  const entries = process.argv.filter(a => a.startsWith(`--${name}=`));
  const found = entries.length > 0 ? entries[entries.length - 1] : undefined; // 取最后一次出现以覆盖默认
  return found ? found.split("=")[1] : def;
}

(async () => {
  const dirId = getArg("dirId", "user")!;
  const workspaceId = getArg("workspaceId");
  const url = getArg("url", "https://www.xiaohongshu.com/explore")!;

  const transport = new StdioClientTransport({
    command: process.platform === 'win32' ? 'npx.cmd' : 'npx',
    args: ['tsx', 'src/mcp/server.ts'],
    env: process.env
  });

  const client = new Client({ name: 'local-mcp-client', version: '0.1.0' });
  await client.connect(transport);

  const res = await client.callTool({
    name: 'runner.runTask',
    arguments: workspaceId
      ? { taskName: 'openAndScreenshot', url, payload: {}, dirIds: [dirId], limit: 1, workspaceId }
      : { taskName: 'openAndScreenshot', url, payload: {}, dirIds: [dirId], limit: 1 }
  });

  process.stderr.write(JSON.stringify(res) + "\n");
  process.exit(0);
})();
