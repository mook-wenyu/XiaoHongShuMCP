/* 中文注释：小红书笔记详情（模态）内的交互动作集合：点赞/收藏/评论/关注
 * 约束：
 * - 仅在“笔记详情页（模态弹窗）”存在时执行；否则返回错误。
 * - 所有点击统一走拟人化 clickHuman；仅点击“封面/标题之外”的局部控件：like/collect/follow/comment。
 * - 成功语义以 API 回执为准（like/collect/follow）；UI 更新仅作旁证，不作为成功判定前置条件。
 */
import type { Page, Locator } from "playwright";
import { clickHuman, typeHuman, hoverHuman } from "../../humanization/actions.js";
import { ERRORS } from "../../errors.js";
import { XHS_CONF } from "../../config/xhs.js";
import {
	waitLike,
	waitDislike,
	waitCollect,
	waitUncollect,
	waitFollow,
	waitUnfollow,
	waitComment,
} from "./netwatch.js";

export type Ok<T = any> = { ok: true } & T;
export type Fail = { ok: false; code: string; message?: string };

async function ensureNoteModalOpen(page: Page): Promise<boolean> {
	try {
		const count = await page
			.locator(
				'.note-detail-mask:visible, [role="dialog"]:visible, [aria-modal="true"]:visible, #noteContainer:visible, .note-container:visible',
			)
			.count();
		return count > 0;
	} catch {
		return false;
	}
}

function modalShellRoot(page: Page): Locator {
	// 优先选择“模态外壳”，若不存在则回退到内容容器；以单一多选择器统一处理
	return page
		.locator(
			'.note-detail-mask:visible, [role="dialog"]:visible, [aria-modal="true"]:visible, #noteContainer:visible, .note-container:visible',
		)
		.first();
}

// 已废弃：旧的内容容器定位（不再使用）

// 交互条常见容器集合（不同版本/AB 变体）
function engageScopes(page: Page): Locator[] {
	const shell = modalShellRoot(page);
	return [
		shell.locator(".interactions.engage-bar").first(),
		shell.locator(".engage-bar").first(),
		shell.locator(".engage-bar-container").first(),
		shell.locator(".buttons.engage-bar-style").first(), // 变体：按钮容器
	];
}

// 轻 hover + 微等待，降低可见性抖动风险
async function primeEngageArea(page: Page): Promise<void> {
	const scopes = engageScopes(page);
	for (const s of scopes) {
		try {
			if ((await s.count()) > 0) {
				await s.hover({ timeout: 300 });
				break;
			}
		} catch {}
	}
	try {
		await page.waitForTimeout(120);
	} catch {}
}

async function waitVisibleSoft(loc: Locator, timeout = 2000): Promise<void> {
	try {
		await loc.waitFor({ state: "visible", timeout });
	} catch {}
}

// 检测元素是否“可点”：可见 + 有有效的几何区域 + 命中测试点位
async function isClickable(page: Page, loc: Locator): Promise<boolean> {
	try {
		if (!(await loc.isVisible())) return false;
		const box = await loc.boundingBox().catch(() => null as any);
		if (!box || box.width < 2 || box.height < 2) return false;
		// 额外检查样式：display/visibility/opacity/pointer-events
		const handle = await loc.elementHandle().catch(() => null as any);
		if (!handle) return false;
		const styleOk = await page
			.evaluate((el: HTMLElement) => {
				const cs = window.getComputedStyle(el);
				if (cs.display === "none") return false;
				if (cs.visibility === "hidden" || cs.visibility === "collapse") return false;
				const op = parseFloat(cs.opacity || "1");
				if (op < 0.05) return false;
				if (cs.pointerEvents === "none") return false;
				return true;
			}, handle)
			.catch(() => false as any);
		if (!styleOk) return false;

		const cx = Math.floor(box.x + Math.max(1, box.width / 2));
		const cy = Math.floor(box.y + Math.max(1, box.height / 2));
		// 命中测试：元素中心在视口内，且 elementFromPoint 可追溯到该元素或其祖先
		const ok = await page
			.evaluate(
				([el, x, y]) => {
					const e = document.elementFromPoint(x as number, y as number);
					let node: any = e;
					const target = el as any;
					let depth = 0;
					while (node && depth < 8) {
						if (node === target) return true;
						node = node.parentElement;
						depth++;
					}
					return false;
				},
				[handle, cx, cy],
			)
			.catch(() => false as any);
		return !!ok;
	} catch {
		return false;
	}
}

