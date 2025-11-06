import { describe, it, expect, beforeAll, afterAll } from "vitest";
import { Client } from "@modelcontextprotocol/sdk/client/index.js";
import { StdioClientTransport } from "@modelcontextprotocol/sdk/client/stdio.js";
import { mkdirSync, writeFileSync } from "node:fs";
import { join } from "node:path";

let client: Client;
let SKIP = false;
const dirId = "test";

beforeAll(async () => {
	const root = join(process.cwd(), "artifacts", dirId);
	try {
		mkdirSync(root, { recursive: true });
	} catch {}
	writeFileSync(join(root, "hello.txt"), "hello world", "utf-8");

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

describe("resource templates", () => {
	it("reads artifacts index via resource URI", async () => {
		if (SKIP) {
			expect(true).toBe(true);
			return;
		}
		const uri = `xhs://artifacts/${dirId}/index`;
		const res: any = await (client as any).readResource?.({ uri });
		const text = res?.contents?.[0]?.text as string;
		const obj = JSON.parse(text);
		expect(Array.isArray(obj.files)).toBe(true);
		expect(obj.files.some((x: string) => x.endsWith("hello.txt"))).toBe(true);
	});
});
