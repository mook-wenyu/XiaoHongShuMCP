/* 中文：最小 MCP 客户端，调用 xhs_search_keyword */
import { Client } from "@modelcontextprotocol/sdk/client/index.js";
import { StdioClientTransport } from "@modelcontextprotocol/sdk/client/stdio.js";

function getArg(name: string, def?: string) {
	const entries = process.argv.filter((a) => a.startsWith(`--${name}=`));
	const found = entries.length > 0 ? entries[entries.length - 1] : undefined;
	return found ? found.split("=")[1] : def;
}

(async () => {
	const dirId = getArg("dirId", "user")!;
	const workspaceId = getArg("workspaceId");
	const keyword = getArg("keyword", "美食")!;

	const transport = new StdioClientTransport({
		command: process.platform === "win32" ? "npx.cmd" : "npx",
		args: ["tsx", "src/mcp/server.ts"],
		env: process.env,
	});
	const client = new Client({ name: "local-mcp-client", version: "0.1.0" });
	await client.connect(transport);

	const res = await client.callTool({
		name: "xhs_search_keyword",
		arguments: workspaceId ? { dirId, keyword, workspaceId } : { dirId, keyword },
	});

	process.stderr.write(JSON.stringify(res) + "\n");
	process.exit(0);
})();