// 封装“拟人化点击”：若不可点则轻微 hover 激活后重试；仍不可点则短软等待再试一次；最终兜底进行微偏移点按
async function clickHumanScoped(page: Page, loc: Locator): Promise<void> {
	// 第一次尝试
	if (!(await isClickable(page, loc))) {
		try {
			await hoverHuman(page, loc);
		} catch {}
		await waitVisibleSoft(loc, 500);
	}
	if (!(await isClickable(page, loc))) {
		try {
			await page.waitForTimeout(100);
		} catch {}
	}
	try {
		await clickHuman(page as any, loc);
	} catch {
		// 兜底：使用轻微偏移的原生点击（不改变拟人化默认，仅在极端重叠场景触发）
		try {
			const box = await loc.boundingBox();
			if (box) {
				const x = box.x + Math.max(2, Math.min(box.width - 2, Math.floor(box.width * 0.6)));
				const y = box.y + Math.max(2, Math.min(box.height - 2, Math.floor(box.height * 0.4)));
				await (page as any).mouse.move(x, y);
				await (page as any).mouse.down();
				await (page as any).mouse.up();
			}
		} catch {}
	}
}

function likeButton(page: Page): Locator {
	const bar = engageBarRoot(page);
	// 明确限定在 engage-bar 左侧区域，避免命中评论区点赞（常见 16px）
	return bar.locator('.left .like-wrapper:visible:has(svg[width="24"])').first();
}
function collectButton(page: Page): Locator {
	const bar = engageBarRoot(page);
	return bar.locator("#note-page-collect-board-guide:visible, .collect-wrapper:visible").first();
}
function followButton(page: Page): Locator {
	const shell = modalShellRoot(page);
	// 优先特征类名，其次按语义文本的按钮（关注/已关注）
	return shell
		.locator(
			'.note-detail-follow-btn .follow-button:visible, button:has-text("关注"), button:has-text("已关注")',
		)
		.first();
}
function commentEntry(page: Page): Locator {
	const bar = engageBarRoot(page);
	return bar.locator(".content-edit .inner-when-not-active:visible, .chat-wrapper:visible").first();
}
function commentInput(page: Page): Locator {
	const bar = engageBarRoot(page);
	return bar
		.locator(
			'#content-textarea[contenteditable="true"]:visible, .content-edit [contenteditable="true"]:visible',
		)
		.first();
}
function commentSubmit(page: Page): Locator {
	const bar = engageBarRoot(page);
	return bar.locator(".bottom .btn.submit:not([disabled]):visible").first();
}
// 已废弃：取消评论按钮查询（当前流程未使用）

export async function likeCurrent(page: Page): Promise<Ok<{ newLike?: boolean }> | Fail> {
	if (!(await ensureNoteModalOpen(page)))
		return { ok: false, code: ERRORS.MODAL_REQUIRED, message: "需要笔记详情模态已打开" };
	try {
		await primeEngageArea(page);
		const btn = likeButton(page);
		await waitVisibleSoft(btn);
		const w = waitLike(page, XHS_CONF.feed.waitApiMs);
		await clickHumanScoped(page, btn);
		const r = await w.promise;
		if (!r.ok) return { ok: false, code: "LIKE_FAILED", message: "点赞接口未返回成功" };
		return { ok: true, newLike: r.data?.new_like };
	} catch (e: any) {
		return { ok: false, code: "LIKE_ERROR", message: String(e?.message || e) };
	}
}
export async function unlikeCurrent(page: Page): Promise<Ok | Fail> {
	if (!(await ensureNoteModalOpen(page)))
		return { ok: false, code: ERRORS.MODAL_REQUIRED, message: "需要笔记详情模态已打开" };
	try {
		const w = waitDislike(page, XHS_CONF.feed.waitApiMs);
		await clickHumanScoped(page, likeButton(page));
		const r = await w.promise;
		if (!r.ok) return { ok: false, code: "UNLIKE_FAILED", message: "取消点赞接口未返回成功" };
		return { ok: true };
	} catch (e: any) {
		return { ok: false, code: "UNLIKE_ERROR", message: String(e?.message || e) };
	}
}

export async function collectCurrent(page: Page): Promise<Ok | Fail> {
	if (!(await ensureNoteModalOpen(page)))
		return { ok: false, code: ERRORS.MODAL_REQUIRED, message: "需要笔记详情模态已打开" };
	try {
		await primeEngageArea(page);
		const btn = collectButton(page);
		await waitVisibleSoft(btn);
		const w = waitCollect(page, XHS_CONF.feed.waitApiMs);
		await clickHumanScoped(page, btn);
		const r = await w.promise;
		if (!r.ok) return { ok: false, code: "COLLECT_FAILED", message: "收藏接口未返回成功" };
		return { ok: true };
	} catch (e: any) {
		return { ok: false, code: "COLLECT_ERROR", message: String(e?.message || e) };
	}
}
export async function uncollectCurrent(page: Page): Promise<Ok | Fail> {
	if (!(await ensureNoteModalOpen(page)))
		return { ok: false, code: ERRORS.MODAL_REQUIRED, message: "需要笔记详情模态已打开" };
	try {
		const w = waitUncollect(page, XHS_CONF.feed.waitApiMs);
		await clickHumanScoped(page, collectButton(page));
		const r = await w.promise;
		if (!r.ok) return { ok: false, code: "UNCOLLECT_FAILED", message: "取消收藏接口未返回成功" };
		return { ok: true };
	} catch (e: any) {
		return { ok: false, code: "UNCOLLECT_ERROR", message: String(e?.message || e) };
	}
}

