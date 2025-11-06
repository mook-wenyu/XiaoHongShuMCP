/* 中文：打印指定 dirId 的连接信息（ws/http） */
import { ConfigProvider } from "../src/config/ConfigProvider.js";
import { ServiceContainer } from "../src/core/container.js";

function getArg(name: string) {
	const key = `--${name}=`;
	const arg = process.argv.find((a) => a.startsWith(key));
	return arg ? arg.slice(key.length) : undefined;
}

(async () => {
	const dirId = getArg("dirId");
	if (!dirId) {
		console.error(JSON.stringify({ ok: false, error: "dirId is required" }));
		process.exit(1);
	}
	const provider = ConfigProvider.load();
	const cfg = provider.getConfig();
	const container = new ServiceContainer(cfg);
	const roxy = container.createRoxyClient();
	const res = await roxy.connectionInfo([dirId]);
	console.error(JSON.stringify(res, null, 2));
	await container.cleanup();
})();
