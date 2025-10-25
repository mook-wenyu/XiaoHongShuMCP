/* 中文注释：MCP 服务器（stdio），暴露 roxy.open/close 与 runner.runTask */
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { z } from "zod";
import { ConfigProvider } from "../config/ConfigProvider.js";
import { ServiceContainer } from "../core/container.js";
import { registerActionTools } from "./tools/actions.js";
import { registerRoxyAdminTools } from "./tools/roxyAdmin.js";
import { registerXhsShortcutsTools } from "./tools/xhsShortcuts.js";
import { getParams } from "./utils/params.js";

const OpenParams = z.object({ dirId: z.string(), workspaceId: z.string().optional() });
const CloseParams = z.object({ dirId: z.string() });
// 旧版交互 DSL 已移除，相关入参定义一并清理（保留注释以便追溯）

async function main() {
	// 创建配置和容器
	const configProvider = ConfigProvider.load();
	const config = configProvider.getConfig();
	// MCP 工具入参兼容：统一从 utils/getParams 解析
	// MCP 模式：容器级静默日志，避免污染 stdio 通道
	const container = new ServiceContainer(config, { loggerSilent: true });
	const logger = container.createLogger({ module: "mcp" });

	// 创建服务
	const roxy = container.createRoxyClient();
	const connector = container.createPlaywrightConnector();
	const connectionManager = container.createConnectionManager();
	const policy = container.createPolicyEnforcer();

	// 信号处理 - 使用容器清理
	let shuttingDown = false;
	const onSignal = async (sig: string) => {
		if (shuttingDown) return;
		shuttingDown = true;
		logger.warn({ sig }, "MCP 收到退出信号，清理资源");
		try {
			await container.cleanup();
		} catch (e) {
			logger.error({ err: e }, "清理资源失败");
		}
		process.exit(0);
	};
	process.once("SIGINT", () => { void onSignal("SIGINT"); });
	process.once("SIGTERM", () => { void onSignal("SIGTERM"); });

	const server = new McpServer({
		name: "xhs-mcp",
		version: "0.1.0"
	});

	// 注册工具
	registerActionTools(server, connector, policy);
	registerRoxyAdminTools(server, roxy, policy);
	registerXhsShortcutsTools(server, connector, policy);

	server.registerTool("roxy.openDir", {
		description: "打开 Roxy 窗口（dirId 必填；workspaceId 可选，默认取 ROXY_DEFAULT_WORKSPACE_ID；建立账号上下文=窗口）",
		inputSchema: { dirId: z.string(), workspaceId: z.string().optional() }
	}, async (input: any) => {
		const { dirId, workspaceId } = OpenParams.parse(getParams(input));
		const ws = workspaceId ?? process.env.ROXY_DEFAULT_WORKSPACE_ID;
		const { context } = await connectionManager.get(dirId, { workspaceId: ws });
		return {
			content: [{
				type: "text",
				text: JSON.stringify({ ok: true, dirId, contextPages: context.pages().length })
			}]
		};
	});

	server.registerTool("roxy.closeDir", {
		description: "关闭 Roxy 窗口（dirId 必填）",
		inputSchema: { dirId: z.string() }
	}, async (input: any) => {
		const { dirId } = CloseParams.parse(getParams(input));
		try {
			await connectionManager.close(dirId);
		} catch (e) {
			logger.warn({ dirId, err: e }, "关闭连接失败，忽略");
		}
		await roxy.close(dirId);
		return { content: [{ type: "text", text: JSON.stringify({ ok: true, dirId }) }] };
	});

	// （移除）runner.runTask：不再通过 MCP 层调度多账号任务，改由 CLI 或上层编排实现。


	// xhs.session.check（弱信号，会话态）
	server.registerTool("xhs.session.check", {
		description: "检查小红书登录态（以 cookies/首页加载为弱信号）",
		inputSchema: { dirId: z.string(), workspaceId: z.string().optional() }
	}, async (input: any) => {
		const { dirId, workspaceId } = OpenParams.parse(getParams(input));
		const ws = workspaceId ?? process.env.ROXY_DEFAULT_WORKSPACE_ID;
		const { checkSession } = await import("../domain/xhs/session.js");
		const { context } = await connectionManager.get(dirId, { workspaceId: ws });
		const r = await checkSession(context);
		return { content: [{ type: "text", text: JSON.stringify(r) }] };
	});

	// （移除）tasks.list：不再从 MCP 层暴露任务清单，请参考 README 或 CLI 帮助。

	// server.ping：用于合约测试与 Inspector 探活
	server.registerTool("server.ping", {
		description: "就绪/心跳探活",
		inputSchema: {}
	}, async () => ({
		content: [{ type: "text", text: JSON.stringify({ ok: true, ts: Date.now() }) }]
	}));

	const transport = new StdioServerTransport();
	await server.connect(transport);

	// MCP 模式下不输出日志到 stdout
	// logger.info("MCP 服务器已启动（stdio）");
}

main().catch(async (e) => {
	// MCP 模式下将错误输出到 stderr
	process.stderr.write(`MCP 服务器启动失败: ${e}\n`);
	process.exit(1);
});
