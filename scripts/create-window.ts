/* 中文：创建 Roxy 窗口并输出 dirId */
import { ConfigProvider } from "../src/config/ConfigProvider.js";
import { ServiceContainer } from "../src/core/container.js";

function getArg(name: string, def?: string) {
	const entries = process.argv.filter((a) => a.startsWith(`--${name}=`));
	const found = entries.length > 0 ? entries[entries.length - 1] : undefined;
	return found ? found.split("=")[1] : def;
}

(async () => {
	const provider = ConfigProvider.load();
	const cfg = provider.getConfig();
	const container = new ServiceContainer(cfg);
	const roxy = container.createRoxyClient();
	const workspaceId = getArg("workspaceId");
	if (!workspaceId) {
		console.error(JSON.stringify({ ok: false, error: "workspaceId is required" }));
		process.exit(1);
	}

	const windowName = `codex-xhs-test-${Date.now()}`;
	const res = await roxy.createWindow({ workspaceId: Number(workspaceId), windowName });
	const dirId = res.data?.dirId;
	console.error(JSON.stringify({ ok: true, dirId, windowName }));
	await container.cleanup();
})();
