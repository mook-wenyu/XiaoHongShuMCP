/* 中文说明：小红书快捷工具（官方风格命名前缀 xhs.*），包含会话检查、导航与内容提取 */
import type { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import type { ServiceContainer } from "../../core/container.js";
import type { RoxyBrowserManager } from "../../services/roxyBrowser.js";
import { createXhsHandlers } from "../xhs/handlers.js";
import { z } from "zod";

export function registerXhsTools(
	server: McpServer,
	container: ServiceContainer,
	manager: RoxyBrowserManager,
) {
  const handlers = createXhsHandlers(container, manager);
  // 轻量输入约束（确保 SDK 解析为扁平参数对象）
  const DirId = z.string().min(1);
  const WorkspaceId = z.string().optional();
  const NoteUrl = z.string().min(1); // 不强行校验 URL 形状，避免误判导出链接

  const schemas: Record<string, any> = {
    xhs_session_check: { dirId: DirId, workspaceId: WorkspaceId },
    xhs_navigate_home: { dirId: DirId, workspaceId: WorkspaceId },
    xhs_open_context: { dirId: DirId, workspaceId: WorkspaceId },
    xhs_note_extract_content: { dirId: DirId, workspaceId: WorkspaceId, noteUrl: NoteUrl },
  };

  for (const [tool, handler] of Object.entries(handlers)) {
    const inputSchema = schemas[tool] ?? ({} as any);
    server.registerTool(tool, { description: `xhs tool: ${tool}`, inputSchema }, handler as any);
  }
}
