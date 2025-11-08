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

		// 启动 API 监听器（必须在导航之前）
		const waiter = waitNoteDetail(page, XHS_CONF.feed.waitApiMs);

		// 导航到笔记页面，触发 API 请求
		await page.goto(noteUrl, { waitUntil: "domcontentloaded" });

		// 等待 API 响应
		const result = await waiter.promise;

		// 检查 API 调用是否成功
		if (!result.ok || !result.data) {
			return {
				ok: false,
				code: "API_FAILED",
				message: "笔记详情 API 调用失败或无数据",
			};
		}

		const detail: NoteDetailData = result.data;

		// 验证必要字段
		if (!detail.note_id || !detail.title) {
			return {
				ok: false,
				code: "INVALID_DATA",
				message: "笔记数据不完整（缺少 note_id 或 title）",
			};
		}

		// 构建标准化结果
		return {
			note_id: detail.note_id,
			url: noteUrl,
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
