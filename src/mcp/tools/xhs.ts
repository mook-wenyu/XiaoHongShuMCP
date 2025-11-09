/* 中文说明：小红书快捷工具（官方风格命名前缀 xhs.*），包含会话检查、导航与内容提取 */
import { z } from "zod";
import type { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import type { ServiceContainer } from "../../core/container.js";
import type { RoxyBrowserManager } from "../../services/roxyBrowser.js";
import { ok } from "../utils/result.js";
import { err } from "../utils/errors.js";
import * as Pages from "../../services/pages.js";
import { extractNoteContent } from "../../domain/xhs/noteExtractor.js";

const DirId = z.string().min(1);
const WorkspaceId = z.string().optional();
const NoteUrl = z.string().url();

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
				// 跳转后等待首屏列表注水，避免后续出现空批（不引入新开关，使用既有超时）
				try {
					const sel = "section.note-item, .note-item, .List-item, article, .Card";
					const to = Math.max(200, Number(process.env.XHS_OPEN_WAIT_MS || 1500));
					await page.waitForSelector(sel, { timeout: to });
				} catch {}
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

	// xhs_open_context —— 确保 RoxyBrowser 上下文就绪（诊断/编排辅助）
	server.registerTool(
		"xhs_open_context",
		{
			description: "确保浏览器上下文已打开，返回页面数量与首个页面 URL（若存在）",
			inputSchema: { dirId: DirId, workspaceId: WorkspaceId },
		},
		async (input: any) => {
			try {
				const { dirId, workspaceId } = input as any;
				const context = await manager.getContext(dirId, { workspaceId });
				const pages = context.pages();
				const url = pages.length > 0 ? pages[0].url() : undefined;
				return {
					content: [
						{ type: "text", text: JSON.stringify(ok({ opened: true, pages: pages.length, url })) },
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

	// xhs_note_extract_content —— 提取笔记完整内容（标题、正文、标签、互动数据）
	server.registerTool(
		"xhs_note_extract_content",
		{
			description:
				"提取小红书笔记的完整内容（标题、正文、标签、作者、互动数据），用于数据分析和质量评分",
			inputSchema: {
				dirId: DirId,
				workspaceId: WorkspaceId,
				noteUrl: NoteUrl,
			},
		},
		async (input: any) => {
			try {
				const { dirId, workspaceId, noteUrl } = input as any;

				// 验证输入参数
				if (!noteUrl || typeof noteUrl !== "string") {
					return {
						content: [
							{
								type: "text",
								text: JSON.stringify(err("INVALID_INPUT", "noteUrl 参数缺失或格式错误")),
							},
						],
					};
				}

				// 获取浏览器上下文
				const context = await manager.getContext(dirId, { workspaceId });

				// 执行内容提取
				const result = await extractNoteContent(context, noteUrl);

				// 检查是否提取失败
				if ("ok" in result && result.ok === false) {
					return {
						content: [
							{
								type: "text",
								text: JSON.stringify(err(result.code, result.message)),
							},
						],
					};
				}

				// 返回成功结果
				return {
					content: [{ type: "text", text: JSON.stringify(ok(result)) }],
				};
			} catch (e: any) {
				return {
					content: [
						{
							type: "text",
							text: JSON.stringify(err("INTERNAL_ERROR", String(e?.message || e))),
						},
					],
				};
			}
		},
	);
}
