// 离线枚举工具：避免依赖实际 MCP 启动（无官方桥包时也可运行）
// 通过构建一个最小 FakeServer，调用各工具注册器收集名称

import { registerBrowserTools } from "../src/mcp/tools/browser.js";
import { registerPageTools } from "../src/mcp/tools/page.js";
import { registerXhsTools } from "../src/mcp/tools/xhs.js";
import { registerXhsShortcutsTools } from "../src/mcp/tools/xhsShortcuts.js";
import { registerResourceTools } from "../src/mcp/tools/resources.js";
import { registerRoxyAdminTools } from "../src/mcp/tools/roxyAdmin.js";

class FakeServer {
	public tools: { name: string; description?: string }[] = [];
	registerTool(name: string, meta: any, _handler: any) {
		this.tools.push({ name, description: meta?.description });
	}
	// 兼容接口占位（未使用）
	registerResource() {}
}

async function main() {
	const server = new FakeServer();
	const container: any = {};
	const adapter: any = {};
	const roxy: any = {};
	const policy: any = {};

	// 官方命名（改为下划线风格，避免部分客户端点号解析问题）
	registerBrowserTools(server as any, container, adapter);
	registerPageTools(server as any, container, adapter);
	registerXhsTools(server as any, container, adapter);
	registerXhsShortcutsTools(server as any, container, adapter);
	registerResourceTools(server as any, container, adapter);
	// 高权限管理工具
	registerRoxyAdminTools(server as any, roxy, policy);

	const expected = [
		// 浏览器 / 页面
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
		// XHS 基线
		"xhs_session_check",
		"xhs_navigate_home",
		// XHS 扩展（快捷语义动作）
		"xhs_close_modal",
		"xhs_navigate_discover",
		"xhs_search_keyword",
		"xhs_keyword_browse",
		"xhs_select_note",
		"xhs_note_like",
		"xhs_note_unlike",
		"xhs_note_collect",
		"xhs_note_uncollect",
		"xhs_user_follow",
		"xhs_user_unfollow",
		"xhs_comment_post",
		// 资源工具
		"resources_listArtifacts",
		"resources_readArtifact",
		"page_snapshot",
		// 管理工具
		"roxy_workspaces_list",
		"roxy_windows_list",
		"roxy_window_create",
	];
	const optional = ["server_ping", "server_capabilities"];

	const names = server.tools.map((t) => t.name);
	const missing = expected.filter((n) => !names.includes(n));
	const extras = names.filter((n) => !expected.includes(n) && !optional.includes(n));

	if (missing.length === 0 && extras.length === 0) {
		console.log(
			JSON.stringify({ ok: true, total: names.length, note: "tools surface matches expected" }),
		);
		process.exit(0);
	} else {
		console.log(JSON.stringify({ ok: false, total: names.length, missing, extras }));
		process.exit(1);
	}
}
main().catch((e) => {
	console.error("check-mcp-tools failed:", e);
	process.exit(2);
});
