/* 中文注释：页面工具（薄层注册，委派至 src/mcp/page/handlers.ts） */
import type { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import type { ServiceContainer } from "../../core/container.js";
import type { RoxyBrowserManager } from "../../services/roxyBrowser.js";
import { createPageHandlers } from "../page/handlers.js";

export function registerPageToolsWithPrefix(
  server: McpServer,
  container: ServiceContainer,
  manager: RoxyBrowserManager,
  prefix = "page",
) {
  const handlers = createPageHandlers(container, manager, prefix);
  for (const [tool, handler] of Object.entries(handlers)) {
    server.registerTool(tool, { description: `page tool: ${tool}`, inputSchema: {} as any }, handler as any);
  }
}

export function registerPageTools(
  server: McpServer,
  container: ServiceContainer,
  manager: RoxyBrowserManager,
) {
  return registerPageToolsWithPrefix(server, container, manager, "page");
}

