/* 中文注释：xhsShortcuts 笔记动作 handler 工厂 */
import * as Pages from "../../services/pages.js";
import { ok } from "../utils/result.js";
import { err } from "../utils/errors.js";
import { screenshotOnError } from "./util.js";

function wrapAction(actionImport: string) {
  return (container: any, manager: any) => async (input: any) => {
    let page: any | undefined;
    try {
      const { dirId, workspaceId } = input as any;
      const context = await manager.getContext(dirId, { workspaceId });
      page = await Pages.ensurePage(context, {});
      const mod = await import("../../domain/xhs/noteActions.js");
      const action = (mod as any)[actionImport];
      const r = await action(page);
      return { content: [{ type: "text", text: JSON.stringify(r.ok ? ok(r) : err("ACTION_FAILED", r.message || `${actionImport} failed`, r)) }] };
    } catch (e: any) {
      const shot = page ? await screenshotOnError(page, String((input as any)?.dirId ?? "unknown"), `${actionImport}-error`) : undefined;
      return { content: [{ type: "text", text: JSON.stringify(err("INTERNAL_ERROR", String(e?.message || e), { screenshotPath: shot })) }] };
    }
  };
}

export const createLikeHandler = wrapAction("likeCurrent");
export const createUnlikeHandler = wrapAction("unlikeCurrent");
export const createCollectHandler = wrapAction("collectCurrent");
export const createUncollectHandler = wrapAction("uncollectCurrent");

// 独立实现：发表评论（需要传入文本参数）
export function createCommentPostHandler(container: any, manager: any) {
  return async (input: any) => {
    let page: any | undefined;
    try {
      const { dirId, workspaceId, text } = input as any;
      const context = await manager.getContext(dirId, { workspaceId });
      page = await Pages.ensurePage(context, {});
      const mod = await import("../../domain/xhs/noteActions.js");
      const r = await (mod as any).commentCurrent(page, String(text || ""));
      return { content: [{ type: "text", text: JSON.stringify(r.ok ? ok(r) : err("ACTION_FAILED", r.message || "commentCurrent failed", r)) }] };
    } catch (e: any) {
      const shot = page ? await screenshotOnError(page, String((input as any)?.dirId ?? "unknown"), `commentCurrent-error`) : undefined;
      return { content: [{ type: "text", text: JSON.stringify(err("INTERNAL_ERROR", String(e?.message || e), { screenshotPath: shot })) }] };
    }
  };
}
