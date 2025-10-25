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
      roxy.health().then(() => true).catch(() => false),
      new Promise<boolean>((resolve) => setTimeout(() => resolve(false), 1500))
    ]);
    if (!ok) { await container.cleanup().catch(() => {}); return false; }
    // 简单能力探测：列出 workspace 与窗口，若存在至少一个带 ws 的连接则视为可用
    try {
      const wsList = await roxy.workspaces().catch(() => ({ data: { rows: [] as any[] } } as any));
      const wsId = (wsList?.data?.rows?.[0]?.id) as any;
      if (wsId) {
        const win = await roxy.listWindows({ workspaceId: wsId }).catch(() => ({ data: { rows: [] as any[] } } as any));
        const ids: string[] = Array.isArray(win?.data?.rows) ? win.data.rows.map((w: any) => w?.dirId).filter(Boolean) : [];
        if (ids.length > 0) {
          const info = await roxy.connectionInfo(ids).catch(() => ({ data: [] as any[] } as any));
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
  if (!await roxyReady()) return false;
  try {
    const provider = ConfigProvider.load();
    const container = new ServiceContainer(provider.getConfig());
    const roxy = container.createRoxyClient();
    const dirId = `selftest_${Date.now()}`;
    try {
      const opened = await roxy.ensureOpen(dirId);
      const ok = !!opened?.ws && typeof opened.ws === 'string';
      await roxy.close(dirId).catch(() => {});
      await container.cleanup().catch(() => {});
      return ok;
    } catch {
      await container.cleanup().catch(() => {});
      return false;
    }
  } catch {
    return false;
  }
}
