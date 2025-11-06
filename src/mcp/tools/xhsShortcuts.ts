/* 中文注释：小红书快捷工具（语义化封装，基于原子动作组合）
 * 设计边界：
 * - 仅提供稳定的语义动作（关闭模态/导航发现/关键词搜索）；不内置业务流程编排与反检测逻辑。
 * - 复用现有原子能力：page.* 工具、人类化动作、selectors 韧性与 domain 封装。
 * - 与新架构对齐：直接使用 RoxyBrowserManager 获取持久化 Context。
 */
import { z } from "zod";
import type { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import type { ServiceContainer } from "../../core/container.js";
import type { RoxyBrowserManager } from "../../services/roxyBrowser.js";
import * as Pages from "../../services/pages.js";
import { ok } from "../utils/result.js";
import { err } from "../utils/errors.js";
import { XHS_CONF } from "../../config/xhs.js";
import { resolveLocatorResilient } from "../../selectors/index.js";
import { XhsSelectors } from "../../selectors/xhs.js";
import { clickHuman, hoverHuman, scrollHuman } from "../../humanization/actions.js";

const DirId = z.string().min(1);
const WorkspaceId = z.string().optional();
const Keyword = z.string().min(1);
const Keywords = z.array(z.string()).nonempty();

function escapeRegExp(s: string): string {
	return s.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

async function screenshotOnError(page: any, dirId: string, tag: string) {
	try {
		const { ensureDir, pathJoin } = await import("../../services/artifacts.js");
		const outRoot = pathJoin("artifacts", dirId, "navigation");
		await ensureDir(outRoot);
		const path = pathJoin(outRoot, `${tag}-${Date.now()}.png`);
		await page.screenshot({ path, fullPage: true }).catch(() => {});
		return path;
	} catch {
		return undefined;
	}
}

export function registerXhsShortcutsTools(
	server: McpServer,
	container: ServiceContainer,
	manager: RoxyBrowserManager,
) {
	// 关闭当前笔记模态（若存在）
	server.registerTool(
		"xhs_close_modal",
		{
			description: "关闭当前页的笔记详情模态窗口（Esc→关闭按钮→遮罩 三级兜底）",
			inputSchema: { dirId: DirId, workspaceId: WorkspaceId },
		},
		async (input: any) => {
			try {
				const { dirId, workspaceId } = input as any;
				const context = await manager.getContext(dirId, { workspaceId });
				const page = await Pages.ensurePage(context, {});
				const { closeModalIfOpen } = await import("../../domain/xhs/navigation.js");
				const closed = await closeModalIfOpen(page);
				return { content: [{ type: "text", text: JSON.stringify(ok({ closed })) }] };
			} catch (e: any) {
				return {
					content: [
						{ type: "text", text: JSON.stringify(err("INTERNAL_ERROR", String(e?.message || e))) },
					],
				};
			}
		},
	);

	// 导航到发现页（个性化推荐流）
	server.registerTool(
		"xhs_navigate_discover",
		{
			description: "导航到小红书发现页（推荐流），使用语义化选择器与轻量 API 软校验",
			inputSchema: { dirId: DirId, workspaceId: WorkspaceId },
		},
		async (input: any) => {
			let page: any | undefined;
			try {
				const { dirId, workspaceId } = input as any;
				const context = await manager.getContext(dirId, { workspaceId });
				page = await Pages.ensurePage(context, {});

				const { ensureDiscoverPage } = await import("../../domain/xhs/navigation.js");
				await ensureDiscoverPage(page);

				// 软校验：homefeed 回执（不作为强制错误）
				let feedItems: number | undefined;
				let feedTtfbMs: number | undefined;
				let verified = false;
				try {
					const { waitHomefeed } = await import("../../domain/xhs/netwatch.js");
					const w = waitHomefeed(page, XHS_CONF.feed.waitApiMs);
					const r = await w.promise;
					feedItems = Array.isArray((r as any).data?.items)
						? (r as any).data.items.length
						: undefined;
					feedTtfbMs = r.ttfbMs;
					verified = !!r.ok;
				} catch {}

				return {
					content: [
						{
							type: "text",
							text: JSON.stringify(
								ok({ target: "discover", url: page.url(), verified, feedItems, feedTtfbMs }),
							),
						},
					],
				};
			} catch (e: any) {
				const { dirId } = input as any;
				const shot = page
					? await screenshotOnError(page, String(dirId ?? "unknown"), "navigate-discover-error")
					: undefined;
				return {
					content: [
						{
							type: "text",
							text: JSON.stringify(
								err("NAVIGATE_FAILED", String(e?.message || e), { screenshotPath: shot }),
							),
						},
					],
				};
			}
		},
	);

	// 关键词搜索（进入搜索结果页并尝试监听 search.notes 回执）
	server.registerTool(
		"xhs_search_keyword",
		{
			description: "在当前上下文中按关键词进行站内搜索（拟人化输入 + 软校验）",
			inputSchema: { dirId: DirId, keyword: Keyword, workspaceId: WorkspaceId },
		},
		async (input: any) => {
			let page: any | undefined;
			try {
				const { dirId, keyword, workspaceId } = input as any;
				const context = await manager.getContext(dirId, { workspaceId });
				page = await Pages.ensurePage(context, {});
				const { searchKeyword } = await import("../../domain/xhs/search.js");
				const r = await searchKeyword(page, String(keyword));
				return { content: [{ type: "text", text: JSON.stringify(ok(r)) }] };
			} catch (e: any) {
				const { dirId } = input as any;
				const shot = page
					? await screenshotOnError(page, String(dirId ?? "unknown"), "search-keyword-error")
					: undefined;
				return {
					content: [
						{
							type: "text",
							text: JSON.stringify(
								err("INTERNAL_ERROR", String(e?.message || e), { screenshotPath: shot }),
							),
						},
					],
				};
			}
		},
	);

	// 关键词浏览（轻量滚动，提升可见文本覆盖）
	server.registerTool(
		"xhs_keyword_browse",
		{
			description: "按多个关键词进行搜索并进行轻量滚动浏览（不做业务动作）",
			inputSchema: { dirId: DirId, keywords: Keywords, workspaceId: WorkspaceId },
		},
		async (input: any) => {
			let page: any | undefined;
			try {
				const { dirId, keywords, workspaceId } = input as any;
				const context = await manager.getContext(dirId, { workspaceId });
				page = await Pages.ensurePage(context, {});
				const { searchKeyword } = await import("../../domain/xhs/search.js");
				const key = (Array.isArray(keywords) ? keywords : [keywords])
					.map((s: any) => String(s || "").trim())
					.filter(Boolean)
					.join(" ");
				const t0 = Date.now();
				const r = await searchKeyword(page, key);
				const searchTimeMs = Date.now() - t0;
				// 浏览：滚动 2~3 段以扩大可见文本覆盖
				const tBrowse0 = Date.now();
				try {
					await scrollHuman(page, XHS_CONF.scroll.step);
					await page.waitForTimeout(XHS_CONF.scroll.shortSearchWaitMs);
					await scrollHuman(page, Math.floor(XHS_CONF.scroll.step * 0.8));
				} catch {}
				const browseTimeMs = Date.now() - tBrowse0;
				return {
					content: [
						{
							type: "text",
							text: JSON.stringify(
								ok({
									...r,
									browsed: true,
									metrics: {
										search: {
											timeMs: searchTimeMs,
											verified: !!(r as any).verified,
											matchedCount: (r as any).matchedCount,
										},
										browse: { steps: 2, timeMs: browseTimeMs },
										url: page.url(),
									},
								}),
							),
						},
					],
				};
			} catch (e: any) {
				const { dirId } = input as any;
				const shot = page
					? await screenshotOnError(page, String(dirId ?? "unknown"), "keyword-browse-error")
					: undefined;
				return {
					content: [
						{
							type: "text",
							text: JSON.stringify(
								err("INTERNAL_ERROR", String(e?.message || e), { screenshotPath: shot }),
							),
						},
					],
				};
			}
		},
	);

	// 根据关键词选择并打开一条笔记（点击卡片锚点→等待笔记模态）
	server.registerTool(
		"xhs_select_note",
		{
			description: "根据多个关键词匹配并打开一条笔记（搜索→匹配卡片→点击→等待模态）",
			inputSchema: { dirId: DirId, keywords: Keywords, workspaceId: WorkspaceId },
		},
		async (input: any) => {
			let page: any | undefined;
			try {
				const { dirId, keywords, workspaceId } = input as any;
				const keys: string[] = (Array.isArray(keywords) ? keywords : [keywords])
					.map((s: any) => String(s || "").trim())
					.filter(Boolean);
				if (!keys.length)
					return {
						content: [
							{ type: "text", text: JSON.stringify(err("INVALID_PARAMS", "keywords 不能为空")) },
						],
					};

				const context = await manager.getContext(dirId, { workspaceId });
				page = await Pages.ensurePage(context, {});

				// 1) 进行搜索（用第一个关键词）
				let searchOk: boolean | undefined;
				let searchVerified: boolean | undefined;
				let searchMatchedCount: number | undefined;
				let searchTimeMs: number | undefined;
				try {
					const { searchKeyword } = await import("../../domain/xhs/search.js");
					const t0 = Date.now();
					const r = await searchKeyword(page, keys[0]);
					searchTimeMs = Date.now() - t0;
					searchOk = !!(r as any).ok;
					searchVerified = !!(r as any).verified;
					searchMatchedCount = (r as any).matchedCount;
				} catch {}

				// 2) 定位候选锚点并筛选包含任一关键词的卡片
				const regex = new RegExp(keys.map(escapeRegExp).join("|"), "i");
				let target: any | undefined;
				let rounds = 0;
				let candidateCount: number | undefined;
				let filteredCount: number | undefined;
				let filterTimeMs: number | undefined;
				let hrefBefore: string | undefined;
				let titleBefore: string | undefined;
				let matchedKeywords: number | undefined;
				let clickToModalMs: number | undefined;
				for (let round = 0; round < 4 && !target; round++) {
					rounds++;
					try {
						const tFilter0 = Date.now();
						const anchors = await resolveLocatorResilient(page as any, XhsSelectors.noteAnchor(), {
							selectorId: "note-anchor",
							verifyTimeoutMs: 1200,
							retryAttempts: 2,
							skipHealthMonitor: false,
						});
						try {
							candidateCount = await anchors.count();
						} catch {}
						const filtered = anchors.filter({ hasText: regex });
						const count = await filtered.count();
						filteredCount = count;
						filterTimeMs = Date.now() - tFilter0;
						if (count > 0) {
							target = filtered.first();
							try {
								hrefBefore = (await target.getAttribute("href")) ?? undefined;
							} catch {}
							try {
								titleBefore = ((await target.textContent()) || "").trim() || undefined;
							} catch {}
							try {
								const lower = (titleBefore || "").toLowerCase();
								matchedKeywords = keys.reduce(
									(n, k) => n + (lower.includes(String(k).toLowerCase()) ? 1 : 0),
									0,
								);
							} catch {}
						} else {
							// 滚动一段后重试（加载更多卡片）
							try {
								await scrollHuman(page as any, XHS_CONF.scroll.step);
							} catch {}
							try {
								await page.waitForTimeout(XHS_CONF.scroll.shortSearchWaitMs);
							} catch {}
						}
					} catch {
						// 解析失败也尝试小滚动后重试
						try {
							await scrollHuman(page as any, Math.floor(XHS_CONF.scroll.step * 0.8));
						} catch {}
						try {
							await page.waitForTimeout(300);
						} catch {}
					}
				}

				// 3) 点击并等待笔记模态出现
				if (target) {
					try {
						await target.scrollIntoViewIfNeeded();
					} catch {}
					try {
						await hoverHuman(page as any, target);
					} catch {}
					const tClick = Date.now();
					await clickHuman(page as any, target);
					try {
						const mask = await resolveLocatorResilient(page as any, XhsSelectors.noteModalMask(), {
							selectorId: "note-modal-mask",
							verifyTimeoutMs: 2000,
							retryAttempts: 2,
							skipHealthMonitor: false,
						});
						await mask.first().waitFor({ state: "visible", timeout: 5000 });
						clickToModalMs = Date.now() - tClick;
					} catch {}
				}

				const opened = await (async () => {
					try {
						const mask = await resolveLocatorResilient(page as any, XhsSelectors.noteModalMask(), {
							selectorId: "note-modal-mask-check",
							verifyTimeoutMs: 800,
							retryAttempts: 1,
							skipHealthMonitor: true,
						});
						return (await mask.count()) > 0;
					} catch {
						return false;
					}
				})();

				// 提取 noteId（从 href 解析）
				let noteId: string | undefined;
				try {
					const href = hrefBefore || "";
					const m = href.match(
						/(?:\/explore\/|\/discovery\/item\/|\/search_result\/|\/note\/)([^\/?#]+)/,
					);
					if (m && m[1]) noteId = m[1];
				} catch {}

				return {
					content: [
						{
							type: "text",
							text: JSON.stringify(
								ok({
									opened,
									url: page.url(),
									noteId,
									title: titleBefore,
									matchMode: "any",
									matchedKeywords,
									metrics: {
										search: {
											ok: searchOk,
											verified: searchVerified,
											matchedCount: searchMatchedCount,
											timeMs: searchTimeMs,
										},
										select: { rounds, candidateCount, filteredCount, filterTimeMs },
										timing: { clickToModalMs },
									},
								}),
							),
						},
					],
				};
			} catch (e: any) {
				const { dirId } = input as any;
				const shot = page
					? await screenshotOnError(page, String(dirId ?? "unknown"), "select-note-error")
					: undefined;
				return {
					content: [
						{
							type: "text",
							text: JSON.stringify(
								err("INTERNAL_ERROR", String(e?.message || e), { screenshotPath: shot }),
							),
						},
					],
				};
			}
		},
	);

	// ========== 笔记交互：点赞/取消点赞/收藏/取消收藏 ==========
	server.registerTool(
		"xhs_note_like",
		{
			description: "对当前笔记（需要模态打开）执行点赞，结果以接口回执为准",
			inputSchema: { dirId: DirId, workspaceId: WorkspaceId },
		},
		async (input: any) => {
			let page: any | undefined;
			try {
				const { dirId, workspaceId } = input as any;
				const context = await manager.getContext(dirId, { workspaceId });
				page = await Pages.ensurePage(context, {});
				const { likeCurrent } = await import("../../domain/xhs/noteActions.js");
				const r = await likeCurrent(page);
				return {
					content: [
						{
							type: "text",
							text: JSON.stringify(
								r.ok ? ok(r) : err("ACTION_FAILED", r.message || "like failed", r),
							),
						},
					],
				};
			} catch (e: any) {
				const shot = page
					? await screenshotOnError(
							page,
							String((input as any)?.dirId ?? "unknown"),
							"note-like-error",
						)
					: undefined;
				return {
					content: [
						{
							type: "text",
							text: JSON.stringify(
								err("INTERNAL_ERROR", String(e?.message || e), { screenshotPath: shot }),
							),
						},
					],
				};
			}
		},
	);

	server.registerTool(
		"xhs_note_unlike",
		{
			description: "对当前笔记（需要模态打开）取消点赞，结果以接口回执为准",
			inputSchema: { dirId: DirId, workspaceId: WorkspaceId },
		},
		async (input: any) => {
			let page: any | undefined;
			try {
				const { dirId, workspaceId } = input as any;
				const context = await manager.getContext(dirId, { workspaceId });
				page = await Pages.ensurePage(context, {});
				const { unlikeCurrent } = await import("../../domain/xhs/noteActions.js");
				const r = await unlikeCurrent(page);
				return {
					content: [
						{
							type: "text",
							text: JSON.stringify(
								r.ok ? ok(r) : err("ACTION_FAILED", r.message || "unlike failed", r),
							),
						},
					],
				};
			} catch (e: any) {
				const shot = page
					? await screenshotOnError(
							page,
							String((input as any)?.dirId ?? "unknown"),
							"note-unlike-error",
						)
					: undefined;
				return {
					content: [
						{
							type: "text",
							text: JSON.stringify(
								err("INTERNAL_ERROR", String(e?.message || e), { screenshotPath: shot }),
							),
						},
					],
				};
			}
		},
	);

	server.registerTool(
		"xhs_note_collect",
		{
			description: "对当前笔记（需要模态打开）执行收藏，结果以接口回执为准",
			inputSchema: { dirId: DirId, workspaceId: WorkspaceId },
		},
		async (input: any) => {
			let page: any | undefined;
			try {
				const { dirId, workspaceId } = input as any;
				const context = await manager.getContext(dirId, { workspaceId });
				page = await Pages.ensurePage(context, {});
				const { collectCurrent } = await import("../../domain/xhs/noteActions.js");
				const r = await collectCurrent(page);
				return {
					content: [
						{
							type: "text",
							text: JSON.stringify(
								r.ok ? ok(r) : err("ACTION_FAILED", r.message || "collect failed", r),
							),
						},
					],
				};
			} catch (e: any) {
				const shot = page
					? await screenshotOnError(
							page,
							String((input as any)?.dirId ?? "unknown"),
							"note-collect-error",
						)
					: undefined;
				return {
					content: [
						{
							type: "text",
							text: JSON.stringify(
								err("INTERNAL_ERROR", String(e?.message || e), { screenshotPath: shot }),
							),
						},
					],
				};
			}
		},
	);

	server.registerTool(
		"xhs_note_uncollect",
		{
			description: "对当前笔记（需要模态打开）取消收藏，结果以接口回执为准",
			inputSchema: { dirId: DirId, workspaceId: WorkspaceId },
		},
		async (input: any) => {
			let page: any | undefined;
			try {
				const { dirId, workspaceId } = input as any;
				const context = await manager.getContext(dirId, { workspaceId });
				page = await Pages.ensurePage(context, {});
				const { uncollectCurrent } = await import("../../domain/xhs/noteActions.js");
				const r = await uncollectCurrent(page);
				return {
					content: [
						{
							type: "text",
							text: JSON.stringify(
								r.ok ? ok(r) : err("ACTION_FAILED", r.message || "uncollect failed", r),
							),
						},
					],
				};
			} catch (e: any) {
				const shot = page
					? await screenshotOnError(
							page,
							String((input as any)?.dirId ?? "unknown"),
							"note-uncollect-error",
						)
					: undefined;
				return {
					content: [
						{
							type: "text",
							text: JSON.stringify(
								err("INTERNAL_ERROR", String(e?.message || e), { screenshotPath: shot }),
							),
						},
					],
				};
			}
		},
	);

	// ========== 用户交互：关注/取关 ==========
	server.registerTool(
		"xhs_user_follow",
		{
			description: "关注当前笔记作者（需要模态打开）",
			inputSchema: { dirId: DirId, workspaceId: WorkspaceId },
		},
		async (input: any) => {
			let page: any | undefined;
			try {
				const { dirId, workspaceId } = input as any;
				const context = await manager.getContext(dirId, { workspaceId });
				page = await Pages.ensurePage(context, {});
				const { followAuthor } = await import("../../domain/xhs/noteActions.js");
				const r = await followAuthor(page);
				return {
					content: [
						{
							type: "text",
							text: JSON.stringify(
								r.ok ? ok(r) : err("ACTION_FAILED", r.message || "follow failed", r),
							),
						},
					],
				};
			} catch (e: any) {
				const shot = page
					? await screenshotOnError(
							page,
							String((input as any)?.dirId ?? "unknown"),
							"user-follow-error",
						)
					: undefined;
				return {
					content: [
						{
							type: "text",
							text: JSON.stringify(
								err("INTERNAL_ERROR", String(e?.message || e), { screenshotPath: shot }),
							),
						},
					],
				};
			}
		},
	);

	server.registerTool(
		"xhs_user_unfollow",
		{
			description: "取消关注当前笔记作者（需要模态打开）",
			inputSchema: { dirId: DirId, workspaceId: WorkspaceId },
		},
		async (input: any) => {
			let page: any | undefined;
			try {
				const { dirId, workspaceId } = input as any;
				const context = await manager.getContext(dirId, { workspaceId });
				page = await Pages.ensurePage(context, {});
				const { unfollowAuthor } = await import("../../domain/xhs/noteActions.js");
				const r = await unfollowAuthor(page);
				return {
					content: [
						{
							type: "text",
							text: JSON.stringify(
								r.ok ? ok(r) : err("ACTION_FAILED", r.message || "unfollow failed", r),
							),
						},
					],
				};
			} catch (e: any) {
				const shot = page
					? await screenshotOnError(
							page,
							String((input as any)?.dirId ?? "unknown"),
							"user-unfollow-error",
						)
					: undefined;
				return {
					content: [
						{
							type: "text",
							text: JSON.stringify(
								err("INTERNAL_ERROR", String(e?.message || e), { screenshotPath: shot }),
							),
						},
					],
				};
			}
		},
	);

	// ========== 发表评论 ==========
	server.registerTool(
		"xhs_comment_post",
		{
			description: "在当前笔记模态内发表评论（拟人化输入 + 接口软校验）",
			inputSchema: { dirId: DirId, text: Keyword, workspaceId: WorkspaceId },
		},
		async (input: any) => {
			let page: any | undefined;
			try {
				const { dirId, text, workspaceId } = input as any;
				const context = await manager.getContext(dirId, { workspaceId });
				page = await Pages.ensurePage(context, {});
				const { commentCurrent } = await import("../../domain/xhs/noteActions.js");
				const r = await commentCurrent(page, String(text));
				return {
					content: [
						{
							type: "text",
							text: JSON.stringify(
								r.ok ? ok(r) : err("ACTION_FAILED", r.message || "comment failed", r),
							),
						},
					],
				};
			} catch (e: any) {
				const shot = page
					? await screenshotOnError(
							page,
							String((input as any)?.dirId ?? "unknown"),
							"comment-post-error",
						)
					: undefined;
				return {
					content: [
						{
							type: "text",
							text: JSON.stringify(
								err("INTERNAL_ERROR", String(e?.message || e), { screenshotPath: shot }),
							),
						},
					],
				};
			}
		},
	);
}
