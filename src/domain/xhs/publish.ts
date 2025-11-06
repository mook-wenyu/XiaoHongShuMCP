/* 中文注释：XHS 领域占位任务：创建草稿（仅示意，不含具体选择器与流程） */
import type { BrowserContext } from "playwright";

export async function createDraft(ctx: BrowserContext, _payload: { title?: string; content?: string; images?: string[] }) {
	const page = await ctx.newPage();
	try {
		// TODO: 进入发布页面、填写内容/上传图片、保存草稿。
		return { ok: false, reason: "NOT_IMPLEMENTED" };
	} finally {
		await page.close();
	}
}
