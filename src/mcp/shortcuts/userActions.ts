/* 中文注释：xhsShortcuts 用户动作 handler 工厂 */
import * as Pages from "../../services/pages.js";
import { ok } from "../utils/result.js";
import { err } from "../utils/errors.js";
import { screenshotOnError } from "./util.js";

function wrapUser(actionImport: string) {
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

export const createFollowHandler = wrapUser("followAuthor");
export const createUnfollowHandler = wrapUser("unfollowAuthor");

