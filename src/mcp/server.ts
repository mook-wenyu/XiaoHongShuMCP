/* MCP 服务器（stdio） - RoxyBrowser + Playwright 集成 */
import { McpServer, ResourceTemplate } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { ConfigProvider } from "../config/ConfigProvider.js";
import { ServiceContainer } from "../core/container.js";
import { registerBrowserTools } from "./tools/browser.js";
import { registerPageTools } from "./tools/page.js";
import { registerXhsTools } from "./tools/xhs.js";
import { registerXhsShortcutsTools } from "./tools/xhsShortcuts.js";
import { registerResourceTools } from "./tools/resources.js";
import type { IRoxyClient } from "../contracts/IRoxyClient.js";
import type { PolicyEnforcer } from "../services/policy.js";
import { registerRoxyAdminTools } from "./tools/roxyAdmin.js";
import * as Pages from "../services/pages.js";
import { promises as fs } from "node:fs";
import { join } from "node:path";

async function main() {
	const configProvider = ConfigProvider.load();
	const config = configProvider.getConfig();
	const container = new ServiceContainer(config, { loggerSilent: true });

	const roxyBrowserManager = container.createRoxyBrowserManager();

	const onSignal = async () => {
		try {
			await container.cleanup();
		} catch {}
		process.exit(0);
	};
	process.once("SIGINT", () => {
		void onSignal();
	});
	process.once("SIGTERM", () => {
		void onSignal();
	});

	const server = new McpServer({ name: "xhs-mcp", version: "0.2.0" });

	registerBrowserTools(server, container, roxyBrowserManager);
	registerPageTools(server, container, roxyBrowserManager);
	registerXhsTools(server, container, roxyBrowserManager);
	registerXhsShortcutsTools(server, container, roxyBrowserManager);
	registerResourceTools(server, container, roxyBrowserManager);
	// 仅保留官方命名的原子动作（browser./page.）；不再注册 roxy.* 浏览别名

	server.registerTool(
		"server_capabilities",
		{ description: "返回适配器与版本信息（附带 roxy 健康状态）", inputSchema: {} },
		async () => {
			const caps: any = {
				adapter: "roxyBrowser",
				version: "0.2.0",
				integration: "Playwright CDP",
				adminTools: true,
			};
			// 附带 roxy 健康探测（不抛错，仅报告）
			try {
				const health = await container.createRoxyClient().health();
				caps.roxyHealth = typeof health === "string" ? health : health?.code === 0 ? "ok" : health;
			} catch (e: any) {
				caps.roxyHealth = `error: ${String(e?.message || e)}`;
			}
			return { content: [{ type: "text", text: JSON.stringify(caps) }] };
		},
	);

	// 永久开启高权限 Roxy 管理类工具（工作区/窗口管理）
	try {
		const roxy: IRoxyClient = container.createRoxyClient();
		const policy: PolicyEnforcer = container.createPolicyEnforcer();
		registerRoxyAdminTools(server, roxy, policy);
	} catch {}

	server.registerResource(
		"xhs-artifacts-index",
		new ResourceTemplate("xhs://artifacts/{dirId}/index", { list: undefined }),
		{
			title: "Artifacts Index",
			description: "列出 artifacts/<dirId> 下的文件",
			mimeType: "application/json",
		},
		async (_uri, { dirId }) => {
			async function listFiles(root: string): Promise<string[]> {
				const out: string[] = [];
				async function walk(p: string, base: string) {
					let ents: any[] = [];
					try {
						ents = await fs.readdir(p, { withFileTypes: true });
					} catch {
						return;
					}
					for (const e of ents) {
						const full = join(p, e.name);
						const rel = join(base, e.name);
						if (e.isDirectory()) await walk(full, rel);
						else out.push(rel);
					}
				}
				await walk(root, "");
				return out.sort();
			}
			const root = join("artifacts", String(dirId ?? ""));
			const files = await listFiles(root);
			const text = JSON.stringify({ root, files });
			return { contents: [{ uri: _uri.href, text, mimeType: "application/json" }] };
		},
	);

	server.registerResource(
		"xhs-page-snapshot",
		new ResourceTemplate("xhs://snapshot/{dirId}/{page}", { list: undefined }),
		{ title: "Page Snapshot", description: "返回 a11y 快照与统计", mimeType: "application/json" },
		async (_uri, { dirId, page }) => {
			const pageIndex = Number(page);
			const context = await roxyBrowserManager.getContext(String(dirId ?? ""));
			const p = await Pages.ensurePage(context, {
				pageIndex: isNaN(pageIndex) ? undefined : pageIndex,
			});
			const snap = await p.accessibility.snapshot({ interestingOnly: true }).catch(() => undefined);
			const maxNodes = Math.max(100, Math.min(5000, Number(process.env.SNAPSHOT_MAX_NODES || 800)));
			const result = (() => {
				if (!snap) return { tree: undefined, stats: undefined };
				const out: any = { role: snap.role, name: snap.name, children: [] as any[] };
				let count = 0;
				const roleCounts: Record<string, number> = {};
				const landmarkRoles = new Set([
					"banner",
					"navigation",
					"main",
					"contentinfo",
					"search",
					"complementary",
					"form",
					"region",
				]);
				const seenLandmarks = new Set<string>();
				function addRole(role?: string) {
					if (!role) return;
					roleCounts[role] = (roleCounts[role] || 0) + 1;
					if (landmarkRoles.has(role)) seenLandmarks.add(role);
				}
				addRole(snap.role);
				function walk(node: any, into: any) {
					if (count >= maxNodes) return;
					const kids = Array.isArray(node.children) ? node.children : [];
					for (const k of kids) {
						if (count >= maxNodes) break;
						const child = { role: k.role, name: k.name } as any;
						(into.children as any[]).push(child);
						count++;
						addRole(k.role);
						if (k.children) {
							child.children = [];
							walk(k, child);
						}
					}
				}
				walk(snap, out);
				return {
					tree: out,
					stats: { nodeCount: count + 1, roleCounts, landmarks: Array.from(seenLandmarks).sort() },
				};
			})();
			let clickableCount: number | undefined = undefined;
			try {
				clickableCount = await p.evaluate(() => {
					const qs =
						'a,button,[role="button"],[onclick],input[type="submit"],input[type="button"],summary,area[href]';
					return document.querySelectorAll(qs).length;
				});
			} catch {}
			const text = JSON.stringify({
				url: p.url(),
				title: await p.title().catch(() => undefined),
				a11y: result.tree,
				stats: { ...(result.stats || {}), clickableCount },
			});
			return { contents: [{ uri: _uri.href, text, mimeType: "application/json" }] };
		},
	);

	server.registerTool("server_ping", { description: "连通性/心跳", inputSchema: {} }, async () => ({
		content: [{ type: "text", text: JSON.stringify({ ok: true, ts: Date.now() }) }],
	}));

	const transport = new StdioServerTransport();
	await server.connect(transport);
}

main().catch(async (e) => {
	process.stderr.write(`MCP 服务器启动失败: ${e}\n`);
	process.exit(1);
});
