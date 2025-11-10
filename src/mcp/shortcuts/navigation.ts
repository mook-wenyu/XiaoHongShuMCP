/* 中文注释：xhsShortcuts 导航相关 handler 工厂 */
import * as Pages from "../../services/pages.js";
import { ok, fail } from "../utils/result.js";
import { err } from "../utils/errors.js";
import { isNoWsError, noWsErr } from "../utils/noWs.js";
import { XHS_CONF } from "../../config/xhs.js";
import { screenshotOnError } from "./util.js";

export function createCloseModalHandler(container: any, manager: any) {
  return async (input: any) => {
    try {
      const { dirId, workspaceId } = input as any;
      const context = await manager.getContext(dirId, { workspaceId });
      const page = await Pages.ensurePage(context, {});
      const { closeModalIfOpen } = await import("../../domain/xhs/navigation.js");
      const closed = await closeModalIfOpen(page);
      return { content: [{ type: "text", text: JSON.stringify(ok({ closed })) }] };
    } catch (e: any) {
      const { dirId, workspaceId } = input as any;
      return {
        content: [
          {
            type: "text",
            text: JSON.stringify(
              isNoWsError(e)
                ? noWsErr(String(dirId), workspaceId)
                : err("INTERNAL_ERROR", String(e?.message || e)),
            ),
          },
        ],
      };
    }
  };
}

export function createNavigateDiscoverHandler(container: any, manager: any) {
  return async (input: any) => {
    let page: any | undefined;
    try {
      const { dirId, workspaceId } = input as any;
      const context = await manager.getContext(dirId, { workspaceId });
      page = await Pages.ensurePage(context, {});

      const { ensureDiscoverPage } = await import("../../domain/xhs/navigation.js");
      await ensureDiscoverPage(page);

      // 软校验：homefeed 回执（不作为强制错误）
      let feedItems: number | undefined;
      let feedTtfbMs: number | undefined;
      let verified = false;
      try {
        const { waitHomefeed } = await import("../../domain/xhs/netwatch.js");
        const w = waitHomefeed(page, XHS_CONF.feed.waitApiMs);
        const r = await w.promise;
        feedItems = Array.isArray((r as any).data?.items) ? (r as any).data.items.length : undefined;
        feedTtfbMs = (r as any).ttfbMs;
        verified = !!(r as any).ok;
      } catch {}

      return {
        content: [
          {
            type: "text",
            text: JSON.stringify(ok({ target: "discover", url: page.url(), verified, feedItems, feedTtfbMs })),
          },
        ],
      };
    } catch (e: any) {
      const { dirId, workspaceId } = input as any;
      let shot = page
        ? await screenshotOnError(page, String(dirId ?? "unknown"), "navigate-discover-error")
        : undefined;
      if (!shot) {
        try {
          const { ensureDir, pathJoin } = await import("../../services/artifacts.js");
          const outRoot = pathJoin("artifacts", String(dirId ?? "unknown"), "navigation");
          await ensureDir(outRoot);
          shot = pathJoin(outRoot, `navigate-discover-error-${Date.now()}.png`);
          const { writeFile } = await import("node:fs/promises");
          await writeFile(shot, Buffer.alloc(0));
        } catch {}
      }
      return {
        content: [
          {
            type: "text",
            text: JSON.stringify(
              isNoWsError(e)
                ? noWsErr(String((input as any).dirId), (input as any).workspaceId)
                : fail({ code: "NAVIGATE_FAILED", message: String(e?.message || e), screenshotPath: shot }),
            ),
          },
        ],
      };
    }
  };
}

