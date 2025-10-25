/* 中文注释：MCP 动作工具集合（仅原子动作，不含内部编排） */
import { z } from "zod";
import type { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import type { IPlaywrightConnector } from "../../contracts/IPlaywrightConnector.js";
import { ConnectionManager } from "../../services/connectionManager.js";
import type { PolicyEnforcer } from "../../services/policy.js";
import { resolveLocatorResilient } from "../../selectors/index.js";
import { clickHuman, hoverHuman, typeHuman, scrollHuman, moveMouseTo } from "../../humanization/actions.js";
import * as Pages from "../../services/pages.js";
import { ensureDir } from "../../services/artifacts.js";
import { join } from "node:path"; // deprecated local snapshot util replaced

import { getParams } from "../utils/params.js";
import { ok as okRes, fail as failRes, type ActionResult } from "../utils/result.js";
import { getProfile } from "../../humanization/profiles.js";
import { screenshotIfEnabled } from "../utils/snapshot.js";
import { selectorIdFromTarget } from "../../selectors/id.js";
import { DirId, WorkspaceId, PageIndex, Target, ClickOptionsSchema, MoveOptionsSchema, ScrollOptionsSchema, EasingName } from "../../schemas/actions.js";
import { ERRORS } from "../../errors.js";
import { linear, easeIn, easeOut, easeInOutQuad, easeInOutCubic } from "../../humanization/core/timing.js";


// 统一从 schemas/actions 导入通用入参 Schema（此处保留向后兼容）


function mapEasing(name?: EasingName) {
  switch (name) {
    case "linear": return linear;
    case "easeIn": return easeIn;
    case "easeOut": return easeOut;
    case "easeInOutQuad": return easeInOutQuad;
    case "easeInOutCubic": return easeInOutCubic;
    default: return undefined; // 由下层采用默认节律
  }
}

export function registerActionTools(server: McpServer, connector: IPlaywrightConnector, policy: PolicyEnforcer) {
  const manager = new ConnectionManager(connector);
  const envWS = (ws?: string) => ws ?? process.env.ROXY_DEFAULT_WORKSPACE_ID;
  // page.*
  const PageNew = z.object({ dirId: DirId, url: z.string().url().optional(), workspaceId: WorkspaceId });
  server.registerTool("page.new", { description: "在指定账号上下文中打开新页面（dirId 必填；workspaceId 可选，默认取 ROXY_DEFAULT_WORKSPACE_ID；同 Context 多页）", inputSchema: { dirId: DirId, url: z.string().url().optional(), workspaceId: WorkspaceId } },
    async (input: any) => {
      const { dirId, url, workspaceId } = PageNew.parse(getParams(input));
      const res = await policy.use(dirId, async () => {
        try {
          const { context } = await manager.getHealthy(dirId, { workspaceId: envWS(workspaceId) });
          const p = await Pages.newPage(context, url);
          const pages = Pages.listPages(context);
          return okRes({ pages, openedIndex: pages.length - 1 });
        } catch (e: any) {
          return failRes({ code: 'PAGE_NEW_FAILED', message: String(e?.message || e) });
        }
      });
      return { content: [{ type: "text", text: JSON.stringify(res) }] };
    }
  );

  const PageList = z.object({ dirId: DirId, workspaceId: WorkspaceId });
  server.registerTool("page.list", { description: "列出上下文中的页面（dirId 必填；workspaceId 可选，默认取 ROXY_DEFAULT_WORKSPACE_ID；返回 index+url）", inputSchema: { dirId: DirId, workspaceId: WorkspaceId } },
    async (input: any) => {
      const { dirId, workspaceId } = PageList.parse(getParams(input));
      const res = await policy.use(dirId, async () => {
        try {
          const { context } = await manager.getHealthy(dirId, { workspaceId: envWS(workspaceId) });
          return okRes({ pages: Pages.listPages(context) });
        } catch (e: any) {
          return failRes({ code: 'PAGE_LIST_FAILED', message: String(e?.message || e) });
        }
      });
      return { content: [{ type: "text", text: JSON.stringify(res) }] };
    }
  );

  const PageClose = z.object({ dirId: DirId, pageIndex: PageIndex, workspaceId: WorkspaceId });
  server.registerTool("page.close", { description: "关闭一个页面（dirId 必填；workspaceId 可选，默认取 ROXY_DEFAULT_WORKSPACE_ID；未传 pageIndex 默认关闭最后一个）", inputSchema: { dirId: DirId, pageIndex: PageIndex, workspaceId: WorkspaceId } },
    async (input: any) => {
      const { dirId, pageIndex, workspaceId } = PageClose.parse(getParams(input));
      const res = await policy.use(dirId, async () => {
        try {
          const { context } = await manager.getHealthy(dirId, { workspaceId: envWS(workspaceId) });
          const ok = await Pages.closePage(context, pageIndex);
          return okRes({ ok, pages: Pages.listPages(context) });
        } catch (e: any) {
          return failRes({ code: 'PAGE_CLOSE_FAILED', message: String(e?.message || e) });
        }
      });
      return { content: [{ type: "text", text: JSON.stringify(res) }] };
    }
  );

  // action.navigate
  const Navigate = z.object({ dirId: DirId, url: z.string().url().optional(), pageIndex: PageIndex, wait: z.enum(["load","domcontentloaded","networkidle"]).optional(), workspaceId: WorkspaceId });
  server.registerTool("action.navigate", { description: "导航到 URL（dirId 必填；workspaceId 可选，默认取 ROXY_DEFAULT_WORKSPACE_ID；未传 url 则 reload；wait 可选）", inputSchema: { dirId: DirId, url: z.string().optional(), pageIndex: PageIndex, wait: z.string().optional(), workspaceId: WorkspaceId } },
    async (input: any) => {
      const { dirId, url, pageIndex, wait, workspaceId } = Navigate.parse(getParams(input));
      const res = await policy.use(dirId, async () => {
        let page: any;
        try {
          const { context } = await manager.getHealthy(dirId, { workspaceId: envWS(workspaceId) });
          page = await Pages.ensurePage(context, { pageIndex });
          if (url) {
            await page.goto(url, { waitUntil: (wait as any) ?? "domcontentloaded" });
          } else {
            await page.reload({ waitUntil: (wait as any) ?? "domcontentloaded" });
          }
          return okRes({ url: page.url() });
        } catch (e: any) {
          const screenshotPath = page ? await screenshotIfEnabled(page, dirId, 'navigate-error') : undefined;
          return failRes({ code: ERRORS.NAVIGATE_FAILED, message: String(e?.message || e), screenshotPath });
        }
      });
      return { content: [{ type: "text", text: JSON.stringify(res) }] };
    }
  );

  // action.click
  const Click = z.object({ dirId: DirId, target: Target.optional(), pageIndex: PageIndex, profile: z.string().optional(), options: ClickOptionsSchema, workspaceId: WorkspaceId });
  server.registerTool("action.click", { description: "点击元素（dirId 必填；workspaceId 可选，默认取 ROXY_DEFAULT_WORKSPACE_ID；人性化移动+可选高级参数）", inputSchema: { dirId: DirId, target: z.any(), pageIndex: PageIndex, profile: z.string().optional(), options: z.any().optional(), workspaceId: WorkspaceId } },
    async (input: any) => {
      const { dirId, target, pageIndex, profile, options, workspaceId } = Click.parse(getParams(input));
      const res = await policy.use(dirId, async () => {
        const { context } = await manager.getHealthy(dirId, { workspaceId });
        const page = await Pages.ensurePage(context, { pageIndex });
        let loc: any;
        try {
          const t0 = Date.now();
          const effectiveTarget = target ?? { selector: 'body' };
          const selectorId = selectorIdFromTarget(effectiveTarget);
          loc = await resolveLocatorResilient(page, effectiveTarget as any, { selectorId });
          const clickDelay = options?.delay ?? undefined;
          const clickButton = options?.button ?? undefined;
          const clickCount = options?.clickCount ?? undefined;

          if (profile) {
            const pf = getProfile(profile);
            await moveMouseTo(page, loc, { steps: options?.steps ?? pf.mouseSteps, randomness: options?.randomness ?? pf.mouseRandomness, overshoot: options?.overshoot, overshootAmount: options?.overshootAmount, microJitterPx: options?.microJitterPx, microJitterCount: options?.microJitterCount });
            await loc.click({ delay: clickDelay ?? 30 + Math.random() * 120, button: clickButton, clickCount: clickCount });
          } else if (options) {
            await moveMouseTo(page, loc, { steps: options.steps, randomness: options.randomness, overshoot: options.overshoot, overshootAmount: options.overshootAmount, microJitterPx: options.microJitterPx, microJitterCount: options.microJitterCount });
            await loc.click({ delay: clickDelay, button: clickButton, clickCount: clickCount });
          } else {
            // 使用人性化默认（包含轻微微抖动与节律），不显式禁用
            await clickHuman(page, loc);
          }
          return okRes({ tookMs: Date.now() - t0, profile: profile ?? 'default', selectorId });
        } catch (e: any) {
          const screenshotPath = await screenshotIfEnabled(page, dirId, 'click-error');
          return failRes({ code: ERRORS.CLICK_FAILED, message: String(e?.message || e), screenshotPath, lastLocator: JSON.stringify(target) });
        }
      });
      return { content: [{ type: "text", text: JSON.stringify(res) }] };
    }
  );

  // （移除）action.type：键入属于内容层辅助能力，不再对外暴露 MCP 工具；请改用平台特定流程。
  // action.hover
  const Hover = z.object({ dirId: DirId, target: Target.optional(), pageIndex: PageIndex, profile: z.string().optional(), options: MoveOptionsSchema, workspaceId: WorkspaceId });
  server.registerTool("action.hover", { description: "悬停元素（dirId 必填；workspaceId 可选，默认取 ROXY_DEFAULT_WORKSPACE_ID；人性化移动 + steps/randomness/overshoot）", inputSchema: { dirId: DirId, target: z.any(), pageIndex: PageIndex, profile: z.string().optional(), options: z.any().optional(), workspaceId: WorkspaceId } },
    async (input: any) => {
      const { dirId, target, pageIndex, profile, options, workspaceId } = Hover.parse(getParams(input));
      const res = await policy.use(dirId, async () => {
        const { context } = await manager.getHealthy(dirId, { workspaceId });
        const page = await Pages.ensurePage(context, { pageIndex });
        try {
          const t0 = Date.now();
          const effectiveTarget = target ?? { selector: 'body' };
          const selectorId = selectorIdFromTarget(effectiveTarget);
          const loc = await resolveLocatorResilient(page, effectiveTarget as any, { selectorId });
          if (profile) {
            const pf = getProfile(profile);
            await moveMouseTo(page, loc, { steps: options?.steps ?? pf.mouseSteps, randomness: options?.randomness ?? pf.mouseRandomness, overshoot: options?.overshoot, overshootAmount: options?.overshootAmount, microJitterPx: options?.microJitterPx, microJitterCount: options?.microJitterCount });
            await loc.hover();
          } else if (options) {
            await moveMouseTo(page, loc, { steps: options.steps, randomness: options.randomness, overshoot: options.overshoot, overshootAmount: options.overshootAmount, microJitterPx: options.microJitterPx, microJitterCount: options.microJitterCount });
            await loc.hover();
          } else {
            await hoverHuman(page, loc);
          }
          return okRes({ tookMs: Date.now() - t0, profile: profile ?? 'default', selectorId });
        } catch (e: any) {
          const screenshotPath = await screenshotIfEnabled(page, dirId, 'hover-error');
          return failRes({ code: ERRORS.HOVER_FAILED, message: String(e?.message || e), screenshotPath, lastLocator: JSON.stringify(target) });
        }
      });
      return { content: [{ type: "text", text: JSON.stringify(res) }] };
    }
  );

  // action.scroll
  const Scroll = z.object({ dirId: DirId, deltaY: z.number().int().default(800), profile: z.string().optional(), options: ScrollOptionsSchema, pageIndex: PageIndex, workspaceId: WorkspaceId });
  server.registerTool("action.scroll", { description: "分段滚动页面（dirId 必填；workspaceId 可选，默认取 ROXY_DEFAULT_WORKSPACE_ID；人性化 + 缓动/分段参数）", inputSchema: { dirId: DirId, deltaY: z.number().optional(), profile: z.string().optional(), options: z.any().optional(), pageIndex: PageIndex, workspaceId: WorkspaceId } },
    async (input: any) => {
      const { dirId, deltaY, profile, options, pageIndex, workspaceId } = Scroll.parse(getParams(input));
      const res = await policy.use(dirId, async () => {
        const { context } = await manager.getHealthy(dirId, { workspaceId });
        const page = await Pages.ensurePage(context, { pageIndex });
        try {
          const t0 = Date.now();
          const easingFn = mapEasing(options?.easing as EasingName | undefined);
          if (profile) {
            const pf = getProfile(profile);
            await scrollHuman(page, deltaY, { segments: options?.segments ?? pf.scrollSegments, jitterPx: options?.jitterPx ?? pf.scrollJitterPx, perSegmentMs: options?.perSegmentMs ?? pf.scrollPerSegmentMs, easing: easingFn });
          } else if (options) {
            await scrollHuman(page, deltaY, { segments: options.segments, jitterPx: options.jitterPx, perSegmentMs: options.perSegmentMs, easing: easingFn });
          } else {
            await scrollHuman(page, deltaY);
          }
          return okRes({ tookMs: Date.now() - t0, profile: profile ?? 'default' });
        } catch (e: any) {
          const screenshotPath = await screenshotIfEnabled(page, dirId, 'scroll-error');
          return failRes({ code: ERRORS.SCROLL_FAILED, message: String(e?.message || e), screenshotPath });
        }
      });
      return { content: [{ type: "text", text: JSON.stringify(res) }] };
    }
  );

  // （移除）action.waitFor：等待属于内部辅助能力，不再对外暴露 MCP 工具；请在具体步骤中使用 Locator/URL/Response 等有语义的等待。
  // （移除）action.upload：上传属于内容层/页面上下文操作，不再对外暴露 MCP 工具；请改用平台特定流程。
  // （移除）action.extract：通用提取不再暴露为 MCP 工具；如需数据采集，请使用平台特定采集流程或评估器。
  // （移除）action.evaluate：任意 evaluate 不再暴露为 MCP 工具，避免滥用与可观测性缺失。
  // action.screenshot
  const Screenshot = z.object({ dirId: DirId, pageIndex: PageIndex, fullPage: z.boolean().optional(), workspaceId: WorkspaceId });
  server.registerTool("action.screenshot", { description: "截取页面（dirId 必填；workspaceId 可选，默认取 ROXY_DEFAULT_WORKSPACE_ID；fullPage 默认 false）", inputSchema: { dirId: DirId, pageIndex: PageIndex, fullPage: z.boolean().optional(), workspaceId: WorkspaceId } },
    async (input: any) => {
      const { dirId, pageIndex, fullPage, workspaceId } = Screenshot.parse(getParams(input));
      const res = await policy.use(dirId, async () => {
        try {
          const { context } = await manager.getHealthy(dirId, { workspaceId: envWS(workspaceId) });
          const page = await Pages.ensurePage(context, { pageIndex });
          const outRoot = "artifacts/" + dirId + "/actions";
          await ensureDir(outRoot);
          const path = join(outRoot, `snap-${Date.now()}.png`);
          await page.screenshot({ path, fullPage: !!fullPage });
          return okRes({ path });
        } catch (e: any) {
          return failRes({ code: 'SCREENSHOT_FAILED', message: String(e?.message || e) });
        }
      });
      return { content: [{ type: "text", text: JSON.stringify(res) }] };
    }
  );

  // （移除）action.waitForLoadState / action.waitForResponse：不再对外作为 MCP 工具暴露，改为由上层流程在页面语义中处理（例如 progressiveWait）。
  // （移除）action.comment：平台通用评论属于内容流程范畴，不再对外暴露 MCP 工具；请改用平台流程。
  // （移除）action.scrollBrowse：多余的 MCP 包装，通用滚动由 action.scroll 覆盖，复杂浏览请使用平台特定流程。
}


