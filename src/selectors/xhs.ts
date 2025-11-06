/* 中文注释：小红书页面常用元素的语义化选择器映射
 * 目标：集中维护、真实元素为准、提供多候选回退链，便于在 UI 改版时统一调整。
 */
import type { TargetHints } from "./types.js";

// 搜索输入框：按优先顺序提供多种可行语义
export const XhsSelectors = {
	searchInput(): TargetHints {
		return {
			alternatives: [
				// ARIA/可访问性名称（Playwright 推荐）
				{ role: "textbox", name: { contains: "搜索小红书" } },
				// placeholder（当前 DOM 见用户提供 HTML 片段）
				{ placeholder: "搜索小红书" },
				// id+class（较稳定，作为兜底）
				{ selector: "#search-input.search-input" },
				// 容器内的 input
				{ selector: ".input-box input.search-input" },
			],
		};
	},

	searchSubmit(): TargetHints {
		return {
			alternatives: [
				{ role: "button", name: { regex: "^(搜索|查找|Search)$" } },
				{ selector: ".input-box .input-button" },
				{ selector: ".input-box .search-icon" },
			],
		};
	},

	// 顶部导航中的“发现”
	navDiscover(): TargetHints {
		return {
			alternatives: [
				{ role: "link", name: { exact: "发现" } },
				{ text: { exact: "发现" } },
				{ selector: "a[href*=\"channel_id=homefeed_recommend\"]" },
			],
		};
	},

	// 卡片锚点：用于点击打开笔记详情（模态）
	noteAnchor(): TargetHints {
		return {
			alternatives: [
				// 搜索结果页：/search_result/{noteId}
				{ selector: "a[href*=\"/search_result/\"]" },
				// 搜索列表：/discovery/item/{noteId}
				{ selector: "a[href*=\"/discovery/item/\"]" },
				// 发现/探索：/explore/{noteId}
				{ selector: "a[href^=\"/explore/\"]" },
				// 兜底：包含 note 路由的链接
				{ selector: "a[href*=\"/note\"], a[href*=\"/explore?\"]" },
			],
		};
	},

	// 笔记详情模态相关
	noteModalMask(): TargetHints {
		return {
			alternatives: [
				{ selector: ".note-detail-mask" },
				{ selector: "[role=dialog]" },
				{ selector: "[aria-modal=true]" },
				{ selector: "#noteContainer" },
				{ selector: ".note-container" },
			],
		};
	},

	// 关闭按钮候选（Esc 仍由流程层先行尝试）
	noteModalClose(): TargetHints {
		return {
			alternatives: [
				{ role: "button", name: { contains: "关闭" } },
				{ selector: "button[aria-label*=关闭 i]" },
				{ selector: "[data-testid*=close i]" },
				{ selector: "[class*=close i]" },
				{ selector: ".close-mask-dark" },
				{ selector: ".close-box" },
				{ selector: "svg:has(use[xlink\:href=\"#close\"])" },
			],
		};
	},
};
