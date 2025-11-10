/* 中文说明：小红书快捷工具（官方风格命名前缀 xhs.*），包含会话检查、导航与内容提取 */
import type { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import type { ServiceContainer } from "../../core/container.js";
import type { RoxyBrowserManager } from "../../services/roxyBrowser.js";
import { createXhsHandlers } from "../xhs/handlers.js";

export function registerXhsTools(
	server: McpServer,
	container: ServiceContainer,
	manager: RoxyBrowserManager,
) {
  const handlers = createXhsHandlers(container, manager);
  for (const [tool, handler] of Object.entries(handlers)) {
    server.registerTool(tool, { description: `xhs tool: ${tool}`, inputSchema: {} as any }, handler as any);
  }
}
