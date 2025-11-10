/* 中文注释：xhsShortcuts 搜索相关 handler 工厂 */
import * as Pages from "../../services/pages.js";
import { ok } from "../utils/result.js";
import { err } from "../utils/errors.js";
import { isNoWsError, noWsErr } from "../utils/noWs.js";
import { XHS_CONF } from "../../config/xhs.js";
import { screenshotOnError } from "./util.js";

export function createSearchKeywordHandler(container: any, manager: any) {
  return async (input: any) => {
    let page: any | undefined;
    try {
      const { dirId, keyword, workspaceId } = input as any;
      const context = await manager.getContext(dirId, { workspaceId });
      page = await Pages.ensurePage(context, {});
      const { searchKeyword } = await import("../../domain/xhs/search.js");
      const r = await searchKeyword(page, String(keyword));
      try {
        if (/\/search_result\?keyword=/.test(page.url())) {
          const sel = "section.note-item, .note-item, .List-item, article, .Card";
          const to = Math.max(200, Number(process.env.XHS_OPEN_WAIT_MS || 1500));
          await page.waitForSelector(sel, { timeout: to });
        }
      } catch {}
      return { content: [{ type: "text", text: JSON.stringify(ok(r)) }] };
    } catch (e: any) {
      const { dirId, workspaceId } = input as any;
      const shot = page ? await screenshotOnError(page, String(dirId ?? "unknown"), "search-keyword-error") : undefined;
      return {
        content: [
          {
            type: "text",
            text: JSON.stringify(
              isNoWsError(e) ? noWsErr(String(dirId), workspaceId) : err("INTERNAL_ERROR", String(e?.message || e), { screenshotPath: shot }),
            ),
          },
        ],
      };
    }
  };
}

