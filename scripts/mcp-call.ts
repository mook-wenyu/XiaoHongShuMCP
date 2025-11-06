/* 中文：最小 MCP 客户端，通过 stdio 启动本仓库的 MCP 服务器并调用工具 */
import { Client } from "@modelcontextprotocol/sdk/client/index.js";
import { StdioClientTransport } from "@modelcontextprotocol/sdk/client/stdio.js";

function getArg(name: string, def?: string) {
	const entries = process.argv.filter((a) => a.startsWith(`--${name}=`));
	const found = entries.length > 0 ? entries[entries.length - 1] : undefined; // 取最后一次出现
	return found ? found.split("=")[1] : def;
}

(async () => {
	const dirId = getArg("dirId", "user")!;
	const workspaceId = getArg("workspaceId");

	const transport = new StdioClientTransport({
		command: process.platform === "win32" ? "npx.cmd" : "npx",
		args: ["tsx", "src/mcp/server.ts"],
		env: process.env,
	});

	const client = new Client({ name: "local-mcp-client", version: "0.1.0" });
	await client.connect(transport);

	const res = await client.callTool({
		name: "xhs_navigate_home",
		arguments: workspaceId ? { dirId, workspaceId } : { dirId },
	});

	// 将结果打印到 stderr，避免与 MCP stdio 冲突
	process.stderr.write(JSON.stringify(res) + "\n");
	process.exit(0);
})();
