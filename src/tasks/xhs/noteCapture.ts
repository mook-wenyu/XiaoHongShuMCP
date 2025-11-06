/* 中文注释：小红书笔记抓取（HTML+截图+质量验证） */
import type { BrowserContext } from "playwright";
import { promises as fs } from "node:fs";
import { join } from "node:path";
import {
	progressiveWait,
	validateCaptureQuality,
	type CaptureQuality,
	type CaptureMetadata,
} from "../../services/scraper/quality.js";
import { createLogger } from "../../logging/index.js";

const log = createLogger();

export async function noteCapture(
	ctx: BrowserContext,
	dirId: string,
	url: string,
	outDir = "artifacts",
): Promise<{
	htmlPath: string;
	pngPath: string;
	quality: CaptureQuality;
	metadata: CaptureMetadata;
}> {
	const startTime = Date.now();
	const page = await ctx.newPage();

	await page.goto(url, { waitUntil: "domcontentloaded" });

	// 使用渐进式等待策略
	await progressiveWait(page);

	// 验证抓取质量
	const quality = await validateCaptureQuality(page);

	// 如果质量分数过低，记录警告
	if (quality.score < 50) {
		log.warn({ url, quality }, "抓取内容质量较低，可能需要调整等待策略");
	}

	// 保存 HTML 和截图
	const html = await page.content();
	const dir = join(outDir, dirId, "notes");
	await fs.mkdir(dir, { recursive: true });

	const ts = Date.now();
	const htmlPath = join(dir, `note-${ts}.html`);
	const pngPath = join(dir, `note-${ts}.png`);

	await fs.writeFile(htmlPath, html, "utf-8");
	await page.screenshot({ path: pngPath, fullPage: true });

	await page.close();

	const waitTimeMs = Date.now() - startTime;

	const metadata: CaptureMetadata = {
		timestamp: new Date().toISOString(),
		url,
		quality,
		waitTimeMs,
	};

	log.info(
		{
			url,
			quality: quality.score,
			htmlLength: quality.htmlLength,
			waitTimeMs,
		},
		"笔记抓取完成",
	);

	return { htmlPath, pngPath, quality, metadata };
}
