/* 中文注释：xhs 工具 handler 实现（从原 tools/xhs.ts 拆分） */
import * as Pages from "../../services/pages.js";
import { ok } from "../utils/result.js";
import { err } from "../utils/errors.js";
import { isNoWsError, noWsErr } from "../utils/noWs.js";

export function createXhsHandlers(container: any, manager: any) {
  const handlers: Record<string, any> = {};

  handlers["xhs_session_check"] = async (input: any) => {
    try {
      const { dirId, workspaceId } = input as any;
      const context = await manager.getContext(dirId, { workspaceId });
      const { checkSession } = await import("../../domain/xhs/session.js");
      const r = await checkSession(context);
      return { content: [{ type: "text", text: JSON.stringify(ok(r)) }] };
    } catch (e: any) {
      const { dirId, workspaceId } = input as any;
      return { content: [{ type: "text", text: JSON.stringify(isNoWsError(e) ? noWsErr(String(dirId), workspaceId) : err("INTERNAL_ERROR", String(e?.message || e))) }] };
    }
  };

  handlers["xhs_navigate_home"] = async (input: any) => {
    try {
      const { dirId, workspaceId } = input as any;
      const context = await manager.getContext(dirId, { workspaceId });
      const page = await Pages.ensurePage(context, {});
      await page.goto("https://www.xiaohongshu.com/explore", { waitUntil: "domcontentloaded" });
      try {
        const sel = "section.note-item, .note-item, .List-item, article, .Card";
        const to = Math.max(200, Number(process.env.XHS_OPEN_WAIT_MS || 1500));
        await page.waitForSelector(sel, { timeout: to });
      } catch {}
      const url = page.url();
      const verified = /\/explore\b/.test(url);
      return { content: [{ type: "text", text: JSON.stringify(ok({ url, verified })) }] };
    } catch (e: any) {
      const { dirId, workspaceId } = input as any;
      return { content: [{ type: "text", text: JSON.stringify(isNoWsError(e) ? noWsErr(String(dirId), workspaceId) : err("NAVIGATE_FAILED", String(e?.message || e))) }] };
    }
  };

  handlers["xhs_open_context"] = async (input: any) => {
    try {
      const { dirId, workspaceId } = input as any;
      const context = await manager.getContext(dirId, { workspaceId });
      const pages = context.pages();
      const url = pages.length > 0 ? pages[0].url() : undefined;
      return { content: [{ type: "text", text: JSON.stringify(ok({ opened: true, pages: pages.length, url })) }] };
    } catch (e: any) {
      const { dirId, workspaceId } = input as any;
      return { content: [{ type: "text", text: JSON.stringify(isNoWsError(e) ? noWsErr(String(dirId), workspaceId) : err("INTERNAL_ERROR", String(e?.message || e))) }] };
    }
  };

  handlers["xhs_note_extract_content"] = async (input: any) => {
    try {
      const { dirId, workspaceId, noteUrl } = input as any;
      if (!noteUrl || typeof noteUrl !== "string") {
        return { content: [{ type: "text", text: JSON.stringify(err("INVALID_INPUT", "noteUrl 参数缺失或格式错误")) }] };
      }
      const context = await manager.getContext(dirId, { workspaceId });
      const { extractNoteContent } = await import("../../domain/xhs/noteExtractor.js");
      const result = await extractNoteContent(context, noteUrl);
      if ((result as any).ok === false) {
        return { content: [{ type: "text", text: JSON.stringify(err((result as any).code, (result as any).message)) }] };
      }
      return { content: [{ type: "text", text: JSON.stringify(ok(result)) }] };
    } catch (e: any) {
      const { dirId, workspaceId } = input as any;
      return { content: [{ type: "text", text: JSON.stringify(isNoWsError(e) ? noWsErr(String(dirId), workspaceId) : err("INTERNAL_ERROR", String(e?.message || e))) }] };
    }
  };

  return handlers;
}

