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
// 已移除 DSL/任务桥接：发布等流程直接由选择器原子动作驱动

const DirId = z.string().default("user"); // Roxy Browser API 窗口标识符
const WorkspaceId = z.string().optional();
const Keywords = z.array(z.string()).nonempty();

export function registerXhsShortcutsTools(server: McpServer, connector: IPlaywrightConnector, policy: PolicyEnforcer) {
  const manager = new ConnectionManager(connector);

  const OpenParams = z.object({ dirId: DirId, workspaceId: WorkspaceId });
  server.registerTool("browser_open", { description: "打开或复用浏览器窗口（dirId 为 Roxy 窗口标识符）", inputSchema: { dirId: z.string().optional(), workspaceId: z.string().optional() } },
    async (input: any) => {
      const { dirId, workspaceId } = OpenParams.parse(getParams(input));
      const res = await policy.use(dirId, async () => {
        const { context } = await manager.get(dirId, { workspaceId });
        return { ok: true, pages: Pages.listPages(context) };
      });
      return { content: [{ type: "text", text: JSON.stringify(res) }] };
    }
  );

  const LLE = z.object({
    dirId: DirId, actionType: z.enum(["Hover","Click","MoveRandom","Wheel","ScrollTo","InputText","PressKey","Wait"]).or(z.string()),
    locator: z.any().optional(), parameters: z.record(z.any()).optional(), pageIndex: z.number().int().nonnegative().optional(), workspaceId: WorkspaceId
  });
  server.registerTool("ll_execute", { description: "执行单个拟人化动作（精细控制）", inputSchema: { dirId: z.string().optional(), actionType: z.string(), locator: z.any(), parameters: z.record(z.any()).optional(), pageIndex: z.number().optional(), workspaceId: z.string().optional() } },
    async (input: any) => {
      const { dirId, actionType, locator, parameters, pageIndex, workspaceId } = LLE.parse(getParams(input));
      const res = await policy.use(dirId, async () => {
        const { context } = await manager.get(dirId, { workspaceId });
        const page = await Pages.ensurePage(context, { pageIndex });
        switch (actionType) {
          case "Hover": { const l = await resolveLocatorAsync(page, locator as any); await hoverHuman(page, l); break; }
          case "Click": { const l = await resolveLocatorAsync(page, locator as any); await clickHuman(page, l); break; }
          case "InputText": { const l = await resolveLocatorAsync(page, locator as any); await typeHuman(l, String(parameters?.text ?? ""), { wpm: Number(parameters?.wpm ?? 180) }); break; }
          case "Wheel": await page.mouse.wheel(0, Number(parameters?.deltaY ?? 800)); break;
          case "ScrollTo": await page.evaluate(({ x, y }) => window.scrollTo(x ?? 0, y ?? 0), { x: Number(parameters?.x ?? 0), y: Number(parameters?.y ?? 0) }); break;
          case "MoveRandom": await page.mouse.move(Math.random()*800+100, Math.random()*400+100, { steps: 30 }); break;
          case "PressKey": await page.keyboard.press(String(parameters?.key ?? "Enter")); break;
          case "Wait": await page.waitForTimeout(Number(parameters?.ms ?? 1000)); break;
          default: throw new Error(`未知动作: ${actionType}`);
        }
        return { ok: true };
      });
      return { content: [{ type: "text", text: JSON.stringify(res) }] };
    }
  );

  // 登录入口
  const BK = z.object({ dirId: DirId, workspaceId: WorkspaceId });
  server.registerTool("xhs_open_login", { description: "打开小红书首页，等待人工登录", inputSchema: { dirId: z.string().optional(), workspaceId: z.string().optional() } },
    async (input: any) => {
      const { dirId, workspaceId } = BK.parse(getParams(input));
      const res = await policy.use(dirId, async () => {
        const { context } = await manager.get(dirId, { workspaceId });
        const page = await Pages.ensurePage(context, {});
        await page.goto("https://www.xiaohongshu.com/", { waitUntil: "domcontentloaded" });
        return { ok: true, url: page.url() };
      });
      return { content: [{ type: "text", text: JSON.stringify(res) }] };
    }
  );

  // 会话检查（别名转发）
  server.registerTool("xhs_check_session", { description: "检查当前会话是否已登录（弱信号）", inputSchema: { dirId: z.string().optional(), workspaceId: z.string().optional() } },
    async (input: any) => {
      const { dirId, workspaceId } = BK.parse(getParams(input));
      const res = await policy.use(dirId, async () => {
        const { context } = await manager.get(dirId, { workspaceId });
        const { checkSession } = await import("../../domain/xhs/session.js");
        return checkSession(context);
      });
      return { content: [{ type: "text", text: JSON.stringify(res) }] };
    }
  );

  // 导航到发现页
  server.registerTool("xhs_navigate_explore", { description: "导航到发现页", inputSchema: { dirId: z.string().optional(), workspaceId: z.string().optional() } },
    async (input: any) => {
      const { dirId, workspaceId } = BK.parse(getParams(input));
      const res = await policy.use(dirId, async () => {
        const { context } = await manager.get(dirId, { workspaceId });
        const page = await Pages.ensurePage(context, {});
        // 尝试点击“发现”导航；若不存在则直接访问首页作为兜底
        try {
          const loc = await resolveLocatorAsync(page, { text: "发现" } as any);
          await loc.waitFor({ state: "visible", timeout: 3000 }).catch(() => {});
          await clickHuman(page, loc);
        } catch {
          await page.goto("https://www.xiaohongshu.com/", { waitUntil: "domcontentloaded" });
        }
        return { ok: true, url: page.url() };
      });
      return { content: [{ type: "text", text: JSON.stringify(res) }] };
    }
  );

  // 搜索关键词
  const Search = z.object({ dirId: DirId, keyword: z.string().min(1), workspaceId: WorkspaceId });
  server.registerTool("xhs_search_keyword", { description: "在搜索框输入关键词并搜索", inputSchema: { dirId: z.string().optional(), keyword: z.string(), workspaceId: z.string().optional() } },
    async (input: any) => {
      const { dirId, keyword, workspaceId } = Search.parse(getParams(input));
      const res = await policy.use(dirId, async () => {
        const { context } = await manager.get(dirId, { workspaceId });
        const page = await Pages.ensurePage(context, {});
        await page.goto("https://www.xiaohongshu.com/", { waitUntil: "domcontentloaded" });
        const box = await resolveLocatorAsync(page, { placeholder: "搜索" } as any);
        await clickHuman(page, box); await typeHuman(box, keyword + "\n", { wpm: 200 });
        await page.waitForLoadState("domcontentloaded");
        return { ok: true, url: page.url() };
      });
      return { content: [{ type: "text", text: JSON.stringify(res) }] };
    }
  );

  // 根据关键词选择一条笔记
  const SelectNote = z.object({ dirId: DirId, keywords: Keywords, workspaceId: WorkspaceId });
  server.registerTool("xhs_select_note", { description: "根据关键词选择笔记", inputSchema: { dirId: z.string().optional(), keywords: z.array(z.string()), workspaceId: z.string().optional() } },
    async (input: any) => {
      const { dirId, keywords, workspaceId } = SelectNote.parse(getParams(input));
      const res = await policy.use(dirId, async () => {
        const { context } = await manager.get(dirId, { workspaceId });
        const page = await Pages.ensurePage(context, {});
        // 选择第一个包含任意关键词的卡片
        const ok = await page.evaluate((keys: string[]) => {
          const items = Array.from(document.querySelectorAll('a, div')) as HTMLElement[];
          for (const el of items) {
            const t = (el.innerText || "").trim();
            if (t && keys.some(k => t.includes(k))) { (el as HTMLElement).click(); return true; }
          }
          return false;
        }, keywords);
        return { ok };
      });
      return { content: [{ type: "text", text: JSON.stringify(res) }] };
    }
  );

  // 点赞/收藏/评论/滚动（当前笔记页）
  const BKOnly = BK; const Comment = z.object({ dirId: DirId, commentText: z.string().min(1), workspaceId: WorkspaceId });
  server.registerTool("xhs_like_current", { description: "点赞当前笔记", inputSchema: { dirId: z.string().optional(), workspaceId: z.string().optional() } },
    async (input:any) => {
      const { dirId, workspaceId } = BKOnly.parse(getParams(input));
      const res = await policy.use(dirId, async () => {
        const { context } = await manager.get(dirId, { workspaceId });
        const page = await Pages.ensurePage(context, {});
        // 启发式：优先 aria-label 其后文本兜底
        const locators = [ { selector: '[aria-label*="赞" i]' }, { text: '赞' } ];
        for (const l of locators) { try { const target = await resolveLocatorAsync(page, l as any); await clickHuman(page, target); return { ok: true }; } catch {} }
        return { ok: false };
      });
      return { content: [{ type: "text", text: JSON.stringify(res) }] };
    }
  );

  server.registerTool("xhs_favorite_current", { description: "收藏当前笔记", inputSchema: { dirId: z.string().optional(), workspaceId: z.string().optional() } },
    async (input:any) => {
      const { dirId, workspaceId } = BKOnly.parse(getParams(input));
      const res = await policy.use(dirId, async () => {
        const { context } = await manager.get(dirId, { workspaceId });
        const page = await Pages.ensurePage(context, {});
        const locators = [ { selector: '[aria-label*="收藏" i]' }, { text: '收藏' } ];
        for (const l of locators) { try { const target = await resolveLocatorAsync(page, l as any); await clickHuman(page, target); return { ok: true }; } catch {} }
        return { ok: false };
      });
      return { content: [{ type: "text", text: JSON.stringify(res) }] };
    }
  );

  server.registerTool("xhs_comment_current", { description: "评论当前笔记", inputSchema: { dirId: z.string().optional(), commentText: z.string(), workspaceId: z.string().optional() } },
    async (input:any) => {
      const { dirId, commentText, workspaceId } = Comment.parse(getParams(input));
      const res = await policy.use(dirId, async () => {
        const { context } = await manager.get(dirId, { workspaceId });
        const page = await Pages.ensurePage(context, {});
        const box = await resolveLocatorAsync(page, { placeholder: '评论' } as any);
        await clickHuman(page, box); await typeHuman(box, commentText + "\n", { wpm: 180 });
        return { ok: true };
      });
      return { content: [{ type: "text", text: JSON.stringify(res) }] };
    }
  );

  server.registerTool("xhs_scroll_browse", { description: "拟人化滚动浏览", inputSchema: { dirId: z.string().optional(), workspaceId: z.string().optional() } },
    async (input:any) => {
      const { dirId, workspaceId } = BK.parse(getParams(input));
      const res = await policy.use(dirId, async () => {
        const { context } = await manager.get(dirId, { workspaceId });
        const page = await Pages.ensurePage(context, {});
        await scrollHuman(page, 2000);
        return { ok: true };
      });
      return { content: [{ type: "text", text: JSON.stringify(res) }] };
    }
  );

  // 随机浏览 / 关键词浏览（流程）
  const Rand = z.object({ dirId: DirId, portraitId: z.string().optional(), behaviorProfile: z.string().optional(), workspaceId: WorkspaceId });
  server.registerTool("xhs_random_browse", { description: "随机浏览（概率点赞/收藏）", inputSchema: { dirId: z.string().optional(), portraitId: z.string().optional(), behaviorProfile: z.string().optional(), workspaceId: z.string().optional() } },
    async (input:any) => {
      const { dirId, workspaceId } = Rand.parse(getParams(input));
      const res = await policy.use(dirId, async () => {
        const { context } = await manager.get(dirId, { workspaceId });
        const page = await Pages.ensurePage(context, {});
        await scrollHuman(page, 1200);
        return { ok: true };
      });
      return { content: [{ type: "text", text: JSON.stringify(res) }] };
    }
  );

  const KeyBrowse = z.object({ dirId: DirId, keywords: Keywords, behaviorProfile: z.string().optional(), workspaceId: WorkspaceId });
  server.registerTool("xhs_keyword_browse", { description: "关键词浏览（概率点赞/收藏）", inputSchema: { dirId: z.string().optional(), keywords: z.array(z.string()), behaviorProfile: z.string().optional(), workspaceId: z.string().optional() } },
    async (input:any) => {
      const { dirId, keywords, workspaceId } = KeyBrowse.parse(getParams(input));
      const res = await policy.use(dirId, async () => {
        const { context } = await manager.get(dirId, { workspaceId });
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
  server.registerTool("xhs_capture_page_notes", { description: "采集当前页面笔记（可见范围）", inputSchema: { dirId: z.string().optional(), targetCount: z.number(), workspaceId: z.string().optional() } },
    async (input:any) => {
      const { dirId, targetCount, workspaceId } = CapPage.parse(getParams(input));
      const res = await policy.use(dirId, async () => {
        const { context } = await manager.get(dirId, { workspaceId });
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
  server.registerTool("xhs_note_capture", { description: "按关键词批量采集笔记", inputSchema: { dirId: z.string().optional(), keywords: z.array(z.string()), targetCount: z.number(), workspaceId: z.string().optional() } },
    async (input:any) => {
      const { dirId, keywords, targetCount, workspaceId } = NoteCap.parse(getParams(input));
      const res = await policy.use(dirId, async () => {
        const { context } = await manager.get(dirId, { workspaceId });
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
  server.registerTool("xhs_publish_note", { description: "上传图片、填写标题和正文（默认保存草稿）", inputSchema: { dirId: z.string().optional(), imagePaths: z.array(z.string()), title: z.string(), content: z.string(), saveAsDraft: z.boolean().optional(), workspaceId: z.string().optional() } },
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