export async function followAuthor(page: Page): Promise<Ok<{ fstatus?: string }> | Fail> {
	if (!(await ensureNoteModalOpen(page)))
		return { ok: false, code: ERRORS.MODAL_REQUIRED, message: "需要笔记详情模态已打开" };
	try {
		await primeEngageArea(page);
		const btn = followButton(page);
		await waitVisibleSoft(btn);
		// 若已关注则直接返回
		try {
			if ((await btn.textContent())?.includes("已关注")) return { ok: true, fstatus: "follows" };
		} catch {}
		const w = waitFollow(page, XHS_CONF.feed.waitApiMs);
		await clickHumanScoped(page, btn);
		const r = await w.promise;
		if (!r.ok) {
			// 降级：若 UI 已变更为“已关注”，则视为成功（并返回 fstatus=follows）
			try {
				if ((await btn.textContent())?.includes("已关注")) return { ok: true, fstatus: "follows" };
			} catch {}
			return { ok: false, code: "FOLLOW_FAILED", message: "关注接口未返回成功" };
		}
		return { ok: true, fstatus: r.data?.fstatus };
	} catch (e: any) {
		return { ok: false, code: "FOLLOW_ERROR", message: String(e?.message || e) };
	}
}
export async function unfollowAuthor(page: Page): Promise<Ok<{ fstatus?: string }> | Fail> {
	if (!(await ensureNoteModalOpen(page)))
		return { ok: false, code: ERRORS.MODAL_REQUIRED, message: "需要笔记详情模态已打开" };
	try {
		await primeEngageArea(page);
		const btn = followButton(page);
		await waitVisibleSoft(btn);
		// 若未关注则直接返回
		try {
			if (
				(await btn.textContent())?.includes("关注") &&
				!(await btn.textContent())?.includes("已关注")
			)
				return { ok: true, fstatus: "none" };
		} catch {}
		const w = waitUnfollow(page, XHS_CONF.feed.waitApiMs);
		await clickHumanScoped(page, btn);
		const r = await w.promise;
		if (!r.ok) {
			// 降级：若 UI 已变更为“关注”（且不含“已关注”），则视为取关成功
			try {
				const txt = await btn.textContent();
				if (txt && txt.includes("关注") && !txt.includes("已关注"))
					return { ok: true, fstatus: "none" };
			} catch {}
			return { ok: false, code: "UNFOLLOW_FAILED", message: "取消关注接口未返回成功" };
		}
		return { ok: true, fstatus: r.data?.fstatus };
	} catch (e: any) {
		return { ok: false, code: "UNFOLLOW_ERROR", message: String(e?.message || e) };
	}
}

export async function commentCurrent(
	page: Page,
	text: string,
): Promise<Ok<{ commentId?: string; noteId?: string; content?: string }> | Fail> {
	if (!(await ensureNoteModalOpen(page)))
		return { ok: false, code: ERRORS.MODAL_REQUIRED, message: "需要笔记详情模态已打开" };
	try {
		await primeEngageArea(page);
		const entry = commentEntry(page);
		if (await entry.isVisible().catch(() => false)) {
			await clickHumanScoped(page, entry);
			try {
				await page.waitForTimeout(80);
			} catch {}
		}
		const box = commentInput(page);
		await waitVisibleSoft(box);
		await clickHumanScoped(page, box);
		await typeHuman(box as any, text, { wpm: 180 });
		// 在点击发送前挂监听
		const waiter = waitComment(page, XHS_CONF.feed.waitApiMs);
		const submit = commentSubmit(page);
		await waitVisibleSoft(submit);
		if (await submit.isVisible().catch(() => false)) {
			await clickHumanScoped(page, submit);
		} else {
			// 回退：回车提交（若按钮不可见但输入框聚焦）
			try {
				await (box as any).press?.("Enter");
			} catch {}
		}
		const r = await waiter.promise;
		if (!r.ok) return { ok: false, code: ERRORS.COMMENT_FAILED, message: "评论接口未返回成功" };
		return { ok: true, commentId: r.data?.id, noteId: r.data?.note_id, content: r.data?.content };
	} catch (e: any) {
		return { ok: false, code: "COMMENT_ERROR", message: String(e?.message || e) };
	}
}

// 测试导出（仅单测使用）
export const __test = { isClickable, clickHumanScoped };

// 从模态根中选取首个可见的 engage-bar 容器，若不存在则回退到模态根（保证作用域不越界）
function engageBarRoot(page: Page): Locator {
	const shell = modalShellRoot(page);
	// 统一用多选择器合并，避免 .or 链式引发等待抖动；基于“模态外壳”而非 noteContainer，确保能命中兄弟节点的 engage-bar
	const candidate = shell
		.locator(
			".interactions.engage-bar, .engage-bar, .engage-bar-container, .buttons.engage-bar-style",
		)
		.first();
	return candidate;
}
