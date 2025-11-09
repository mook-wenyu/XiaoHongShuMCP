/* 小红书笔记内容提取器
 * 职责：通过 API 拦截提取笔记的完整内容（标题、正文、标签、互动数据）
 * 原则：单一职责、高内聚、依赖注入
 */
import type { BrowserContext, Page } from "playwright";
import { waitNoteDetail, type NoteDetailData } from "./netwatch.js";
import { XHS_CONF } from "../../config/xhs.js";

/**
 * 笔记内容提取成功结果
 */
export interface NoteContentResult {
	note_id: string;
	url: string;
	title: string;
	content: string;
	tags: string[];
	author_nickname: string;
	interact_stats: {
		likes: number;
		collects: number;
		comments: number;
		shares: number;
	};
	extracted_at: string;
}

/**
 * 笔记内容提取失败结果
 */
export interface ExtractError {
	ok: false;
	code: string;
	message: string;
}

/**
 * 提取单个笔记的完整内容
 *
 * @param ctx - Playwright BrowserContext（用于创建新页面）
 * @param noteUrl - 笔记完整 URL
 * @returns 成功时返回 NoteContentResult，失败时返回 ExtractError
 *
 * 设计原则：
 * - KISS: 简单直接的流程，避免过度复杂
 * - SOLID: 单一职责，仅负责内容提取
 * - 高内聚: 所有提取逻辑集中在此函数
 * - 低耦合: 通过接口与外部交互，不依赖具体实现
 */
export async function extractNoteContent(
	ctx: BrowserContext,
	noteUrl: string,
): Promise<NoteContentResult | ExtractError> {
	let page: Page | null = null;

	try {
		// 创建新页面用于内容提取
		page = await ctx.newPage();

		const normalizedUrl = sanitizeNoteUrl(noteUrl);

		// 启动 API 监听器（必须在导航之前）
		const waiter = waitNoteDetail(page, XHS_CONF.feed.waitApiMs);

		// 导航到笔记页面，触发 API 请求
		await page.goto(noteUrl, { waitUntil: "domcontentloaded" });

		// 等待 API 响应
		const result = await waiter.promise;

		if (result && result.ok && result.data) {
			const detail: NoteDetailData = result.data;
			if (detail.note_id && detail.title) {
				return buildResultFromDetail(detail, noteUrl);
			}
		}

		// API 未命中或数据不完整 → DOM 兜底
		const domResult = await extractFromDom(page, normalizedUrl);
		if (domResult) return domResult;

		return {
			ok: false,
			code: "API_FAILED",
			message: "笔记详情 API 调用失败或无数据",
		};
	} catch (e: any) {
		// 统一错误处理
		return {
			ok: false,
			code: "EXTRACT_ERROR",
			message: String(e?.message || e),
		};
	} finally {
		// 资源清理：确保页面被关闭
		if (page) {
			try {
				await page.close();
			} catch {
				// 忽略关闭失败（可能已被 context 关闭）
			}
		}
	}
}

/**
 * 将 API 详情映射为标准返回
 */
function buildResultFromDetail(detail: NoteDetailData, url: string): NoteContentResult {
	// 附带 extraction_method=api 便于上游统计命中率（向前兼容，JSON 消费方可忽略额外字段）
	const base: any = {
		note_id: detail.note_id,
		url,
		title: detail.title,
		content: detail.desc,
		tags: detail.tags.map((t) => t.name),
		author_nickname: detail.author?.nickname || "",
		interact_stats: {
			likes: detail.interact_info?.liked_count || 0,
			collects: detail.interact_info?.collected_count || 0,
			comments: detail.interact_info?.comment_count || 0,
			shares: detail.interact_info?.share_count || 0,
		},
		extracted_at: new Date().toISOString(),
	};
	base.extraction_method = "api";
	return base as NoteContentResult;
}

/**
 * 规范化小红书笔记 URL：剔除易失参数，提取 noteId
 */
function sanitizeNoteUrl(url: string): string {
	try {
		const u = new URL(url);
		// 仅保留 explore 路径与必要查询参数（此处直接清理 xsec_*）
		u.searchParams.delete("xsec_token");
		u.searchParams.delete("xsec_source");
		return u.toString();
	} catch {
		return url;
	}
}

function extractNoteIdFromUrl(url: string): string {
	try {
		const u = new URL(url);
		const m = u.pathname.match(/\/explore\/([0-9a-z]+)/i);
		return m?.[1] || "";
	} catch {
		return "";
	}
}

/**
 * DOM 兜底解析（针对未触发 API 的导出链接等场景）
 */
async function extractFromDom(page: Page, url: string): Promise<NoteContentResult | null> {
	// 等待渲染完成的最小信号：标题或 noteContainer 渲染完成
	const maxWait = Math.max(Number(XHS_CONF.capture?.waitContentMs || 5000), 3000);
	try {
		await Promise.race([
			page.waitForSelector("#detail-title", { timeout: maxWait }),
			page.waitForSelector("#noteContainer", { timeout: maxWait }),
		]);
	} catch {
		// 页面未出现预期节点
		return null;
	}

	const data = await page.evaluate(() => {
		const pickText = (sel: string): string => {
			const el = document.querySelector(sel) as HTMLElement | null;
			return (el?.textContent || "").trim();
		};
		const pickTexts = (sel: string): string[] =>
			Array.from(document.querySelectorAll(sel))
				.map((el) => (el.textContent || "").trim())
				.filter(Boolean);

		// 标题
		const title = pickText("#detail-title") || pickText(".title");

		// 正文（多个段落拼接）
		const contentParts = Array.from(document.querySelectorAll("#detail-desc .note-text"))
			.map((el) => (el as HTMLElement).innerText?.trim() || "")
			.filter(Boolean);
		const content = contentParts.join("\n").replace(/\n{2,}/g, "\n");

		// 标签（去掉 # 前缀）
		const tags = Array.from(document.querySelectorAll("a.tag#hash-tag"))
			.map((a) => (a.textContent || "").replace(/^#/u, "").trim())
			.filter(Boolean);

		// 作者昵称
		const author = pickText(".author .username") || pickText(".name .username") || "";

		// 互动数据（多选择器兜底）
		const toInt = (s: string) => {
			const n = parseInt((s || "").replace(/[^0-9]/g, ""), 10);
			return Number.isFinite(n) ? n : 0;
		};
		const likes = toInt(pickText(".engage-bar .like .count")) || toInt(pickText(".like .count"));
		const collects =
			toInt(pickText(".collect-wrapper .count")) || toInt(pickText(".collect .count"));
		const comments = toInt(pickText(".chat .count")) || toInt(pickText(".comment .count"));
		const shares = 0; // 页面上分享数不一定直接可见，保留 0

		return { title, content, tags, author, likes, collects, comments, shares };
	});

	const noteId = extractNoteIdFromUrl(url);

	if (!data || !(data.title || data.content)) return null;

	const base: any = {
		note_id: noteId,
		url,
		title: data.title || "",
		content: data.content || "",
		tags: Array.isArray(data.tags) ? data.tags : [],
		author_nickname: data.author || "",
		interact_stats: {
			likes: data.likes || 0,
			collects: data.collects || 0,
			comments: data.comments || 0,
			shares: data.shares || 0,
		},
		extracted_at: new Date().toISOString(),
	};
	base.extraction_method = "dom";
	return base as NoteContentResult;
}
