/* 通用 MCP 调用脚本：按工具名与 JSON 参数调用本仓库 MCP 服务器
 * 用法示例：
 *   npx tsx scripts/mcp-call-generic.ts --tool=xhs_navigate_home --args='{"dirId":"user"}'
 *   npx tsx scripts/mcp-call-generic.ts --tool=xhs_search_keyword --args='{"dirId":"user","keyword":"Gemini"}'
 */
import "dotenv/config";
import { Client } from "@modelcontextprotocol/sdk/client/index.js";
import { StdioClientTransport } from "@modelcontextprotocol/sdk/client/stdio.js";

function getArg(name: string, def?: string) {
	const pair = process.argv.find((a) => a.startsWith(`--${name}=`));
	return pair ? pair.split("=", 2)[1] : def;
}

(async () => {
	const tool = getArg("tool");
	if (!tool) {
		console.error(JSON.stringify({ ok: false, error: "missing --tool" }));
		process.exit(2);
	}
	const argsRaw = getArg("args");
	let args: any = {};
	if (argsRaw) {
		try {
			args = JSON.parse(argsRaw);
		} catch (e: any) {
			console.error(
				JSON.stringify({ ok: false, error: `invalid --args JSON: ${String(e?.message || e)}` }),
			);
			process.exit(2);
		}
	} else {
		// 兼容 PowerShell 传参：--arg:dirId=xxx --arg:keyword=xxx
		// 数组支持：--arglist:keywords=a,b,c
		const listPairs = process.argv.filter((a) => a.startsWith("--arglist:"));
		for (const p of listPairs) {
			const kv = p.slice("--arglist:".length);
			const eq = kv.indexOf("=");
			if (eq > 0) {
				const k = kv.slice(0, eq);
				const v = kv.slice(eq + 1);
				const arr = v
					.split(",")
					.map((s) => s.trim())
					.filter(Boolean);
				args[k] = arr;
			}
		}
		const pairs = process.argv.filter((a) => a.startsWith("--arg:"));
		for (const p of pairs) {
			const kv = p.slice("--arg:".length);
			const eq = kv.indexOf("=");
			if (eq > 0) {
				const k = kv.slice(0, eq);
				const v = kv.slice(eq + 1);
				// 若已通过 arglist 设定数组，则跳过覆盖
				if (Array.isArray(args[k])) continue;
				// 尝试自动解析 JSON 原语
				if (v === "true" || v === "false") args[k] = v === "true";
				else if (!isNaN(Number(v))) args[k] = Number(v);
				else args[k] = v;
			}
		}
	}

	const transport = new StdioClientTransport({
		command: process.platform === "win32" ? "npx.cmd" : "npx",
		args: ["tsx", "src/mcp/server.ts"],
		env: process.env as any,
	});
	const client = new Client({ name: "local-mcp-client", version: "0.1.0" });
	await client.connect(transport);

	try {
		const res = await client.callTool({ name: tool, arguments: args });
		// 打印到 stderr，避免与 MCP 通道冲突
		process.stderr.write(JSON.stringify({ ok: true, tool, args, res }, null, 2) + "\n");
		process.exit(0);
	} catch (e: any) {
		process.stderr.write(
			JSON.stringify({ ok: false, tool, args, error: String(e?.message || e) }, null, 2) + "\n",
		);
		process.exit(1);
	}
})();
