/* 中文注释：小红书快捷工具（语义化封装，基于原子动作组合）
 * 重构（Route 2）：本文件仅负责“工具注册与委派”，实际实现拆分至 ../shortcuts/* 子模块，提升内聚/可测/可维护性。
 */
import { z } from "zod";
import type { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import type { ServiceContainer } from "../../core/container.js";
import type { RoxyBrowserManager } from "../../services/roxyBrowser.js";

import { createCloseModalHandler, createNavigateDiscoverHandler } from "../shortcuts/navigation.js";
import { createSearchKeywordHandler } from "../shortcuts/search.js";
import { createCollectSearchResultsHandler } from "../shortcuts/collect.js";
import { createKeywordBrowseHandler } from "../shortcuts/browse.js";
import { createSelectNoteHandler } from "../shortcuts/select.js";
import { createLikeHandler, createUnlikeHandler, createCollectHandler, createUncollectHandler, createCommentPostHandler } from "../shortcuts/noteActions.js";
import { createFollowHandler, createUnfollowHandler } from "../shortcuts/userActions.js";

const DirId = z.string().min(1);
const WorkspaceId = z.string().optional();
const Keyword = z.string().min(1);
const CommentText = z.string().min(1);
const Keywords = z.array(z.string()).nonempty();

export function registerXhsShortcutsTools(
  server: McpServer,
  container: ServiceContainer,
  manager: RoxyBrowserManager,
) {
  // 关闭当前笔记模态
  server.registerTool(
    "xhs_close_modal",
    { description: "关闭当前页的笔记详情模态窗口（Esc→关闭按钮→遮罩 三级兜底）", inputSchema: { dirId: DirId, workspaceId: WorkspaceId } },
    createCloseModalHandler(container, manager) as any,
  );

  // 导航发现
  server.registerTool(
    "xhs_navigate_discover",
    { description: "导航到小红书发现页（推荐流），使用语义化选择器与轻量 API 软校验", inputSchema: { dirId: DirId, workspaceId: WorkspaceId } },
    createNavigateDiscoverHandler(container, manager) as any,
  );

  // 搜索 / 收集 / 浏览
  server.registerTool(
    "xhs_search_keyword",
    { description: "在当前上下文中按关键词进行站内搜索（拟人化输入 + 软校验）", inputSchema: { dirId: DirId, keyword: Keyword, workspaceId: WorkspaceId } },
    createSearchKeywordHandler(container, manager) as any,
  );
  server.registerTool(
    "xhs_collect_search_results",
    { description: "在站内搜索后收集前N条笔记结果（优先 API 回执，DOM 兜底）", inputSchema: { dirId: DirId, keyword: Keyword, limit: z.number().optional(), workspaceId: WorkspaceId } },
    createCollectSearchResultsHandler(container, manager) as any,
  );
  server.registerTool(
    "xhs_keyword_browse",
    { description: "按多个关键词进行搜索并进行轻量滚动浏览（不做业务动作）", inputSchema: { dirId: DirId, keywords: Keywords, workspaceId: WorkspaceId } },
    createKeywordBrowseHandler(container, manager) as any,
  );

  // 选择笔记
  server.registerTool(
    "xhs_select_note",
    { description: "在首页/发现/搜索页内按关键词匹配卡片→点击→等待模态；若不在该三类页面则先导航到发现页再执行匹配", inputSchema: { dirId: DirId, keywords: Keywords, workspaceId: WorkspaceId } },
    createSelectNoteHandler(container, manager) as any,
  );

  // 笔记动作
  server.registerTool(
    "xhs_note_like",
    { description: "对当前笔记（需要模态打开）执行点赞，结果以接口回执为准", inputSchema: { dirId: DirId, workspaceId: WorkspaceId } },
    createLikeHandler(container, manager) as any,
  );
  server.registerTool(
    "xhs_note_unlike",
    { description: "对当前笔记（需要模态打开）取消点赞，结果以接口回执为准", inputSchema: { dirId: DirId, workspaceId: WorkspaceId } },
    createUnlikeHandler(container, manager) as any,
  );
  server.registerTool(
    "xhs_note_collect",
    { description: "对当前笔记（需要模态打开）执行收藏，结果以接口回执为准", inputSchema: { dirId: DirId, workspaceId: WorkspaceId } },
    createCollectHandler(container, manager) as any,
  );
  server.registerTool(
    "xhs_note_uncollect",
    { description: "对当前笔记（需要模态打开）取消收藏，结果以接口回执为准", inputSchema: { dirId: DirId, workspaceId: WorkspaceId } },
    createUncollectHandler(container, manager) as any,
  );

  // 发表评论
  server.registerTool(
    "xhs_comment_post",
    { description: "在当前笔记详情模态中发表评论（监听接口回执确认成功）", inputSchema: { dirId: DirId, text: CommentText, workspaceId: WorkspaceId } },
    createCommentPostHandler(container, manager) as any,
  );

  // 用户动作
  server.registerTool(
    "xhs_user_follow",
    { description: "关注当前笔记作者（需要模态打开）", inputSchema: { dirId: DirId, workspaceId: WorkspaceId } },
    createFollowHandler(container, manager) as any,
  );
  server.registerTool(
    "xhs_user_unfollow",
    { description: "取消关注当前笔记作者（需要模态打开）", inputSchema: { dirId: DirId, workspaceId: WorkspaceId } },
    createUnfollowHandler(container, manager) as any,
  );
}
