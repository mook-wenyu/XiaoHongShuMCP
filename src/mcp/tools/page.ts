/* 中文说明：页面工具（可复用前缀）
 * 提供 create/list/close/navigate/click/hover/scroll/screenshot/type/input.clear 等原子动作。
 */
import { z } from "zod";
import type { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import type { ServiceContainer } from "../../core/container.js";
import type { RoxyBrowserManager } from "../../services/roxyBrowser.js";
import { ok } from "../utils/result.js";
import { err, mapError } from "../utils/errors.js";
import { resolveLocatorAsync } from "../../selectors/index.js";
import * as Pages from "../../services/pages.js";
import { logHumanTrace } from "../../humanization/trace.js";
import { clickHuman, hoverHuman, scrollHuman, moveMouseTo, typeHuman, clearInputHuman } from "../../humanization/actions.js";
import { getProfile } from "../../humanization/profiles.js";

const DirId = z.string().min(1);
const WorkspaceId = z.string().optional();
const PageIndex = z.number().int().nonnegative().optional();
const Url = z.string().url();

const TargetHints = z.object({}).passthrough();

const HumanOptions = z.object({
  enabled: z.boolean().optional(),
  profile: z.string().optional(),
  // 鼠标移动参数
  steps: z.number().int().positive().optional(),
  randomness: z.number().min(0).max(1).optional(),
  overshoot: z.boolean().optional(),
  overshootAmount: z.number().min(0).optional(),
  microJitterPx: z.number().min(0).optional(),
  microJitterCount: z.number().int().min(0).max(8).optional(),
  // 滚动参数
  segments: z.number().int().min(1).max(30).optional(),
  jitterPx: z.number().min(0).optional(),
  perSegmentMs: z.number().min(0).optional(),
  microPauseChance: z.number().min(0).max(1).optional(),
  microPauseMinMs: z.number().min(0).optional(),
  microPauseMaxMs: z.number().min(0).optional(),
  macroPauseEvery: z.number().int().min(0).optional(),
  macroPauseMinMs: z.number().min(0).optional(),
  macroPauseMaxMs: z.number().min(0).optional(),
  // 输入速度（每分钟字数）
  wpm: z.number().min(60).max(280).optional(),
}).partial().optional();

// 支持布尔或对象：true 等价于 { enabled: true }
const HumanParam = z.union([z.boolean(), HumanOptions]).optional();

function buildMouseMoveOptions(human: any | undefined, container: ServiceContainer){
  const p = getProfile(human?.profile ?? container.getHumanizationProfileKey())
  return {
    steps: human?.steps ?? p.mouseSteps,
    randomness: human?.randomness ?? p.mouseRandomness,
    overshoot: human?.overshoot ?? true,
    overshootAmount: human?.overshootAmount ?? 12,
    microJitterPx: human?.microJitterPx ?? 1.2,
    microJitterCount: human?.microJitterCount ?? 2,
  }
}

function buildScrollOptions(human: any | undefined, container: ServiceContainer){
  const p = getProfile(human?.profile ?? container.getHumanizationProfileKey())
  return {
    segments: human?.segments ?? human?.steps ?? p.scrollSegments,
    jitterPx: human?.jitterPx ?? p.scrollJitterPx,
    perSegmentMs: human?.perSegmentMs ?? p.scrollPerSegmentMs,
    microPauseChance: human?.microPauseChance ?? 0.25,
    microPauseMinMs: human?.microPauseMinMs ?? 60,
    microPauseMaxMs: human?.microPauseMaxMs ?? 160,
    macroPauseEvery: human?.macroPauseEvery ?? 4,
    macroPauseMinMs: human?.macroPauseMinMs ?? 120,
    macroPauseMaxMs: human?.macroPauseMaxMs ?? 260,
  }
}

export function registerPageToolsWithPrefix(server: McpServer, container: ServiceContainer, manager: RoxyBrowserManager, prefix = "page") {
  const name = (n: string) => `${prefix}_${n.replace(/\./g, "_")}`;

  // create
  server.registerTool(name("create"), {
    description: "创建页面（可选 url）",
    inputSchema: { dirId: DirId, url: Url.optional(), workspaceId: WorkspaceId }
  }, async (input: any) => {
    try {
      const { dirId, url, workspaceId } = input as any;
      const r = await manager.createPage(dirId, url, { workspaceId });
      return { content: [{ type: "text", text: JSON.stringify(ok(r)) }] };
    } catch (e: any) { return { content: [{ type: "text", text: JSON.stringify(err("INTERNAL_ERROR", String(e?.message || e))) }] }; }
  });

  // list
  server.registerTool(name("list"), {
    description: "列出窗口中的页面（index+url）",
    inputSchema: { dirId: DirId, workspaceId: WorkspaceId }
  }, async (input: any) => {
    try {
      const { dirId, workspaceId } = input as any;
      const r = await manager.listPages(dirId, { workspaceId });
      return { content: [{ type: "text", text: JSON.stringify(ok(r)) }] };
    } catch (e: any) { return { content: [{ type: "text", text: JSON.stringify(err("INTERNAL_ERROR", String(e?.message || e))) }] }; }
  });

  // close
  server.registerTool(name("close"), {
    description: "关闭一个页面（未传 pageIndex 则关闭最后一个）",
    inputSchema: { dirId: DirId, pageIndex: PageIndex, workspaceId: WorkspaceId }
  }, async (input: any) => {
    try {
      const { dirId, pageIndex, workspaceId } = input as any;
      const r = await manager.closePage(dirId, pageIndex, { workspaceId });
      return { content: [{ type: "text", text: JSON.stringify(ok(r)) }] };
    } catch (e: any) { return { content: [{ type: "text", text: JSON.stringify(err("INTERNAL_ERROR", String(e?.message || e))) }] }; }
  });

  // navigate
  server.registerTool(name("navigate"), {
    description: "导航到指定 URL（可选 pageIndex/workspaceId）",
    inputSchema: { dirId: DirId, url: Url, pageIndex: PageIndex, workspaceId: WorkspaceId }
  }, async (input: any) => {
    const { dirId, url, pageIndex, workspaceId } = input as any;
    try {
      const r = await manager.navigate(dirId, url, pageIndex, { workspaceId });
      return { content: [{ type: "text", text: JSON.stringify(ok(r)) }] };
    } catch (e: any) {
      return { content: [{ type: "text", text: JSON.stringify(mapError(e, "navigate", { dirId, url })) }] };
    }
  });

  async function withPage(dirId: string, pageIndex: number | undefined, workspaceId: string | undefined) {
    const context = await manager.getContext(dirId, { workspaceId });
    const page = await Pages.ensurePage(context, { pageIndex });
    return { context, page };
  }

  // click
  server.registerTool(name("click"), {
    description: "点击目标元素（可选人类化）",
    inputSchema: { dirId: DirId, target: TargetHints, pageIndex: PageIndex, workspaceId: WorkspaceId, human: HumanParam }
  }, async (input: any) => {
    const { dirId, target, pageIndex, workspaceId, human } = input as any;
    const enableHuman = human !== false && !(human && typeof human === 'object' && human.enabled === false);
    const humanObj = typeof human === 'object' ? human : undefined;
    const selectorId = (target && (target.id || target.text || target.role || target.selector)) || "anonymous";
    try {
      const { page } = await withPage(dirId, pageIndex, workspaceId);
      let loc;
      try {
        loc = await resolveLocatorAsync(page, target || {} as any);
      } catch (e: any) {
        return { content: [{ type: "text", text: JSON.stringify(mapError(e, "locator", { dirId, selectorId, target })) }] };
      }
      try {
        if (enableHuman) {
          const moveOpts = buildMouseMoveOptions(humanObj, container)
          await moveMouseTo(page, loc.first(), moveOpts as any);
          await logHumanTrace(dirId, 'click.move', { selectorId, moveOpts });
          await clickHuman(page, loc.first());
          await logHumanTrace(dirId, 'click.click', { selectorId });
        } else {
          await loc.first().click();
        }
        return { content: [{ type: "text", text: JSON.stringify(ok({ selectorId })) }] };
      } catch (e: any) {
        return { content: [{ type: "text", text: JSON.stringify(mapError(e, "action", { dirId, selectorId, target })) }] };
      }
    } catch (e: any) {
      return { content: [{ type: "text", text: JSON.stringify(err("INTERNAL_ERROR", String(e?.message || e))) }] };
    }
  });

  // hover
  server.registerTool(name("hover"), {
    description: "悬停目标元素（可选人类化）",
    inputSchema: { dirId: DirId, target: TargetHints, pageIndex: PageIndex, workspaceId: WorkspaceId, human: HumanParam }
  }, async (input: any) => {
    const { dirId, target, pageIndex, workspaceId, human } = input as any;
    const enableHuman = human !== false && !(human && typeof human === 'object' && human.enabled === false);
    const humanObj = typeof human === 'object' ? human : undefined;
    const selectorId = (target && (target.id || target.text || target.role || target.selector)) || "anonymous";
    try {
      const { page } = await withPage(dirId, pageIndex, workspaceId);
      let loc;
      try {
        loc = await resolveLocatorAsync(page, target || {} as any);
      } catch (e: any) {
        return { content: [{ type: "text", text: JSON.stringify(mapError(e, "locator", { dirId, selectorId, target })) }] };
      }
      try {
        if (enableHuman) {
          const moveOpts = buildMouseMoveOptions(humanObj, container)
          await moveMouseTo(page, loc.first(), moveOpts as any);
          await hoverHuman(page, loc.first());
        } else {
          await loc.first().hover();
        }
        return { content: [{ type: "text", text: JSON.stringify(ok({ selectorId })) }] };
      } catch (e: any) {
        return { content: [{ type: "text", text: JSON.stringify(mapError(e, "action", { dirId, selectorId, target })) }] };
      }
    } catch (e: any) {
      return { content: [{ type: "text", text: JSON.stringify(err("INTERNAL_ERROR", String(e?.message || e))) }] };
    }
  });

  // scroll
  server.registerTool(name("scroll"), {
    description: "滚动页面（可选人类化参数）",
    inputSchema: { dirId: DirId, pageIndex: PageIndex, workspaceId: WorkspaceId, human: HumanParam, deltaY: z.number().optional() }
  }, async (input: any) => {
    const { dirId, pageIndex, workspaceId, human, deltaY } = input as any;
    const enableHuman = human !== false && !(human && typeof human === 'object' && human.enabled === false);
    const humanObj = typeof human === 'object' ? human : undefined;
    try {
      const { page } = await withPage(dirId, pageIndex, workspaceId);
      if (enableHuman) {
        const scrollOpts = buildScrollOptions(humanObj, container)
        await scrollHuman(page, typeof deltaY === 'number' ? deltaY : 800, scrollOpts as any);
      } else {
        await page.mouse.wheel(0, typeof deltaY === 'number' ? deltaY : 800);
      }
      return { content: [{ type: "text", text: JSON.stringify(ok({})) }] };
    } catch (e: any) { return { content: [{ type: "text", text: JSON.stringify(mapError(e, "action", { dirId })) }] }; }
  });

  // type
  server.registerTool(name("type"), {
    description: "在目标元素中输入文本（可选人类化 WPM）",
    inputSchema: { dirId: DirId, target: TargetHints, text: z.string().min(0), pageIndex: PageIndex, workspaceId: WorkspaceId, human: HumanParam }
  }, async (input: any) => {
    const { dirId, target, text, pageIndex, workspaceId, human } = input as any;
    const enableHuman = human !== false && !(human && typeof human === 'object' && human.enabled === false);
    const humanObj = typeof human === 'object' ? human : undefined;
    const selectorId = (target && (target.id || target.text || target.role || target.selector)) || "anonymous";
    try {
      const { page } = await withPage(dirId, pageIndex, workspaceId);
      const loc = await resolveLocatorAsync(page, target || {} as any);
      await loc.first().click();
      if (enableHuman) {
        const wpm = humanObj?.wpm ?? getProfile(humanObj?.profile ?? container.getHumanizationProfileKey()).wpm;
        await typeHuman(loc.first(), String(text ?? ''), { wpm });
      } else {
        await loc.first().fill(String(text ?? ''));
      }
      return { content: [{ type: "text", text: JSON.stringify(ok({ selectorId })) }] };
    } catch (e: any) {
      return { content: [{ type: "text", text: JSON.stringify(mapError(e, "action", { dirId, selectorId })) }] };
    }
  });

  // input.clear
  server.registerTool(name("input.clear"), {
    description: "清空输入框（支持人类化多策略）",
    inputSchema: { dirId: DirId, target: TargetHints, pageIndex: PageIndex, workspaceId: WorkspaceId, human: HumanParam }
  }, async (input: any) => {
    const { dirId, target, pageIndex, workspaceId, human } = input as any;
    const enableHuman = human !== false && !(human && typeof human === 'object' && human.enabled === false);
    const humanObj = typeof human === 'object' ? human : undefined;
    const selectorId = (target && (target.id || target.text || target.role || target.selector)) || "anonymous";
    try {
      const { page } = await withPage(dirId, pageIndex, workspaceId);
      const loc = await resolveLocatorAsync(page, target || {} as any);
      if (enableHuman) {
        await clearInputHuman(page, loc.first());
      } else {
        await loc.first().fill("");
      }
      return { content: [{ type: "text", text: JSON.stringify(ok({ selectorId })) }] };
    } catch (e: any) {
      return { content: [{ type: "text", text: JSON.stringify(mapError(e, "action", { dirId, selectorId })) }] };
    }
  });

  server.registerTool(name("screenshot"), {
    description: "页面截图（返回图片与文件路径）",
    inputSchema: { dirId: DirId, pageIndex: PageIndex, workspaceId: WorkspaceId, fullPage: z.boolean().optional() }
  }, async (input: any) => {
    try {
      const { dirId, pageIndex, workspaceId, fullPage } = input as any;
      const r = await manager.screenshot(dirId, pageIndex, fullPage, { workspaceId });
      const base64 = r.buffer.toString("base64");
      return {
        content: [
          { type: "text", text: JSON.stringify(ok({ path: r.path })) },
          { type: "image", data: base64, mimeType: "image/png" }
        ]
      };
    } catch (e: any) { return { content: [{ type: "text", text: JSON.stringify(err("SCREENSHOT_FAILED", String(e?.message || e))) }] }; }
  });
}

export function registerPageTools(server: McpServer, container: ServiceContainer, manager: RoxyBrowserManager) {
  return registerPageToolsWithPrefix(server, container, manager, "page");
}
