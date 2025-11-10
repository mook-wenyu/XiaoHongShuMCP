/* 中文注释：xhsShortcuts 收集搜索结果 handler 工厂 */
import * as Pages from "../../services/pages.js";
import { ok } from "../utils/result.js";
import { err } from "../utils/errors.js";
import { XHS_CONF } from "../../config/xhs.js";
import { screenshotOnError } from "./util.js";

export function createCollectSearchResultsHandler(container: any, manager: any) {
  return async (input: any) => {
    let page: any | undefined;
    try {
      const { dirId, keyword, limit, workspaceId } = input as any;
      const maxN = Math.max(1, Math.min(Number(limit || 10), 100));
      const context = await manager.getContext(dirId, { workspaceId });
      page = await Pages.ensurePage(context, {});
      const { searchKeyword } = await import("../../domain/xhs/search.js");
      const { waitSearchNotes } = await import("../../domain/xhs/netwatch.js");
      const w = waitSearchNotes(page, (XHS_CONF?.search?.waitApiMs as number) || 6000);
      const res = await searchKeyword(page as any, String(keyword || ""));
      // 若页面本身未触发搜索 API（synthetic 场景常见），主动刺激一次 fetch 以命中路由拦截
      try {
        await page.evaluate((kw: string) => {
          try { fetch(`/api/sns/web/v1/search/notes?keyword=${encodeURIComponent(String(kw||""))}`); } catch {}
        }, String(keyword || ""));
      } catch {}
      let apiItems: Array<{ id?: string; note_card?: { display_title?: string } }> = [];
      try {
        const r = await w.promise;
        apiItems = Array.isArray((r as any)?.data?.items) ? (r as any).data.items : [];
      } catch {}
      const items: Array<{ noteId: string; url: string; title: string }> = [];
      const dedup = new Map<string, boolean>();
      const push = (id?: string, title?: string) => {
        const nid = String(id || "").trim();
        if (!nid || dedup.has(nid)) return;
        dedup.set(nid, true);
        const url = `https://www.xiaohongshu.com/explore/${nid}`;
        items.push({ noteId: nid, url, title: String(title || "") });
      };
      for (const it of apiItems) {
        if (items.length >= maxN) break;
        push(it?.id, it?.note_card?.display_title);
      }
      if (items.length < maxN) {
        try {
          const anchors = await page.evaluate(() => {
            const out: Array<{ id: string; title: string; href?: string }> = [];
            const sel = 'a[href*="/explore/"]';
            document.querySelectorAll(sel).forEach((a) => {
              const href = a.getAttribute('href') || '';
              const m = href.match(/\/explore\/([A-Za-z0-9]+)/);
              if (m) {
                const id = m[1];
                const title = (a.textContent || '').trim();
                out.push({ id, title, href });
              }
            });
            return out.slice(0, 120);
          });
          for (const a of anchors) {
            if (items.length >= maxN) break;
            push(a.id, a.title);
          }
        } catch {}
      }
      const diagnostics = { url: page.url(), verified: !!(res as any)?.verified, apiCount: apiItems.length || 0, total: items.length };
      return { content: [{ type: "text", text: JSON.stringify(ok({ items, diagnostics })) }] };
    } catch (e: any) {
      const { dirId } = input as any;
      const shot = page ? await screenshotOnError(page, String(dirId ?? "unknown"), "collect-search-results-error") : undefined;
      return { content: [{ type: "text", text: JSON.stringify(err("INTERNAL_ERROR", String(e?.message || e), { screenshotPath: shot })) }] };
    }
  };
}
