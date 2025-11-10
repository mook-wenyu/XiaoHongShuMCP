/* 中文注释：xhsShortcuts 工具通用辅助 */
import type { ServiceContainer } from "../../core/container.js";

// 统一错误截图：失败时写入空文件占位，保证返回路径可验证
export async function screenshotOnError(page: any, dirId: string, tag: string) {
  try {
    const { ensureDir, pathJoin } = await import("../../services/artifacts.js");
    const outRoot = pathJoin("artifacts", dirId, "navigation");
    await ensureDir(outRoot);
    const path = pathJoin(outRoot, `${tag}-${Date.now()}.png`);
    try {
      await page.screenshot({ path, fullPage: true });
    } catch {
      try {
        const { writeFile } = await import("node:fs/promises");
        await writeFile(path, Buffer.alloc(0));
      } catch {}
    }
    return path;
  } catch {
    return undefined;
  }
}

