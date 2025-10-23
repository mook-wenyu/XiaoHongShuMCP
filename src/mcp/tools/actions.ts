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
  // page.*
  const PageNew = z.object({ dirId: DirId, url: z.string().url().optional(), workspaceId: WorkspaceId });
  server.registerTool("page.new", { description: "在指定账号上下文中打开新页面（同 Context 多页）", inputSchema: { dirId: DirId, url: z.string().url().optional(), workspaceId: WorkspaceId } },
    async (input: any) => {
      const { dirId, url, workspaceId } = PageNew.parse(getParams(input));
      const res = await policy.use(dirId, async () => {
        try {
          const { context } = await manager.get(dirId, { workspaceId });
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
  server.registerTool("page.list", { description: "列出上下文中的页面（index+url）", inputSchema: { dirId: DirId, workspaceId: WorkspaceId } },
    async (input: any) => {
      const { dirId, workspaceId } = PageList.parse(getParams(input));
      const res = await policy.use(dirId, async () => {
        try {
          const { context } = await manager.get(dirId, { workspaceId });
          return okRes({ pages: Pages.listPages(context) });
        } catch (e: any) {
          return failRes({ code: 'PAGE_LIST_FAILED', message: String(e?.message || e) });
        }
      });
      return { content: [{ type: "text", text: JSON.stringify(res) }] };
    }
  );

  const PageClose = z.object({ dirId: DirId, pageIndex: PageIndex, workspaceId: WorkspaceId });
  server.registerTool("page.close", { description: "关闭一个页面（默认最后一个）", inputSchema: { dirId: DirId, pageIndex: PageIndex, workspaceId: WorkspaceId } },
    async (input: any) => {
      const { dirId, pageIndex, workspaceId } = PageClose.parse(getParams(input));
      const res = await policy.use(dirId, async () => {
        try {
          const { context } = await manager.get(dirId, { workspaceId });
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
  server.registerTool("action.navigate", { description: "导航到 URL（默认 reload；可选等待状态）", inputSchema: { dirId: DirId, url: z.string().optional(), pageIndex: PageIndex, wait: z.string().optional(), workspaceId: WorkspaceId } },
    async (input: any) => {
      const { dirId, url, pageIndex, wait, workspaceId } = Navigate.parse(getParams(input));
      const res = await policy.use(dirId, async () => {
        let page: any;
        try {
          const { context } = await manager.get(dirId, { workspaceId });
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
  server.registerTool("action.click", { description: "点击元素（人性化移动+可选高级参数）", inputSchema: { dirId: DirId, target: z.any(), pageIndex: PageIndex, profile: z.string().optional(), options: z.any().optional(), workspaceId: WorkspaceId } },
    async (input: any) => {
      const { dirId, target, pageIndex, profile, options, workspaceId } = Click.parse(getParams(input));
      const res = await policy.use(dirId, async () => {
        const { context } = await manager.get(dirId, { workspaceId });
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

  // action.type
  const Type = z.object({ dirId: DirId, target: Target.optional(), text: z.string().default(""), wpm: z.number().int().positive().optional(), profile: z.string().optional(), pageIndex: PageIndex, workspaceId: WorkspaceId });
  server.registerTool("action.type", { description: "按 WPM 模拟键入文本（默认空字符串）", inputSchema: { dirId: DirId, target: z.any(), text: z.string().optional(), wpm: z.number().optional(), profile: z.string().optional(), pageIndex: PageIndex, workspaceId: WorkspaceId } },
    async (input: any) => {
      const { dirId, target, text, wpm, profile, pageIndex, workspaceId } = Type.parse(getParams(input));
      const res = await policy.use(dirId, async () => {
        const { context } = await manager.get(dirId, { workspaceId });
        const page = await Pages.ensurePage(context, { pageIndex });
        try {
          const t0 = Date.now();
          // 默认定位策略：若无 target 且 text 非空，优先使用当前可编辑焦点；否则回退到可编辑元素集合；最后回退 body
          let effectiveTarget = target as any;
          if (!effectiveTarget && text && text.length > 0) {
            try {
              // 1) 使用 :focus（若为可编辑元素）
              const focusCount = await page.locator(":focus").count();
              if (focusCount > 0) {
                effectiveTarget = { selector: ":focus" };
              }
            } catch {}
            if (!effectiveTarget) {
              // 2) 启发式选择常见可编辑元素（取第一个可见）
              effectiveTarget = { selector: 'input, textarea, [contenteditable="true"]' };
            }
          }
          // 3) 最终兜底 body（与既有行为保持一致）
          effectiveTarget = effectiveTarget ?? { selector: 'body' };

          const selectorId = selectorIdFromTarget(effectiveTarget);
          const loc = await resolveLocatorResilient(page, effectiveTarget as any, { selectorId });
          const pf = profile ? getProfile(profile) : undefined;
          const wpmResolved = wpm ?? pf?.wpm ?? 180;
          if (text && text.length > 0) {
            await typeHuman(loc, text, { wpm: wpmResolved });
          }
          return okRes({ tookMs: Date.now() - t0, wpm: wpmResolved, profile: profile ?? (pf ? 'default' : undefined), selectorId });
        } catch (e: any) {
          const screenshotPath = await screenshotIfEnabled(page, dirId, 'type-error');
          return failRes({ code: ERRORS.TYPE_FAILED, message: String(e?.message || e), screenshotPath, lastLocator: JSON.stringify(target) });
        }
      });
      return { content: [{ type: "text", text: JSON.stringify(res) }] };
    }
  );

  // action.hover
  const Hover = z.object({ dirId: DirId, target: Target.optional(), pageIndex: PageIndex, profile: z.string().optional(), options: MoveOptionsSchema, workspaceId: WorkspaceId });
  server.registerTool("action.hover", { description: "悬停元素（人性化移动，支持 steps/randomness/overshoot）", inputSchema: { dirId: DirId, target: z.any(), pageIndex: PageIndex, profile: z.string().optional(), options: z.any().optional(), workspaceId: WorkspaceId } },
    async (input: any) => {
      const { dirId, target, pageIndex, profile, options, workspaceId } = Hover.parse(getParams(input));
      const res = await policy.use(dirId, async () => {
        const { context } = await manager.get(dirId, { workspaceId });
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
  server.registerTool("action.scroll", { description: "分段滚动页面（人性化 + 可选缓动/分段参数）", inputSchema: { dirId: DirId, deltaY: z.number().optional(), profile: z.string().optional(), options: z.any().optional(), pageIndex: PageIndex, workspaceId: WorkspaceId } },
    async (input: any) => {
      const { dirId, deltaY, profile, options, pageIndex, workspaceId } = Scroll.parse(getParams(input));
      const res = await policy.use(dirId, async () => {
        const { context } = await manager.get(dirId, { workspaceId });
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

  // action.waitFor
  const WaitFor = z.object({ dirId: DirId, target: Target.optional(), state: z.enum(["visible","hidden","attached","detached"]).optional(), timeoutMs: z.number().int().positive().optional(), pageIndex: PageIndex, workspaceId: WorkspaceId });
  server.registerTool("action.waitFor", { description: "等待选择器状态或仅超时（默认 sleep 500ms）", inputSchema: { dirId: DirId, target: z.any().optional(), state: z.string().optional(), timeoutMs: z.number().optional(), pageIndex: PageIndex, workspaceId: WorkspaceId } },
    async (input: any) => {
      const { dirId, target, state, timeoutMs, pageIndex, workspaceId } = WaitFor.parse(getParams(input));
      const res = await policy.use(dirId, async () => {
        let page: any;
        try {
          const { context } = await manager.get(dirId, { workspaceId });
          page = await Pages.ensurePage(context, { pageIndex });
          if (target) {
            const selectorId = selectorIdFromTarget(target);
            const loc = await resolveLocatorResilient(page, target as any, { selectorId });
            await loc.waitFor({ state: (state as any) ?? "visible", timeout: timeoutMs ?? 10000 });
          } else {
            await page.waitForTimeout(timeoutMs ?? 500);
          }
          return okRes();
        } catch (e: any) {
          const screenshotPath = await screenshotIfEnabled(page, dirId, 'waitfor-error');
          return failRes({ code: ERRORS.WAIT_FOR_FAILED, message: String(e?.message || e), screenshotPath });
        }
      });
      return { content: [{ type: "text", text: JSON.stringify(res) }] };
    }
  );

  // action.upload
  const Upload = z.object({ dirId: DirId, target: Target.optional(), files: z.array(z.string()).optional(), pageIndex: PageIndex, workspaceId: WorkspaceId });
  server.registerTool("action.upload", { description: "文件上传（input[type=file]）", inputSchema: { dirId: DirId, target: z.any().optional(), files: z.array(z.string()).optional(), pageIndex: PageIndex, workspaceId: WorkspaceId } },
    async (input: any) => {
      const { dirId, target, files, pageIndex, workspaceId } = Upload.parse(getParams(input));
      const res = await policy.use(dirId, async () => {
        let page: any;
        try {
          const { context } = await manager.get(dirId, { workspaceId });
          page = await Pages.ensurePage(context, { pageIndex });
          const effectiveTarget = target ?? { selector: 'input[type="file"]' };
          const selectorId = selectorIdFromTarget(effectiveTarget);
          const loc = await resolveLocatorResilient(page, effectiveTarget as any, { selectorId });
          if (!files || files.length === 0) {
            return failRes({ code: ERRORS.UPLOAD_FAILED, message: 'No files provided' });
          }
          await loc.setInputFiles(files);
          return okRes({ selectorId });
        } catch (e: any) {
          const screenshotPath = await screenshotIfEnabled(page, dirId, 'upload-error');
          return failRes({ code: ERRORS.UPLOAD_FAILED, message: String(e?.message || e), lastLocator: JSON.stringify(target), screenshotPath });
        }
      });
      return { content: [{ type: "text", text: JSON.stringify(res) }] };
    }
  );

  // action.extract
  const Extract = z.object({ dirId: DirId, target: Target.optional(), prop: z.enum(["text","html","value","href"]).default("text"), pageIndex: PageIndex, workspaceId: WorkspaceId });
  server.registerTool("action.extract", { description: "提取元素属性或文本", inputSchema: { dirId: DirId, target: z.any().optional(), prop: z.string().optional(), pageIndex: PageIndex, workspaceId: WorkspaceId } },
    async (input: any) => {
      const { dirId, target, prop, pageIndex, workspaceId } = Extract.parse(getParams(input));
      const res = await policy.use(dirId, async () => {
        let page: any;
        try {
          const { context } = await manager.get(dirId, { workspaceId });
          page = await Pages.ensurePage(context, { pageIndex });
          const effectiveTarget = target ?? { selector: 'body' };
          const selectorId = selectorIdFromTarget(effectiveTarget);
          const loc = await resolveLocatorResilient(page, effectiveTarget as any, { selectorId });
          let data: any = null;
          switch (prop) {
            case "html": data = await loc.evaluate((el) => (el as HTMLElement).outerHTML); break;
            case "value": data = await loc.evaluate((el) => (el as HTMLInputElement).value); break;
            case "href": data = await loc.evaluate((el) => (el as HTMLAnchorElement).href); break;
            default: data = await loc.innerText(); break;
          }
          return okRes({ data, selectorId });
        } catch (e: any) {
          const screenshotPath = await screenshotIfEnabled(page, dirId, 'extract-error');
          return failRes({ code: ERRORS.EXTRACT_FAILED, message: String(e?.message || e), lastLocator: JSON.stringify(target), screenshotPath });
        }
      });
      return { content: [{ type: "text", text: JSON.stringify(res) }] };
    }
  );

  // action.evaluate（谨慎：仅页面上下文，非 Node）
  const Evaluate = z.object({ dirId: DirId, expression: z.string(), arg: z.any().optional(), pageIndex: PageIndex, workspaceId: WorkspaceId });
  server.registerTool("action.evaluate", { description: "在页面中执行表达式（page.evaluate）", inputSchema: { dirId: DirId, expression: z.string(), arg: z.any(), pageIndex: PageIndex, workspaceId: WorkspaceId } },
    async (input: any) => {
      const { dirId, expression, arg, pageIndex, workspaceId } = Evaluate.parse(getParams(input));
      const res = await policy.use(dirId, async () => {
        let page: any;
        try {
          const { context } = await manager.get(dirId, { workspaceId });
          page = await Pages.ensurePage(context, { pageIndex });
          const data = await page.evaluate(new Function("arg", `return (async()=>{ ${expression} })()(arg);`) as any, arg);
          return okRes({ data });
        } catch (e: any) {
          const screenshotPath = await screenshotIfEnabled(page, dirId, 'evaluate-error');
          return failRes({ code: ERRORS.EVALUATE_FAILED, message: String(e?.message || e), screenshotPath });
        }
      });
      return { content: [{ type: "text", text: JSON.stringify(res) }] };
    }
  );

  // action.screenshot
  const Screenshot = z.object({ dirId: DirId, pageIndex: PageIndex, fullPage: z.boolean().optional(), workspaceId: WorkspaceId });
  server.registerTool("action.screenshot", { description: "截取页面（默认 fullPage=false）", inputSchema: { dirId: DirId, pageIndex: PageIndex, fullPage: z.boolean().optional(), workspaceId: WorkspaceId } },
    async (input: any) => {
      const { dirId, pageIndex, fullPage, workspaceId } = Screenshot.parse(getParams(input));
      const res = await policy.use(dirId, async () => {
        try {
          const { context } = await manager.get(dirId, { workspaceId });
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
  // action.comment（textarea 或 contenteditable）
  const Comment = z.object({ dirId: DirId, text: z.string().min(1), wpm: z.number().int().positive().optional(), pageIndex: PageIndex, workspaceId: WorkspaceId });
  server.registerTool("action.comment", { description: "在评论框输入文本并回车提交（启发式定位）", inputSchema: { dirId: DirId, text: z.string(), wpm: z.number().optional(), pageIndex: PageIndex, workspaceId: WorkspaceId } },
    async (input:any) => {
      const { dirId, text, wpm, pageIndex, workspaceId } = Comment.parse(getParams(input));
      const res = await policy.use(dirId, async () => {
        let page: any;
        try {
          const { context } = await manager.get(dirId, { workspaceId });
          page = await Pages.ensurePage(context, { pageIndex });
          const targets = [
            { placeholder: '评论' },
            { selector: 'textarea' },
            { selector: '[contenteditable="true"]' }
          ];
          for (const t of targets) {
            try {
              const selectorId = selectorIdFromTarget(t);
              const box = await resolveLocatorResilient(page, t as any, { selectorId });
              await clickHuman(page, box);
              await typeHuman(box, text + "\n", { wpm: wpm ?? 180 });
              return okRes({ selectorId });
            } catch {}
          }
          return failRes({ code: 'COMMENT_TARGET_NOT_FOUND', message: '未找到评论输入框' });
        } catch (e: any) {
          const screenshotPath = await screenshotIfEnabled(page, dirId, 'comment-error');
          return failRes({ code: ERRORS.COMMENT_FAILED, message: String(e?.message || e), screenshotPath });
        }
      });
      return { content: [{ type: 'text', text: JSON.stringify(res) }] };
    }
  );

  // action.scrollBrowse（分段滚动若干次）
  const ScrollBrowse = z.object({ dirId: DirId, segments: z.number().int().positive().default(6), deltaY: z.number().int().default(800), pageIndex: PageIndex, workspaceId: WorkspaceId });
  server.registerTool("action.scrollBrowse", { description: "分段滚动浏览若干次（人性化）", inputSchema: { dirId: DirId, segments: z.number().optional(), deltaY: z.number().optional(), pageIndex: PageIndex, workspaceId: WorkspaceId } },
    async (input:any) => {
      const { dirId, segments, deltaY, pageIndex, workspaceId } = ScrollBrowse.parse(getParams(input));
      const res = await policy.use(dirId, async () => {
        let page: any;
        try {
          const { context } = await manager.get(dirId, { workspaceId });
          page = await Pages.ensurePage(context, { pageIndex });
          const t0 = Date.now();
          for (let i = 0; i < (segments ?? 6); i++) { await scrollHuman(page, deltaY ?? 800); }
          return okRes({ tookMs: Date.now() - t0 });
        } catch (e: any) {
          const screenshotPath = await screenshotIfEnabled(page, dirId, 'scrollbrowse-error');
          return failRes({ code: 'SCROLL_BROWSE_FAILED', message: String(e?.message || e), screenshotPath });
        }
      });
      return { content: [{ type: 'text', text: JSON.stringify(res) }] };
    }
  );
}

