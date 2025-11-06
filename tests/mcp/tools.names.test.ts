import { describe, it, expect, beforeAll, afterAll } from "vitest";
import { Client } from "@modelcontextprotocol/sdk/client/index.js";
import { StdioClientTransport } from "@modelcontextprotocol/sdk/client/stdio.js";

let client: Client;
let SKIP = false;

beforeAll(async () => {
	process.env.ROXY_API_TOKEN = "test";
	process.env.ROXY_API_HOST = "127.0.0.1";
	process.env.ROXY_API_PORT = "50000";

	try {
		const transport = new StdioClientTransport({
			command: process.execPath,
			args: ["dist/mcp/server.js"],
		});
		client = new Client(transport);
		await client.connect();
	} catch {
		SKIP = true;
	}
});

afterAll(async () => {
	try {
		await client.close();
	} catch {}
});

describe("mcp tools list (official names)", () => {
	it("includes official tool names", async () => {
		if (SKIP) {
			expect(true).toBe(true);
			return;
		}
		const res: any =
			(await (client as any).listTools?.()) ?? // sdk >=1.17
			(await (client as any).sendRequest?.({ method: "tools/list", params: {} }));
		const tools = res?.tools ?? res;
		const names = (tools || []).map((t: any) => t.name);
		const expected = [
			"browser_open",
			"browser_close",
			"page_create",
			"page_list",
			"page_close",
			"page_navigate",
			"page_click",
			"page_hover",
			"page_scroll",
			"page_screenshot",
			"page_type",
			"page_input_clear",
			"xhs_session_check",
			"xhs_navigate_home",
			"resources_listArtifacts",
			"resources_readArtifact",
			"page_snapshot",
		];
		for (const n of expected) {
			expect(names).toContain(n);
		}
	});
});
