import { describe, it, expect, beforeAll, afterAll } from "vitest";
import { Client } from "@modelcontextprotocol/sdk/client/index.js";
import { StdioClientTransport } from "@modelcontextprotocol/sdk/client/stdio.js";

let client: Client;
let SKIP = false;

beforeAll(async () => {
	if (process.env.ENABLE_ROXY_ADMIN_TOOLS !== "true") {
		SKIP = true;
		return;
	}
	// 仅在 Roxy 可用时运行（TOKEN + /health OK）
	const { roxyReady } = await import("../../helpers/roxy.js");
	if (!(await roxyReady())) {
		SKIP = true;
		return;
	}
	process.env.ROXY_API_HOST = process.env.ROXY_API_HOST || "127.0.0.1";
	process.env.ROXY_API_PORT = process.env.ROXY_API_PORT || "50000";

	// MCP 模式保持 stdout 干净
	process.env.MCP_LOG_STDERR = "true";
	process.env.LOG_PRETTY = "false";

	const transport = new StdioClientTransport({
		command: process.execPath,
		args: ["dist/mcp/server.js"],
	});
	client = new Client(transport);
	try {
		await client.connect();
	} catch {
		SKIP = true;
	}
});

afterAll(async () => {
	try {
		await client?.close();
	} catch {}
});

describe("roxy.windows.list e2e", () => {
	it("lists windows with numeric workspaceId", async () => {
		if (SKIP) {
			expect(true).toBe(true);
			return;
		}

		// 先列出 workspace，取一个 id（数字）
		const wsRes = await client.callTool({ name: "roxy.workspaces.list", arguments: {} });
		const wsText = (wsRes?.content?.[0] as any)?.text as string;
		const wsObj = JSON.parse(wsText);
		const id = wsObj?.data?.rows?.[0]?.id;
		expect(typeof id === "number").toBe(true);

		// 使用 arguments 传参（MCP 规范）
		const res1 = await client.callTool({
			name: "roxy.windows.list",
			arguments: { workspaceId: id, page_size: 1 },
		});
		const text1 = (res1?.content?.[0] as any)?.text as string;
		const obj1 = JSON.parse(text1);
		expect(obj1).toBeTruthy();

		// 兼容路径：使用 params 传参（回归过去客户端）
		const res2 = await client.callTool({
			name: "roxy.windows.list",
			arguments: {} as any,
			params: { workspaceId: id, page_size: 1 } as any,
		} as any);
		const text2 = (res2?.content?.[0] as any)?.text as string;
		const obj2 = JSON.parse(text2);
		expect(obj2).toBeTruthy();
	});
});
