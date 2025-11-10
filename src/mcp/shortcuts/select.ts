/* 中文注释：xhsShortcuts 选择笔记 handler 工厂 */
import * as Pages from "../../services/pages.js";
import { ok } from "../utils/result.js";
import { err } from "../utils/errors.js";
import { screenshotOnError } from "./util.js";

export function createSelectNoteHandler(container: any, manager: any) {
  return async (input: any) => {
    let page: any | undefined;
    try {
      const { dirId, keywords, workspaceId } = input as any;
      const keys: string[] = (Array.isArray(keywords) ? keywords : [keywords])
        .map((s: any) => String(s || "").trim())
        .filter(Boolean);
      if (!keys.length)
        return { content: [{ type: "text", text: JSON.stringify(err("INVALID_PARAMS", "keywords 不能为空")) }] };

      const context = await manager.getContext(dirId, { workspaceId });
      page = await Pages.ensurePage(context, {});
      const { detectPageType, ensureDiscoverPage, findAndOpenNoteByKeywords, PageType } =
        await import("../../domain/xhs/navigation.js");
      let pType = await detectPageType(page);
      const allowed = [PageType.ExploreHome, PageType.Discover, PageType.Search];
      if (!allowed.includes(pType)) {
        await ensureDiscoverPage(page);
        pType = await detectPageType(page);
      }
      const preferApiAnchors = pType === PageType.Search;
      const tSelect0 = Date.now();
      const r = await findAndOpenNoteByKeywords(page, keys, { preferApiAnchors, useApiAfterScroll: true });
      const selectTimeMs = Date.now() - tSelect0;
      return {
        content: [{ type: "text", text: JSON.stringify(ok({
          opened: !!(r.modalOpen || (r as any).urlOpened), openedPath: (r as any).openedPath,
          matched: r.matched, url: page.url(), pageType: pType,
          metrics: { select: { timeMs: selectTimeMs }, feed: { verified: r.feedVerified, items: r.feedItems, type: r.feedType, ttfbMs: r.feedTtfbMs } },
        })) }],
      };
    } catch (e: any) {
      const { dirId } = input as any;
      const shot = page ? await screenshotOnError(page, String(dirId ?? "unknown"), "select-note-error") : undefined;
      return { content: [{ type: "text", text: JSON.stringify(err("INTERNAL_ERROR", String(e?.message || e), { screenshotPath: shot })) }] };
    }
  };
}

