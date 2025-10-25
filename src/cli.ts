/* 中文注释：自检 CLI（按新约：多窗口=多上下文，一个账号=一个Context） */
import { ConfigProvider } from "./config/ConfigProvider.js";
import { ServiceContainer } from "./core/container.js";
import { parseArg, parseFlag, parsePayload, parseDirIds } from "./utils/cliParser.js";
import { runDirIds } from "./runner/multiAccountRunner.js";
import { runTaskByName, listTasks } from "./tasks/registry.js";

(async () => {
	// 创建配置和容器
	const configProvider = ConfigProvider.load();
	const config = configProvider.getConfig();
	const container = new ServiceContainer(config);
	const logger = container.createLogger({ module: "cli" });

	// 列出任务
	if (parseFlag("list-tasks", process.argv)) {
		console.log(JSON.stringify(listTasks(), null, 2));
		return;
	}

	try {
		// 解析参数
		const taskName = parseArg("task", process.argv, "openAndScreenshot")!;
		const payloadRaw = parseArg("payload", process.argv, "{}");
		let payload: any = await parsePayload(payloadRaw!);
		if (!payload.url) payload.url = config.DEFAULT_URL;

		// 解析 dirIds
		let dirIds = parseDirIds(process.argv);
		if (dirIds.length === 0) {
			throw new Error("未提供任何 dirId（使用 --dir-ids=a,b 或 --dirId=a --dirId=b）");
		}
		const limit = Number(parseArg("limit", process.argv, String(dirIds.length))) || dirIds.length;
		dirIds = dirIds.slice(0, Math.min(limit, dirIds.length));

		const workspaceId = parseArg("workspace-id", process.argv) || parseArg("workspaceId", process.argv) || process.env.ROXY_DEFAULT_WORKSPACE_ID;

		// 创建服务
		const roxy = container.createRoxyClient();
		await roxy.health();
		const connector = container.createPlaywrightConnector();
		const policy = container.createPolicyEnforcer();
		const task = runTaskByName(taskName);

		// 信号处理 - 使用容器清理
		let shuttingDown = false;
		const onSignal = async (sig: string) => {
			if (shuttingDown) return;
			shuttingDown = true;
			logger.warn({ sig }, "收到退出信号，清理资源");
			try {
				await Promise.allSettled(dirIds.map((id) => roxy.close(id)));
				await container.cleanup();
			} catch (e) {
				logger.error({ err: e }, "清理资源失败");
			}
			process.exit(130);
		};
		process.once("SIGINT", () => { void onSignal("SIGINT"); });
		process.once("SIGTERM", () => { void onSignal("SIGTERM"); });

		// 运行任务
		const res = await runDirIds(
			dirIds,
			connector,
			async (ctx, id) => task(ctx, id, payload),
			{
				concurrency: config.MAX_CONCURRENCY,
				timeoutMs: config.TIMEOUT_MS,
				policy,
				taskName,
				openOptions: workspaceId ? { workspaceId } : undefined
			}
		);

		logger.info({ ...res, total: dirIds.length }, "运行完成");
		if (res.failed.length > 0) process.exitCode = 1;
	} catch (err) {
		logger.error({ err }, "CLI 执行失败");
		process.exitCode = 1;
	}
})();
