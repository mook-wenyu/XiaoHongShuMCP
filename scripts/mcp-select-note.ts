/* 中文：MCP 客户端依次调用 home→search→select_note 做联通性检查 */
import { Client } from "@modelcontextprotocol/sdk/client/index.js";
import { StdioClientTransport } from "@modelcontextprotocol/sdk/client/stdio.js";

function getArg(name: string, def?: string) {
	const entries = process.argv.filter((a) => a.startsWith(`--${name}=`));
	const found = entries.length > 0 ? entries[entries.length - 1] : undefined;
	return found ? found.split("=")[1] : def;
}

(async () => {
	// 超时与行为默认值，防止长时间挂起
	process.env.XHS_OPEN_WAIT_MS = process.env.XHS_OPEN_WAIT_MS || "1500";
	// 默认 10 分钟，以覆盖复杂场景下的首次连接与加载
	const TOOL_TIMEOUT = Number(process.env.MCP_TOOL_TIMEOUT_MS || 600000);

	async function callWithTimeout<T>(
		p: Promise<T>,
		ms: number,
		label: string,
		onTimeout?: () => Promise<void>,
	) {
		return new Promise<T>((resolve, reject) => {
			const t = setTimeout(async () => {
				try {
					if (onTimeout) await onTimeout();
				} catch {}
				reject(new Error(`TIMEOUT:${label}:${ms}`));
			}, ms);
			p.then((v) => {
				clearTimeout(t);
				resolve(v);
			}).catch((e) => {
				clearTimeout(t);
				reject(e);
			});
		});
	}
	const dirId = getArg("dirId", "user")!;
	const workspaceId = getArg("workspaceId");
	const keywordSingle = getArg("keyword");
	const keywordsArg = getArg("keywords");
	const keywords = keywordsArg
		? keywordsArg
				.split(",")
				.map((s) => s.trim())
				.filter(Boolean)
		: keywordSingle
			? [keywordSingle]
			: ["美食"];

	async function callToolOnce(name: string, args: any) {
		const transport = new StdioClientTransport({
			command: process.platform === "win32" ? "npx.cmd" : "npx",
			args: ["tsx", "src/mcp/server.ts"],
			env: process.env,
		});
		const client = new Client({ name: "local-mcp-client", version: "0.1.0" });
		await client.connect(transport);
		try {
			const res = await callWithTimeout(
				client.callTool({ name, arguments: args }),
				TOOL_TIMEOUT,
				name,
			);
			try {
				await (client as any)?.close?.();
				await (transport as any)?.close?.();
			} catch {}
			return res;
		} catch (e) {
			try {
				await (client as any)?.close?.();
				await (transport as any)?.close?.();
			} catch {}
			throw e;
		}
	}

	// 1) 到探索页主页
	await callToolOnce("xhs_navigate_home", workspaceId ? { dirId, workspaceId } : { dirId });

	// 2) 关键词浏览（会滚动一段，提升可见文本覆盖）
	await callToolOnce(
		"xhs_keyword_browse",
		workspaceId ? { dirId, keywords, workspaceId } : { dirId, keywords },
	);

	// 3) 根据关键词选择一条笔记（再次尝试）
	const result = await callToolOnce(
		"xhs_select_note",
		workspaceId ? { dirId, keywords, workspaceId } : { dirId, keywords },
	);

	// 4) 截图验证（整页或视口均可，这里用视口即可）
	const shotRes = await callToolOnce(
		"page_screenshot",
		workspaceId ? { dirId, workspaceId } : { dirId },
	);
	// 仅输出路径，避免 base64 图片导致管道压力
	function extractPath(res: any): string | undefined {
		try {
			const txt = (res?.content || []).find((c: any) => c?.type === "text")?.text;
			if (!txt) return undefined;
			const obj = JSON.parse(txt);
			// 兼容 ok({ value: { path } }) 与历史形态
			if (obj?.ok === true) return obj?.value?.path ?? obj?.ok?.path ?? obj?.path;
			return obj?.path;
		} catch {
			return undefined;
		}
	}
	const screenshotPath = extractPath(shotRes);

	process.stderr.write(
		JSON.stringify({ ok: true, steps: { keywords }, result, screenshotPath }, null, 2) + "\n",
	);
	process.exit(0);
})();
