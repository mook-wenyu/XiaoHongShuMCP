/* 中文：MCP 客户端依次调用 home→search→select_note 做联通性检查 */
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
  const keywordSingle = getArg("keyword");
  const keywordsArg = getArg("keywords");
  const keywords = keywordsArg ? keywordsArg.split(',').map(s => s.trim()).filter(Boolean) : (keywordSingle ? [keywordSingle] : ["美食"]);

  const transport = new StdioClientTransport({
    command: process.platform === 'win32' ? 'npx.cmd' : 'npx',
    args: ['tsx', 'src/mcp/server.ts'],
    env: process.env
  });
  const client = new Client({ name: 'local-mcp-client', version: '0.1.0' });
  await client.connect(transport);

  // 1) 到探索页主页
  await client.callTool({ name: 'xhs.navigate.home', arguments: workspaceId ? { dirId, workspaceId } : { dirId } });

  // 2) 关键词浏览（会滚动一段，提升可见文本覆盖）
  await client.callTool({ name: 'xhs_keyword_browse', arguments: workspaceId ? { dirId, keywords, workspaceId } : { dirId, keywords } });

  // 3) 根据关键词选择一条笔记（再次尝试）
  const result = await client.callTool({ name: 'xhs_select_note', arguments: workspaceId ? { dirId, keywords, workspaceId } : { dirId, keywords } });

  process.stderr.write(JSON.stringify({ ok: true, steps: { keywords }, result }) + "\n");
  process.exit(0);
})();
