/* 中文：MCP 客户端顺序执行 search → close_modal → select_note（测试用） */
import { Client } from "@modelcontextprotocol/sdk/client/index.js";
import { StdioClientTransport } from "@modelcontextprotocol/sdk/client/stdio.js";

function getArg(name: string, def?: string) {
  const entries = process.argv.filter(a => a.startsWith(`--${name}=`));
  const found = entries.length > 0 ? entries[entries.length - 1] : undefined;
  return found ? found.split("=")[1] : def;
}

(async () => {
  const dirId = getArg("dirId", "user")!;
  const workspaceId = getArg("workspaceId");
  const keywords = (getArg("keywords", "美食,穿搭") || "").split(",").map(s => s.trim()).filter(Boolean);
  const searchKeyword = getArg("search", keywords[0] || "美食")!;

  const transport = new StdioClientTransport({
    command: process.platform === 'win32' ? 'npx.cmd' : 'npx',
    args: ['tsx', 'src/mcp/server.ts'],
    env: process.env
  });
  const client = new Client({ name: 'local-mcp-client', version: '0.1.0' });
  await client.connect(transport);

  // 1) 搜索
  const searchRes = await client.callTool({
    name: 'xhs_search_keyword',
    arguments: workspaceId ? { dirId, keyword: searchKeyword, workspaceId } : { dirId, keyword: searchKeyword }
  });

  // 2) 关闭模态（若有）
  const closeRes = await client.callTool({
    name: 'xhs_close_modal',
    arguments: workspaceId ? { dirId, workspaceId } : { dirId }
  });

  // 3) 选择笔记
  const selectRes = await client.callTool({
    name: 'xhs_select_note',
    arguments: workspaceId ? { dirId, keywords, workspaceId } : { dirId, keywords }
  });

  process.stderr.write(JSON.stringify({ searchRes, closeRes, selectRes }) + "\n");
  process.exit(0);
})();
