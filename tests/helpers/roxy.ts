/* 测试辅助：检查 Roxy 是否可用，用于动态跳过依赖外部服务的集成用例 */
import { ConfigProvider } from "../../src/config/ConfigProvider.js";
import { ServiceContainer } from "../../src/core/container.js";

export async function roxyReady(): Promise<boolean> {
	if (!process.env.ROXY_API_TOKEN) return false;
	try {
		const provider = ConfigProvider.load();
		const container = new ServiceContainer(provider.getConfig());
		const roxy = container.createRoxyClient();
		const ok = await Promise.race([
			roxy
				.health()
				.then(() => true)
				.catch(() => false),
			new Promise<boolean>((resolve) => setTimeout(() => resolve(false), 1500)),
		]);
		if (!ok) {
			await container.cleanup().catch(() => {});
			return false;
		}
		// 简单能力探测：列出 workspace 与窗口，若存在至少一个带 ws 的连接则视为可用
		try {
			const wsList = await roxy.workspaces().catch(() => ({ data: { rows: [] as any[] } }) as any);
			const wsId = wsList?.data?.rows?.[0]?.id as any;
			if (wsId) {
				const win = await roxy
					.listWindows({ workspaceId: wsId })
					.catch(() => ({ data: { rows: [] as any[] } }) as any);
				const ids: string[] = Array.isArray(win?.data?.rows)
					? win.data.rows.map((w: any) => w?.dirId).filter(Boolean)
					: [];
				if (ids.length > 0) {
					const info = await roxy.connectionInfo(ids).catch(() => ({ data: [] as any[] }) as any);
					const anyWs = Array.isArray(info?.data) && info.data.some((x: any) => x?.ws);
					await container.cleanup().catch(() => {});
					return anyWs;
				}
			}
		} catch {}
		await container.cleanup().catch(() => {});
		return true; // 基础健康可用即返回 true
	} catch {
		return false;
	}
}

export async function roxySupportsOpen(): Promise<boolean> {
	// 放宽门控：即便健康检查超时，也尝试从环境与列表路径判定
	try {
		const provider = ConfigProvider.load();
		const container = new ServiceContainer(provider.getConfig());
		const roxy = container.createRoxyClient();

		// 优先路径：使用环境变量中的 dirId 直接探测 ws（更稳定、避免自检窗口干扰）
		try {
			const fromEnv = (process.env.ROXY_DIR_IDS || "")
				.split(",")
				.map((s) => s.trim())
				.filter(Boolean);
			if (fromEnv.length > 0) {
				const info = await roxy.connectionInfo(fromEnv).catch(() => ({ data: [] as any[] }) as any);
				const anyWs = Array.isArray(info?.data) && info.data.some((x: any) => x?.ws);
				await container.cleanup().catch(() => {});
				if (anyWs) return true;
			}
		} catch {}

		// 次级路径：尝试自检开窗 + 关闭
		const dirId = `selftest_${Date.now()}`;
		try {
			const wsId = process.env.ROXY_DEFAULT_WORKSPACE_ID as any;
			const opened = await roxy.ensureOpen(dirId, wsId);
			const ok = !!opened?.ws && typeof opened.ws === "string";
			await roxy.close(dirId).catch(() => {});
			await container.cleanup().catch(() => {});
			return ok;
		} catch {
			// 降级路径：无法新建窗口时，若存在已运行窗口且可获取 ws，则仍视为支持
			try {
				const wsList = await roxy.workspaces().catch(() => ({ data: { rows: [] as any[] } }) as any);
				const wsId = wsList?.data?.rows?.[0]?.id as any;
				let ids: string[] = [];
				if (wsId) {
					const win = await roxy
						.listWindows({ workspaceId: wsId })
						.catch(() => ({ data: { rows: [] as any[] } }) as any);
					ids = Array.isArray(win?.data?.rows)
						? win.data.rows.map((w: any) => w?.dirId).filter(Boolean)
						: [];
				}
				if (ids.length > 0) {
					const info = await roxy.connectionInfo(ids).catch(() => ({ data: [] as any[] }) as any);
					const anyWs = Array.isArray(info?.data) && info.data.some((x: any) => x?.ws);
					await container.cleanup().catch(() => {});
					return anyWs;
				}
				await container.cleanup().catch(() => {});
				return false;
			} catch {
				await container.cleanup().catch(() => {});
				return false;
			}
		}
	} catch {
		return false;
	}
}
