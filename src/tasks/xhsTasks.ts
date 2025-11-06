/* 中文注释：小红书示例任务集合（占位：打开URL并截图） */
import type { BrowserContext } from "playwright";
import { promises as fs } from "node:fs";
import { join } from "node:path";

export async function openAndScreenshot(
	ctx: BrowserContext,
	profileId: string,
	url: string,
	outDir = "artifacts",
) {
	const page = await ctx.newPage();
	await page.goto(url, { waitUntil: "domcontentloaded" });
	const dir = join(outDir, profileId);
	await fs.mkdir(dir, { recursive: true });
	const file = join(dir, `screenshot-${Date.now()}.png`);
	await page.screenshot({ path: file, fullPage: true });
	await page.close();
	return file;
}
