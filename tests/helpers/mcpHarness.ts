/* 中文注释：最小 MCP Server Harness（测试用）
 * 职责：
 * - 提供 registerTool 接口以收集工具 handler
 * - 便于在测试中直接调用 handler 进行端到端验证
 */
import type { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import type { ServiceContainer } from "../../src/core/container.js";
import type { RoxyBrowserManager } from "../../src/services/roxyBrowser.js";
import { registerBrowserTools } from "../../src/mcp/tools/browser.js";
import { registerPageToolsWithPrefix } from "../../src/mcp/tools/page.js";
import { registerXhsShortcutsTools } from "../../src/mcp/tools/xhsShortcuts.js";
import { registerXhsTools } from "../../src/mcp/tools/xhs.js";

type ToolHandler = (input: any) => Promise<{ content: Array<{ type: string; text: string }> }>;

export function createMcpHarness() {
	const handlers = new Map<string, ToolHandler>();
	const server: McpServer = {
		registerTool(name: string, _schema: any, handler: ToolHandler) {
			handlers.set(name, handler);
		},
		// 其他接口对测试不需要，留空实现
	} as unknown as McpServer;

	return {
		server,
		getHandler(name: string) {
			const h = handlers.get(name);
			if (!h) throw new Error(`未注册工具: ${name}`);
			return h;
		},
		registerAll(container: ServiceContainer, manager: RoxyBrowserManager) {
			registerBrowserTools(server, container, manager);
			registerPageToolsWithPrefix(server, container, manager, "page");
			registerXhsShortcutsTools(server, container, manager);
			registerXhsTools(server, container, manager);
		},
		listTools() {
			return Array.from(handlers.keys());
		},
	};
}

