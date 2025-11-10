/* 中文注释：xhsShortcuts 浏览（关键字滚动） handler 工厂 */
import * as Pages from "../../services/pages.js";
import { ok } from "../utils/result.js";
import { err } from "../utils/errors.js";
import { XHS_CONF } from "../../config/xhs.js";
import { scrollHuman } from "../../humanization/actions.js";
import { screenshotOnError } from "./util.js";

export function createKeywordBrowseHandler(container: any, manager: any) {
  return async (input: any) => {
    let page: any | undefined;
    try {
      const { dirId, keywords, workspaceId } = input as any;
      const context = await manager.getContext(dirId, { workspaceId });
      page = await Pages.ensurePage(context, {});
      const { searchKeyword } = await import("../../domain/xhs/search.js");
      const key = (Array.isArray(keywords) ? keywords : [keywords])
        .map((s) => String(s || "").trim())
        .filter(Boolean)
        .join(" ");
      const t0 = Date.now();
      const r = await searchKeyword(page, key);
      try {
        if (/\/search_result\?keyword=/.test(page.url())) {
          const sel = "section.note-item, .note-item, .List-item, article, .Card";
          const to = Math.max(200, Number(process.env.XHS_OPEN_WAIT_MS || 1500));
          await page.waitForSelector(sel, { timeout: to });
        }
      } catch {}
      const searchTimeMs = Date.now() - t0;
      const tBrowse0 = Date.now();
      try {
        await scrollHuman(page, XHS_CONF.scroll.step);
        await page.waitForTimeout(XHS_CONF.scroll.shortSearchWaitMs);
        await scrollHuman(page, Math.floor(XHS_CONF.scroll.step * 0.8));
      } catch {}
      const browseTimeMs = Date.now() - tBrowse0;
      return {
        content: [{
          type: "text",
          text: JSON.stringify(ok({
            ...r,
            browsed: true,
            metrics: {
              search: { timeMs: searchTimeMs, verified: !!(r as any).verified, matchedCount: (r as any).matchedCount },
              browse: { steps: 2, timeMs: browseTimeMs },
              url: page.url(),
            },
          })),
        }],
      };
    } catch (e: any) {
      const { dirId } = input as any;
      const shot = page ? await screenshotOnError(page, String(dirId ?? "unknown"), "keyword-browse-error") : undefined;
      return { content: [{ type: "text", text: JSON.stringify(err("INTERNAL_ERROR", String(e?.message || e), { screenshotPath: shot })) }] };
    }
  };
}

