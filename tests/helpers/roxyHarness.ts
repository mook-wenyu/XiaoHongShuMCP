/* 中文注释：Roxy 集成测试辅助
 * - 提供获取 ServiceContainer 与 RoxyBrowserManager 的便捷方法
 * - 基于环境变量或 ensureOpen 选择 dirId
 */
import { ConfigProvider } from "../../src/config/ConfigProvider.js";
import { ServiceContainer } from "../../src/core/container.js";
import type { RoxyBrowserManager } from "../../src/services/roxyBrowser.js";
import { roxyReady } from "./roxy.js";

export async function getContainerAndManager(): Promise<{
	container: ServiceContainer;
	manager: RoxyBrowserManager;
}> {
	const provider = ConfigProvider.load();
	const container = new ServiceContainer(provider.getConfig());
	const manager = container.createRoxyBrowserManager();
	return { container, manager } as any;
}

export async function pickDirId(fallbackPrefix = "e2e"): Promise<string> {
	// 优先使用环境变量中的可用 dirId
	const fromEnv = (process.env.ROXY_DIR_IDS || "")
		.split(",")
		.map((s) => s.trim())
		.filter(Boolean);
	if (fromEnv.length > 0) return fromEnv[0];

	// 否则尝试自检 + ensureOpen（若服务端拒绝开窗，后续测试应 describe.skip）
	if (!(await roxyReady())) return `${fallbackPrefix}_${Date.now()}`;
	return `${fallbackPrefix}_${Date.now()}`;
}

