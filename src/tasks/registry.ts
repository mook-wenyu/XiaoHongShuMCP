/* 中文注释：任务注册中心，统一任务签名 (ctx, dirId, args) */
import type { BrowserContext } from "playwright";
import { openAndScreenshot } from "./xhsTasks.js";
import { noteCapture } from "./xhs/noteCapture.js";
import { publishDraft } from "./xhs/publish.js";
// removed interact DSL

export type TaskArgs = Record<string, any>;
export type TaskFn<T = unknown> = (
	ctx: BrowserContext,
	dirId: string,
	args?: TaskArgs,
) => Promise<T>;

const registry: Record<string, TaskFn> = {
	openAndScreenshot: async (ctx, dirId, args) => {
		const url = args?.url || "https://example.com";
		return openAndScreenshot(ctx, dirId, url);
	},
	"xhs.checkSession": async (ctx) => {
		const { checkSession } = await import("../domain/xhs/session.js");
		return checkSession(ctx);
	},
	"xhs.noteCapture": async (ctx, dirId, args) => {
		const url = args?.url || "https://example.com";
		return noteCapture(ctx, dirId, url);
	},
	"xhs.publish": async (ctx, dirId, args) => publishDraft(ctx, dirId, args),
};

export function runTaskByName(name: string): TaskFn {
	const fn = registry[name];
	if (!fn) throw new Error(`未知任务：${name}`);
	return fn;
}

export function listTasks() {
	return Object.keys(registry).map((name) => ({
		name,
		description: descriptions[name] ?? "",
		params: params[name] ?? {},
	}));
}

const descriptions: Record<string, string> = {
	openAndScreenshot: "打开 URL 并截图（同上下文多页）",
	"xhs.checkSession": "检查会话（Cookies 启发式）",
	"xhs.noteCapture": "抓取小红书笔记 HTML+截图",
	"xhs.publish": "发布草稿（选择器直驱）",
};

const params: Record<string, any> = {
	openAndScreenshot: { url: { type: "string", required: true } },
	"xhs.noteCapture": { url: { type: "string", required: true } },
	"xhs.publish": {
		url: { type: "string" },
		images: { type: "string[]" },
		title: { type: "string" },
		content: { type: "string" },
		selectorMap: { type: "object" },
		submit: { type: "boolean" },
	},
};
