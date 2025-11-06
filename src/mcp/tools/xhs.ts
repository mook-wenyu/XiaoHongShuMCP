/* 中文说明：小红书快捷工具（官方风格命名前缀 xhs.*），仅保留会话检查与导航示例 */
import { z } from "zod";
import type { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import type { ServiceContainer } from "../../core/container.js";
import type { RoxyBrowserManager } from "../../services/roxyBrowser.js";
import { ok } from "../utils/result.js";
import { err } from "../utils/errors.js";
import * as Pages from "../../services/pages.js";

const DirId = z.string().min(1);
const WorkspaceId = z.string().optional();

export function registerXhsTools(
	server: McpServer,
	container: ServiceContainer,
	manager: RoxyBrowserManager,
) {
	// xhs_session_check —— 使用现有 domain/xhs/session 进行基于首页/ cookies 的快速判定
	server.registerTool(
		"xhs_session_check",
		{
			description: "检查小红书会话（cookies/首页加载为依据，快速判定）",
			inputSchema: { dirId: DirId, workspaceId: WorkspaceId },
		},
		async (input: any) => {
			try {
				const { dirId, workspaceId } = input as any;
				const context = await manager.getContext(dirId, { workspaceId });
				const { checkSession } = await import("../../domain/xhs/session.js");
				const r = await checkSession(context);
				return { content: [{ type: "text", text: JSON.stringify(ok(r)) }] };
			} catch (e: any) {
				return {
					content: [
						{ type: "text", text: JSON.stringify(err("INTERNAL_ERROR", String(e?.message || e))) },
					],
				};
			}
		},
	);

	// xhs_navigate_home —— 导航到首页 explore
	server.registerTool(
		"xhs_navigate_home",
		{
			description: "导航到小红书首页 explore 并做轻量验证",
			inputSchema: { dirId: DirId, workspaceId: WorkspaceId },
		},
		async (input: any) => {
			try {
				const { dirId, workspaceId } = input as any;
				const context = await manager.getContext(dirId, { workspaceId });
				const page = await Pages.ensurePage(context, {});
				await page.goto("https://www.xiaohongshu.com/explore", { waitUntil: "domcontentloaded" });
				const url = page.url();
				const verified = /\/explore\b/.test(url);
				return { content: [{ type: "text", text: JSON.stringify(ok({ url, verified })) }] };
			} catch (e: any) {
				return {
					content: [
						{ type: "text", text: JSON.stringify(err("NAVIGATE_FAILED", String(e?.message || e))) },
					],
				};
			}
		},
	);
}
