/* 中文注释：小红书快捷工具（语义化封装）
 * 说明：本文件仅封装常见操作的“语义名”，底层仍复用 page.* 与 action.* 的能力模型。
 * 边界：不内置任何反检测或指纹处理逻辑（由 Roxy 负责）。
 */
import { z } from "zod";
import type { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { ConnectionManager } from "../../services/connectionManager.js";
import type { IPlaywrightConnector } from "../../contracts/IPlaywrightConnector.js";
import type { PolicyEnforcer } from "../../services/policy.js";
import * as Pages from "../../services/pages.js";
import { resolveLocatorAsync } from "../../selectors/index.js";
import { clickHuman, hoverHuman, typeHuman, scrollHuman } from "../../humanization/actions.js";
import { ensureDir } from "../../services/artifacts.js";
import { join } from "node:path";
import { getParams } from "../utils/params.js";
import { ok as okRes, fail as failRes } from "../utils/result.js";
import { XHS_CONF } from "../../config/xhs.js";
import { ERRORS } from "../../errors.js";
// 已移除 DSL/任务桥接：发布等流程直接由选择器原子动作驱动

// 统一使用全局 Schema：dirId 必填、workspaceId 可选（可取 ROXY_DEFAULT_WORKSPACE_ID）
import { DirId, WorkspaceId } from "../../schemas/actions.js";
const Keywords = z.array(z.string()).nonempty();

async function screenshotOnError(page: any, dirId: string, tag: string) {
  try {
    const outRoot = join('artifacts', dirId, 'navigation');
    await ensureDir(outRoot);
    const path = join(outRoot, `${tag}-${Date.now()}.png`);
    await page.screenshot({ path, fullPage: true });
    return path;
  } catch { return undefined; }
}

export function registerXhsShortcutsTools(server: McpServer, connector: IPlaywrightConnector, policy: PolicyEnforcer) {
  const manager = new ConnectionManager(connector);



  // 登录与导航
  const BK = z.object({ dirId: DirId, workspaceId: WorkspaceId });


  // 关闭当前笔记模态（原子操作）
  server.registerTool("xhs_close_modal", { description: "关闭当前页的笔记详情模态窗口（Esc→关闭按钮→遮罩）", inputSchema: { dirId: DirId, workspaceId: WorkspaceId } },
    async (input: any) => {
      const { dirId, workspaceId } = BK.parse(getParams(input));
      const res = await policy.use(dirId, async () => {
        const { context } = await manager.getHealthy(dirId, { workspaceId });
        const page = await Pages.ensurePage(context, {});
        const { closeModalIfOpen } = await import("../../domain/xhs/navigation.js");
        const closed = await closeModalIfOpen(page);
        return { ok: true, closed };
      });
      return { content: [{ type: "text", text: JSON.stringify(res) }] };
    }
  );

  // 导航到小红书探索页（主页）
  const NavigateHome = z.object({ dirId: DirId, workspaceId: WorkspaceId });
  server.registerTool('xhs.navigate.home', {
    description: '导航到小红书探索页（主页入口，包含所有频道导航）',
    inputSchema: { dirId: DirId, workspaceId: WorkspaceId }
  }, async (input: any) => {
    const { dirId, workspaceId } = NavigateHome.parse(getParams(input));
    const res = await policy.use(dirId, async () => {
      let page: any;
      try {
        const { context } = await manager.getHealthy(dirId, { workspaceId });
        page = await Pages.ensurePage(context, {});
        await page.goto('https://www.xiaohongshu.com/explore', { waitUntil: 'domcontentloaded' });

        // 轻量语义校验：尝试找到“发现”等关键可见文本；失败不立即报错，用 URL 作为弱验证
        let verified = false;
        try {
          const loc = await resolveLocatorAsync(page, { text: '发现' } as any);
          await loc.waitFor({ state: 'visible', timeout: 3000 });
          verified = true;
        } catch {}
        const url = page.url();
        if (!verified && /\/explore\b/.test(url)) verified = true;

        // 登录/拦截判定（弱信号）
        const requiresLogin = /(login|passport|account)/i.test(url) ? true : undefined;

        return okRes({ target: 'home', url, verified, requiresLogin });
      } catch (e: any) {
        const screenshotPath = page ? await screenshotOnError(page, dirId, 'navigate-home-error') : undefined;
        return failRes({ code: ERRORS.NAVIGATE_FAILED, message: String(e?.message || e), screenshotPath });
      }
    });
    return { content: [{ type: 'text', text: JSON.stringify(res) }] };
  });

  // 导航到小红书发现页（个性化推荐流）
  const NavigateDiscover = z.object({ dirId: DirId, workspaceId: WorkspaceId });
  server.registerTool('xhs.navigate.discover', {
    description: '导航到小红书发现页（个性化推荐流），使用拟人化点击行为并监听 homefeed API',
    inputSchema: { dirId: DirId, workspaceId: WorkspaceId }
  }, async (input: any) => {
    const { dirId, workspaceId } = NavigateDiscover.parse(getParams(input));
    const res = await policy.use(dirId, async () => {
      let page: any;
      try {
        const { context } = await manager.getHealthy(dirId, { workspaceId });
        page = await Pages.ensurePage(context, {});
        await page.goto('https://www.xiaohongshu.com/explore', {
          waitUntil: 'domcontentloaded'
        });
        // 精确定位左侧导航“发现”链接（优先用稳定的 id + anchor），避免文本歧义
        let target = page.locator('#explore-guide-refresh a.link-wrapper').first();
        if (!(await target.isVisible().catch(() => false))) {
          // 其一：在 navigation 区域内以角色定位
          const navLink = page.getByRole('navigation').getByRole('link', { name: '发现' }).first();
          if (await navLink.isVisible().catch(() => false)) target = navLink; else {
            // 其二：以 href 语义与文本过滤定位
            const byHref = page.locator('a[href*="/explore"]').filter({ hasText: '发现' }).first();
            if (await byHref.isVisible().catch(() => false)) target = byHref; else {
              // 兜底：span.channel 文本（点击该元素也可触发父 a）
              const bySpan = page.locator('span.channel:has-text("发现")').first();
              if (await bySpan.isVisible().catch(() => false)) target = bySpan;
            }
          }
        }
        let feedVerified = false; let feedItems: number | undefined; let feedTtfbMs: number | undefined;
        if (await target.isVisible().catch(() => false)) {
          const { waitHomefeed } = await import("../../domain/xhs/netwatch.js");
          const feedW = waitHomefeed(page, XHS_CONF.feed.waitApiMs);
          await clickHuman(page, target);
          const r = await feedW.promise.catch(() => ({ ok: false } as any));
          feedVerified = !!r.ok; feedItems = Array.isArray(r.data?.items) ? r.data?.items.length : undefined; feedTtfbMs = r.ttfbMs;
        }
        return okRes({
          target: 'discover',
          url: page.url(),
          verified: feedVerified,
          feedItems,
          feedTtfbMs,
          description: '已通过探索页/导航进入推荐流（接口回执软校验）'
        });
      } catch (e: any) {
        const screenshotPath = page ? await screenshotOnError(page, dirId, 'navigate-discover-error') : undefined;
        return failRes({
          code: ERRORS.NAVIGATE_FAILED,
          message: String(e?.message || e),
          screenshotPath
        });
      }
    });
    return { content: [{ type: 'text', text: JSON.stringify(res) }] };
  });

  // （移除）xhs_dump_html / xhs_detect_page：调试型工具不再通过 MCP 暴露。
  // 搜索关键词
  const Search = z.object({ dirId: DirId, keyword: z.string().min(1), workspaceId: WorkspaceId });
  server.registerTool("xhs_search_keyword", { description: "关闭模态→点击搜索栏→输入关键词→点击搜索图标", inputSchema: { dirId: DirId, keyword: z.string(), workspaceId: WorkspaceId } },
    async (input: any) => {
      const { dirId, keyword, workspaceId } = Search.parse(getParams(input));
      const res = await policy.use(dirId, async () => {
        const { context } = await manager.getHealthy(dirId, { workspaceId });
        const page = await Pages.ensurePage(context, {});
        const { searchKeyword } = await import("../../domain/xhs/search.js");
        const r = await searchKeyword(page, keyword);
        return r.ok ? okRes({ url: r.url, verified: r.verified === true, matchedCount: r.matchedCount ?? 0 }) : failRes({ code: 'SEARCH_FAILED', message: '定位搜索框或提交失败' });
      });
      return { content: [{ type: "text", text: JSON.stringify(res) }] };
    }
  );

  // 根据关键词选择一条笔记
  const SelectNote = z.object({ dirId: DirId, keywords: Keywords, workspaceId: WorkspaceId });
  server.registerTool("xhs_select_note", { description: "根据关键词选择笔记（API 为准→DOM 侧证；强制等待详情 feed）", inputSchema: { dirId: DirId, keywords: z.array(z.string()), workspaceId: WorkspaceId } },
    async (input: any) => {
      const { dirId, keywords, workspaceId } = SelectNote.parse(getParams(input));
      const res = await policy.use(dirId, async () => {
        const { context } = await manager.getHealthy(dirId, { workspaceId });
        const page = await Pages.ensurePage(context, {});
        const { ensureDiscoverPage, closeModalIfOpen, findAndOpenNoteByKeywords, detectPageType, PageType } = await import("../../domain/xhs/navigation.js");

        // 1) 关闭模态
        await closeModalIfOpen(page);
        // 2) 留在探索/发现/搜索页；否则进入发现页
        const type = await detectPageType(page);
        if (![PageType.ExploreHome, PageType.Discover, PageType.Search].includes(type)) {
          await ensureDiscoverPage(page);
        }
        // 3) 匹配可视区域并滚动重试（API 精确锚点优先；滚动后“智能批次确认”开启）
        const r = await findAndOpenNoteByKeywords(page, keywords, { maxScrolls: Number(process.env.XHS_SELECT_MAX_SCROLLS || 18), settleMs: 220, useApiAfterScroll: true, preferApiAnchors: true });
        if (!r.ok) return failRes({ code: 'NOTE_NOT_FOUND', message: '未在可视范围匹配到关键词' });

        // 4) API 为准：强制等待 feed，未验证到则报错（FEED_TIMEOUT）。
        if (r.feedVerified !== true) {
          return failRes({ code: ERRORS.FEED_TIMEOUT, message: '详情 feed 未到达或超时' });
        }
        // DOM 侧证（可选）：尝试提取 note 元信息，不影响成败
        let noteId: string | undefined; let noteType: string | undefined;
        try { noteId = await page.locator('.note-detail-mask[note-id]').first().getAttribute('note-id') || undefined; } catch {}
        try { noteType = await page.locator('#noteContainer, .note-container').first().getAttribute('data-type') || undefined; } catch {}

        return okRes({ opened: true, url: page.url(), matched: r.matched, noteId, noteType, feedVerified: r.feedVerified, feedItems: r.feedItems, feedType: r.feedType, feedTtfbMs: r.feedTtfbMs });
      });
      return { content: [{ type: "text", text: JSON.stringify(res) }] };
    }
  );

  // —— 详情模态操作 —— 
  const NoteOps = {
    Like: z.object({ dirId: DirId, workspaceId: WorkspaceId }),
    Comment: z.object({ dirId: DirId, commentText: z.string().min(1), workspaceId: WorkspaceId }),
  } as const;

  server.registerTool("xhs.note.like", { description: "在笔记详情模态内执行点赞，返回 newLike=true/false", inputSchema: { dirId: DirId, workspaceId: WorkspaceId } },
    async (input:any) => {
      const { dirId, workspaceId } = NoteOps.Like.parse(getParams(input));
      const res = await policy.use(dirId, async () => {
        const { context } = await manager.getHealthy(dirId, { workspaceId });
        const page = await Pages.ensurePage(context, {});
        const { likeCurrent } = await import("../../domain/xhs/noteActions.js");
        return await likeCurrent(page);
      });
      return { content: [{ type: 'text', text: JSON.stringify(res) }] };
    }
  );

  server.registerTool("xhs.note.unlike", { description: "在笔记详情模态内执行取消点赞", inputSchema: { dirId: DirId, workspaceId: WorkspaceId } },
    async (input:any) => {
      const { dirId, workspaceId } = NoteOps.Like.parse(getParams(input));
      const res = await policy.use(dirId, async () => {
        const { context } = await manager.getHealthy(dirId, { workspaceId });
        const page = await Pages.ensurePage(context, {});
        const { unlikeCurrent } = await import("../../domain/xhs/noteActions.js");
        return await unlikeCurrent(page);
      });
      return { content: [{ type: 'text', text: JSON.stringify(res) }] };
    }
  );

  server.registerTool("xhs.note.collect", { description: "在笔记详情模态内执行收藏", inputSchema: { dirId: DirId, workspaceId: WorkspaceId } },
    async (input:any) => {
      const { dirId, workspaceId } = NoteOps.Like.parse(getParams(input));
      const res = await policy.use(dirId, async () => {
        const { context } = await manager.getHealthy(dirId, { workspaceId });
        const page = await Pages.ensurePage(context, {});
        const { collectCurrent } = await import("../../domain/xhs/noteActions.js");
        return await collectCurrent(page);
      });
      return { content: [{ type: 'text', text: JSON.stringify(res) }] };
    }
  );

  server.registerTool("xhs.note.uncollect", { description: "在笔记详情模态内执行取消收藏", inputSchema: { dirId: DirId, workspaceId: WorkspaceId } },
    async (input:any) => {
      const { dirId, workspaceId } = NoteOps.Like.parse(getParams(input));
      const res = await policy.use(dirId, async () => {
        const { context } = await manager.getHealthy(dirId, { workspaceId });
        const page = await Pages.ensurePage(context, {});
        const { uncollectCurrent } = await import("../../domain/xhs/noteActions.js");
        return await uncollectCurrent(page);
      });
      return { content: [{ type: 'text', text: JSON.stringify(res) }] };
    }
  );

  server.registerTool("xhs.note.comment", { description: "在笔记详情模态内发表评论（打开输入→输入→发送）", inputSchema: { dirId: DirId, commentText: z.string(), workspaceId: WorkspaceId } },
    async (input:any) => {
      const { dirId, commentText, workspaceId } = NoteOps.Comment.parse(getParams(input));
      const res = await policy.use(dirId, async () => {
        const { context } = await manager.getHealthy(dirId, { workspaceId });
        const page = await Pages.ensurePage(context, {});
        const { commentCurrent } = await import("../../domain/xhs/noteActions.js");
        return await commentCurrent(page, commentText);
      });
      return { content: [{ type: 'text', text: JSON.stringify(res) }] };
    }
  );

  server.registerTool("xhs.note.follow", { description: "在笔记详情模态内关注作者", inputSchema: { dirId: DirId, workspaceId: WorkspaceId } },
    async (input:any) => {
      const { dirId, workspaceId } = NoteOps.Like.parse(getParams(input));
      const res = await policy.use(dirId, async () => {
        const { context } = await manager.getHealthy(dirId, { workspaceId });
        const page = await Pages.ensurePage(context, {});
        const { followAuthor } = await import("../../domain/xhs/noteActions.js");
        return await followAuthor(page);
      });
      return { content: [{ type: 'text', text: JSON.stringify(res) }] };
    }
  );

  server.registerTool("xhs.note.unfollow", { description: "在笔记详情模态内取消关注作者", inputSchema: { dirId: DirId, workspaceId: WorkspaceId } },
    async (input:any) => {
      const { dirId, workspaceId } = NoteOps.Like.parse(getParams(input));
      const res = await policy.use(dirId, async () => {
        const { context } = await manager.getHealthy(dirId, { workspaceId });
        const page = await Pages.ensurePage(context, {});
        const { unfollowAuthor } = await import("../../domain/xhs/noteActions.js");
        return await unfollowAuthor(page);
      });
      return { content: [{ type: 'text', text: JSON.stringify(res) }] };
    }
  );


  // 随机浏览 / 关键词浏览（流程）
  const Rand = z.object({ dirId: DirId, portraitId: z.string().optional(), behaviorProfile: z.string().optional(), workspaceId: WorkspaceId });
  server.registerTool("xhs_random_browse", { description: "随机浏览（概率点赞/收藏）", inputSchema: { dirId: DirId, portraitId: z.string().optional(), behaviorProfile: z.string().optional(), workspaceId: WorkspaceId } },
    async (input:any) => {
      const { dirId, workspaceId } = Rand.parse(getParams(input));
      const res = await policy.use(dirId, async () => {
        const { context } = await manager.getHealthy(dirId, { workspaceId });
        const page = await Pages.ensurePage(context, {});
        await scrollHuman(page, 1200);
        return { ok: true };
      });
      return { content: [{ type: "text", text: JSON.stringify(res) }] };
    }
  );

  const KeyBrowse = z.object({ dirId: DirId, keywords: Keywords, behaviorProfile: z.string().optional(), workspaceId: WorkspaceId });
  server.registerTool("xhs_keyword_browse", { description: "关键词浏览（概率点赞/收藏）", inputSchema: { dirId: DirId, keywords: z.array(z.string()), behaviorProfile: z.string().optional(), workspaceId: WorkspaceId } },
    async (input:any) => {
      const { dirId, keywords, workspaceId } = KeyBrowse.parse(getParams(input));
      const res = await policy.use(dirId, async () => {
        const { context } = await manager.getHealthy(dirId, { workspaceId });
        const page = await Pages.ensurePage(context, {});
        await page.goto("https://www.xiaohongshu.com/", { waitUntil: 'domcontentloaded' });
        const box = await resolveLocatorAsync(page, { placeholder: '搜索' } as any);
        await clickHuman(page, box); await typeHuman(box, keywords.join(' ') + "\n", { wpm: 200 });
        await page.waitForLoadState('domcontentloaded');
        await scrollHuman(page, 1600);
        return { ok: true };
      });
      return { content: [{ type: "text", text: JSON.stringify(res) }] };
    }
  );

  // 数据采集
  const CapPage = z.object({ dirId: DirId, targetCount: z.number().int().positive(), workspaceId: WorkspaceId });
  server.registerTool("xhs_capture_page_notes", { description: "采集当前页面笔记（可见范围）", inputSchema: { dirId: DirId, targetCount: z.number(), workspaceId: WorkspaceId } },
    async (input:any) => {
      const { dirId, targetCount, workspaceId } = CapPage.parse(getParams(input));
      const res = await policy.use(dirId, async () => {
        const { context } = await manager.getHealthy(dirId, { workspaceId });
        const page = await Pages.ensurePage(context, {});
        const rows = await page.evaluate((limit: number) => {
          const data: any[] = []; const cards = Array.from(document.querySelectorAll('a, article, div')) as HTMLElement[];
          for (const el of cards) { const t = (el.innerText || '').trim(); if (t && t.length >= 2) data.push({ text: t.slice(0, 60).replace(/\s+/g, ' ') }); if (data.length >= limit) break; }
          return data;
        }, targetCount);
        const outRoot = join('artifacts', dirId, 'capture'); await ensureDir(outRoot);
        const csvPath = join(outRoot, `notes-${Date.now()}.csv`);
        const csv = 'text\n' + rows.map(r => '"' + String(r.text || '').replace(/"/g, '""') + '"').join('\n');
        const { writeFile } = await import('node:fs/promises'); await writeFile(csvPath, csv, 'utf-8');
        return { ok: true, count: rows.length, path: csvPath };
      });
      return { content: [{ type: "text", text: JSON.stringify(res) }] };
    }
  );

  const NoteCap = z.object({ dirId: DirId, keywords: Keywords, targetCount: z.number().int().positive(), workspaceId: WorkspaceId });
  server.registerTool("xhs_note_capture", { description: "按关键词批量采集笔记", inputSchema: { dirId: DirId, keywords: z.array(z.string()), targetCount: z.number(), workspaceId: WorkspaceId } },
    async (input:any) => {
      const { dirId, keywords, targetCount, workspaceId } = NoteCap.parse(getParams(input));
      const res = await policy.use(dirId, async () => {
        const { context } = await manager.getHealthy(dirId, { workspaceId });
        const page = await Pages.ensurePage(context, {});
        await page.goto('https://www.xiaohongshu.com/', { waitUntil: 'domcontentloaded' });
        const box = await resolveLocatorAsync(page, { placeholder: '搜索' } as any);
        await clickHuman(page, box); await typeHuman(box, keywords.join(' ') + "\n", { wpm: 200 });
        await page.waitForLoadState('domcontentloaded');
        await scrollHuman(page, 2200);
        // 采集
        const rows = await page.evaluate((limit: number) => {
          const data: any[] = []; const cards = Array.from(document.querySelectorAll('a, article, div')) as HTMLElement[];
          for (const el of cards) { const t = (el.innerText || '').trim(); if (t && t.length >= 2) data.push({ text: t.slice(0, 60).replace(/\s+/g, ' ') }); if (data.length >= limit) break; }
          return data;
        }, targetCount);
        const outRoot = join('artifacts', dirId, 'capture'); await ensureDir(outRoot);
        const csvPath = join(outRoot, `notes-${Date.now()}.csv`);
        const csv = 'text\n' + rows.map(r => '"' + String(r.text || '').replace(/"/g, '""') + '"').join('\n');
        const { writeFile } = await import('node:fs/promises'); await writeFile(csvPath, csv, 'utf-8');
        return { ok: true, count: rows.length, path: csvPath, keywords };
      });
      return { content: [{ type: "text", text: JSON.stringify(res) }] };
    }
  );

  // 发布笔记（草稿）
  const Publish = z.object({ dirId: DirId, imagePaths: z.array(z.string()).nonempty(), title: z.string(), content: z.string(), saveAsDraft: z.boolean().optional(), workspaceId: WorkspaceId });
  server.registerTool("xhs_publish_note", { description: "上传图片、填写标题和正文（默认保存草稿）", inputSchema: { dirId: DirId, imagePaths: z.array(z.string()), title: z.string(), content: z.string(), saveAsDraft: z.boolean().optional(), workspaceId: WorkspaceId } },
    async (input:any) => {
      const { dirId, imagePaths, title, content } = Publish.parse(getParams(input));
      const res = await policy.use(dirId, async () => {
        const { context } = await manager.get(dirId, { workspaceId: undefined });
        const page = await Pages.ensurePage(context, {});
        // 进入创作页
        await page.goto('https://www.xiaohongshu.com/explore/creation', { waitUntil: 'domcontentloaded' });
        // 上传图片
        const upload = await resolveLocatorAsync(page, { selector: 'input[type=file]' } as any);
        await upload.setInputFiles(imagePaths);
        // 填写标题与正文
        const titleBox = await resolveLocatorAsync(page, { placeholder: '标题' } as any);
        await clickHuman(page, titleBox); await typeHuman(titleBox, title, { wpm: 180 });
        const contentBox = await resolveLocatorAsync(page, { selector: '[contenteditable=true]' } as any);
        await clickHuman(page, contentBox); await typeHuman(contentBox, content, { wpm: 180 });
        // 保存草稿（默认行为）
        const saveBtn = await resolveLocatorAsync(page, { text: '保存草稿' } as any);
        await clickHuman(page, saveBtn);
        return { ok: true };
      });
      return { content: [{ type: "text", text: JSON.stringify(res) }] };
    }
  );
}

function zstringOptional() { return z.string().optional(); }
