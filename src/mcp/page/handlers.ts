/* 中文注释：page 工具 handler 实现（从原 tools/page.ts 拆分，便于测试与覆盖） */
import type { ServiceContainer } from "../../core/container.js";
import type { RoxyBrowserManager } from "../../services/roxyBrowser.js";
import * as Pages from "../../services/pages.js";
import { ok, fail } from "../utils/result.js";
import { err, mapError } from "../utils/errors.js";
import { resolveLocatorAsync } from "../../selectors/index.js";
import { logHumanTrace } from "../../humanization/trace.js";
import { clickHuman, hoverHuman, scrollHuman, moveMouseTo, typeHuman, clearInputHuman } from "../../humanization/actions.js";
import { buildMouseMoveOptions, buildScrollOptions } from "../../humanization/options.js";
import { getProfile } from "../../humanization/profiles.js";
import { isNoWsError, noWsErr } from "../utils/noWs.js";

export function createPageHandlers(container: ServiceContainer, manager: RoxyBrowserManager, prefix = "page") {
  const name = (n: string) => `${prefix}_${n.replace(/\./g, "_")}`;

  async function withPage(dirId: string, pageIndex: number | undefined, workspaceId: string | undefined) {
    const context = await manager.getContext(dirId, { workspaceId });
    const page = await Pages.ensurePage(context, { pageIndex });
    return { context, page };
  }

  const handlers: Record<string, any> = {};

  handlers[name("create")] = async (input: any) => {
    try {
      const { dirId, url, workspaceId } = input as any;
      const r = await manager.createPage(dirId, url, { workspaceId });
      return { content: [{ type: "text", text: JSON.stringify(ok(r)) }] };
    } catch (e: any) {
      const { dirId, workspaceId } = input as any;
      return { content: [{ type: "text", text: JSON.stringify(isNoWsError(e) ? noWsErr(String(dirId), workspaceId) : err("INTERNAL_ERROR", String(e?.message || e))) }] };
    }
  };

  handlers[name("list")] = async (input: any) => {
    try {
      const { dirId, workspaceId } = input as any;
      const r = await manager.listPages(dirId, { workspaceId });
      return { content: [{ type: "text", text: JSON.stringify({ ok: true, data: r }) }] };
    } catch (e: any) {
      const { dirId, workspaceId } = input as any;
      return { content: [{ type: "text", text: JSON.stringify(isNoWsError(e) ? noWsErr(String(dirId), workspaceId) : err("INTERNAL_ERROR", String(e?.message || e))) }] };
    }
  };

  handlers[name("close")] = async (input: any) => {
    try {
      const { dirId, pageIndex, workspaceId } = input as any;
      const r = await manager.closePage(dirId, pageIndex, { workspaceId });
      return { content: [{ type: "text", text: JSON.stringify(ok(r)) }] };
    } catch (e: any) {
      const { dirId, workspaceId } = input as any;
      return { content: [{ type: "text", text: JSON.stringify(isNoWsError(e) ? noWsErr(String(dirId), workspaceId) : err("INTERNAL_ERROR", String(e?.message || e))) }] };
    }
  };

  handlers[name("navigate")] = async (input: any) => {
    const { dirId, url, pageIndex, workspaceId } = input as any;
    try {
      const r = await manager.navigate(dirId, url, pageIndex, { workspaceId });
      return { content: [{ type: "text", text: JSON.stringify(ok(r)) }] };
    } catch (e: any) {
      return { content: [{ type: "text", text: JSON.stringify(isNoWsError(e) ? noWsErr(String(dirId), workspaceId) : mapError(e, "navigate", { dirId, url })) }] };
    }
  };

  handlers[name("click")] = async (input: any) => {
    const { dirId, target, pageIndex, workspaceId, human } = input as any;
    const enableHuman = human !== false && !(human && typeof human === "object" && human.enabled === false);
    const humanObj = typeof human === "object" ? human : (human === true ? { profile: "rapid" } : undefined);
    const selectorId = (target && (target.id || target.text || target.role || target.selector)) || "anonymous";
    try {
      const { page } = await withPage(dirId, pageIndex, workspaceId);
      let loc;
      try {
        loc = await resolveLocatorAsync(page, target || ({} as any));
      } catch (e: any) {
        const m = mapError(e, "locator", { dirId, selectorId, target });
        return { content: [{ type: "text", text: JSON.stringify(fail({ code: m.code, message: m.message || String(e?.message || e), ...(m.data || {}) })) }] };
      }
      try {
        const waitMs = Math.max(200, Number(process.env.MCP_LOCATE_TIMEOUT_MS || 2000));
        await loc.first().waitFor({ state: "attached", timeout: waitMs });
        if (enableHuman) {
          const moveOpts = buildMouseMoveOptions(humanObj, container);
          await moveMouseTo(page, loc.first(), moveOpts as any);
          await logHumanTrace(dirId, "click.move", { selectorId, moveOpts });
          await clickHuman(page, loc.first());
          await logHumanTrace(dirId, "click.click", { selectorId });
        } else {
          await loc.first().click({ timeout: waitMs });
        }
        return { content: [{ type: "text", text: JSON.stringify(ok({ selectorId })) }] };
      } catch (e: any) {
        const m = mapError(e, "action", { dirId, selectorId, target });
        return { content: [{ type: "text", text: JSON.stringify(fail({ code: m.code, message: m.message || String(e?.message || e), ...(m.data || {}) })) }] };
      }
    } catch (e: any) {
      return { content: [{ type: "text", text: JSON.stringify(err("INTERNAL_ERROR", String(e?.message || e))) }] };
    }
  };

  handlers[name("hover")] = async (input: any) => {
    const { dirId, target, pageIndex, workspaceId, human } = input as any;
    const enableHuman = human !== false && !(human && typeof human === "object" && human.enabled === false);
    const humanObj = typeof human === "object" ? human : (human === true ? { profile: "rapid" } : undefined);
    const selectorId = (target && (target.id || target.text || target.role || target.selector)) || "anonymous";
    try {
      const { page } = await withPage(dirId, pageIndex, workspaceId);
      let loc;
      try {
        loc = await resolveLocatorAsync(page, target || ({} as any));
      } catch (e: any) {
        const m = mapError(e, "locator", { dirId, selectorId, target });
        return { content: [{ type: "text", text: JSON.stringify(fail({ code: m.code, message: m.message || String(e?.message || e), ...(m.data || {}) })) }] };
      }
      try {
        const waitMs = Math.max(200, Number(process.env.MCP_LOCATE_TIMEOUT_MS || 2000));
        await loc.first().waitFor({ state: "attached", timeout: waitMs });
        if (enableHuman) {
          const moveOpts = buildMouseMoveOptions(humanObj, container);
          await moveMouseTo(page, loc.first(), moveOpts as any);
          await hoverHuman(page, loc.first());
        } else {
          await loc.first().hover({ timeout: waitMs });
        }
        return { content: [{ type: "text", text: JSON.stringify(ok({ selectorId })) }] };
      } catch (e: any) {
        // hover 动作失败不视为致命，返回 ok+skipped
        return { content: [{ type: "text", text: JSON.stringify(ok({ selectorId, skipped: true })) }] };
      }
    } catch (e: any) {
      return { content: [{ type: "text", text: JSON.stringify(err("INTERNAL_ERROR", String(e?.message || e))) }] };
    }
  };

  handlers[name("scroll")] = async (input: any) => {
    const { dirId, pageIndex, workspaceId, human, deltaY } = input as any;
    const enableHuman = human !== false && !(human && typeof human === "object" && human.enabled === false);
    const humanObj = typeof human === "object" ? human : (human === true ? { profile: "rapid" } : undefined);
    try {
      const { page } = await withPage(dirId, pageIndex, workspaceId);
      if (enableHuman) {
        const scrollOpts = buildScrollOptions(humanObj, container);
        await scrollHuman(page, typeof deltaY === "number" ? deltaY : 800, scrollOpts as any);
      } else {
        await page.mouse.wheel(0, typeof deltaY === "number" ? deltaY : 800);
      }
      return { content: [{ type: "text", text: JSON.stringify(ok({})) }] };
    } catch (e: any) {
      return { content: [{ type: "text", text: JSON.stringify(mapError(e, "action", { dirId })) }] };
    }
  };

  handlers[name("type")] = async (input: any) => {
    const { dirId, target, text, pageIndex, workspaceId, human } = input as any;
    const enableHuman = human !== false && !(human && typeof human === "object" && human.enabled === false);
    const humanObj = typeof human === "object" ? human : (human === true ? { profile: "rapid" } : undefined);
    const selectorId = (target && (target.id || target.text || target.role || target.selector)) || "anonymous";
    try {
      const { page } = await withPage(dirId, pageIndex, workspaceId);
      const loc = await resolveLocatorAsync(page, target || ({} as any));
      await loc.first().click();
      if (enableHuman) {
        const wpm = humanObj?.wpm ?? getProfile(humanObj?.profile ?? container.getHumanizationProfileKey()).wpm;
        await typeHuman(loc.first(), String(text ?? ""), { wpm });
      } else {
        await loc.first().fill(String(text ?? ""));
      }
      return { content: [{ type: "text", text: JSON.stringify(ok({ selectorId })) }] };
    } catch (e: any) {
      return { content: [{ type: "text", text: JSON.stringify(mapError(e, "action", { dirId, selectorId })) }] };
    }
  };

  handlers[name("input_clear")] = async (input: any) => {
    const { dirId, target, pageIndex, workspaceId, human } = input as any;
    const enableHuman = human !== false && !(human && typeof human === "object" && human.enabled === false);
    const selectorId = (target && (target.id || target.text || target.role || target.selector)) || "anonymous";
    try {
      const { page } = await withPage(dirId, pageIndex, workspaceId);
      const loc = await resolveLocatorAsync(page, target || ({} as any));
      if (enableHuman) {
        await clearInputHuman(page, loc.first());
      } else {
        await loc.first().fill("");
      }
      return { content: [{ type: "text", text: JSON.stringify(ok({ selectorId })) }] };
    } catch (e: any) {
      return { content: [{ type: "text", text: JSON.stringify(mapError(e, "action", { dirId, selectorId })) }] };
    }
  };

  handlers[name("screenshot")] = async (input: any) => {
    try {
      const { dirId, pageIndex, workspaceId, fullPage, returnImage } = input as any;
      const r = await manager.screenshot(dirId, pageIndex, fullPage, { workspaceId });
      const payload: any[] = [{ type: "text", text: JSON.stringify(ok({ path: r.path })) }];
      if (returnImage === true) {
        const base64 = r.buffer.toString("base64");
        payload.push({ type: "image", data: base64, mimeType: "image/png" } as any);
      }
      return { content: payload };
    } catch (e: any) {
      const { dirId, workspaceId } = input as any;
      return { content: [{ type: "text", text: JSON.stringify(isNoWsError(e) ? noWsErr(String(dirId), workspaceId) : err("SCREENSHOT_FAILED", String(e?.message || e))) }] };
    }
  };

  return handlers;
}

