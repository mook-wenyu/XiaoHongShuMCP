/* 中文说明：资源工具（简版）：列出/读取 artifacts 与页面快照 */
import { z } from "zod";
import type { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import type { ServiceContainer } from "../../core/container.js";
import type { RoxyBrowserManager } from "../../services/roxyBrowser.js";
import { ok } from "../utils/result.js";
import { err } from "../utils/errors.js";
import { promises as fs } from "node:fs";
import { join } from "node:path";
import * as Pages from "../../services/pages.js";

const DirId = z.string().min(1);
const WorkspaceId = z.string().optional();

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

export function registerResourceTools(
	server: McpServer,
	container: ServiceContainer,
	manager: RoxyBrowserManager,
) {
	// resources_listArtifacts
	server.registerTool(
		"resources_listArtifacts",
		{
			description: "列出 artifacts/<dirId> 下的文件（相对路径数组）",
			inputSchema: { dirId: DirId },
		},
		async (input: any) => {
			try {
				const { dirId } = input as any;
				const root = join("artifacts", dirId);
				const files = await listFiles(root);
				return { content: [{ type: "text", text: JSON.stringify(ok({ root, files })) }] };
			} catch (e: any) {
				return {
					content: [
						{ type: "text", text: JSON.stringify(err("INTERNAL_ERROR", String(e?.message || e))) },
					],
				};
			}
		},
	);

	// resources_readArtifact（返回 text 或 image）
	server.registerTool(
		"resources_readArtifact",
		{
			description: "读取 artifacts 文件；若为 png/jpg 则返回 image 内容项",
			inputSchema: { dirId: DirId, path: z.string().min(1) },
		},
		async (input: any) => {
			try {
				const { dirId, path } = input as any;
				const full = join("artifacts", dirId, path);
				const buf = await fs.readFile(full);
				const lower = full.toLowerCase();
				if (lower.endsWith(".png")) {
					return {
						content: [{ type: "image", data: buf.toString("base64"), mimeType: "image/png" }],
					};
				}
				if (lower.endsWith(".jpg") || lower.endsWith(".jpeg")) {
					return {
						content: [{ type: "image", data: buf.toString("base64"), mimeType: "image/jpeg" }],
					};
				}
				return { content: [{ type: "text", text: buf.toString("utf-8") }] };
			} catch (e: any) {
				return {
					content: [
						{ type: "text", text: JSON.stringify(err("INTERNAL_ERROR", String(e?.message || e))) },
					],
				};
			}
		},
	);

	// page_snapshot（可访问性快照，裁剪输出 + 统计）
	server.registerTool(
		"page_snapshot",
		{
			description: "返回可访问性快照（a11y）以及 url/title（可选 pageIndex），并附加统计",
			inputSchema: {
				dirId: DirId,
				pageIndex: z.number().int().nonnegative().optional(),
				workspaceId: WorkspaceId,
				maxNodes: z.number().int().positive().default(800).optional(),
			},
		},
		async (input: any) => {
			try {
				const { dirId, pageIndex, workspaceId, maxNodes } = input as any;
				const context = await manager.getContext(dirId, { workspaceId });
				const page = await Pages.ensurePage(context, { pageIndex });
				const snap = await page.accessibility
					.snapshot({ interestingOnly: true })
					.catch(() => undefined);
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
						if (count >= (maxNodes ?? 800)) return;
						const kids = Array.isArray(node.children) ? node.children : [];
						for (const k of kids) {
							if (count >= (maxNodes ?? 800)) break;
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
						stats: {
							nodeCount: count + 1,
							roleCounts,
							landmarks: Array.from(seenLandmarks).sort(),
						},
					};
				})();
				let clickableCount: number | undefined = undefined;
				try {
					clickableCount = await page.evaluate(() => {
						const qs =
							'a,button,[role="button"],[onclick],input[type="submit"],input[type="button"],summary,area[href]';
						return document.querySelectorAll(qs).length;
					});
				} catch {}
				return {
					content: [
						{
							type: "text",
							text: JSON.stringify(
								ok({
									url: page.url(),
									title: await page.title().catch(() => undefined),
									a11y: result.tree,
									stats: { ...(result.stats || {}), clickableCount },
								}),
							),
						},
					],
				};
			} catch (e: any) {
				return {
					content: [
						{ type: "text", text: JSON.stringify(err("INTERNAL_ERROR", String(e?.message || e))) },
					],
				};
			}
		},
	);
}
