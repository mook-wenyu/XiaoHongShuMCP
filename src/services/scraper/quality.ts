/* 中文注释：HTML 抓取质量验证和渐进式等待策略 */
import type { Page } from "playwright";
import { XHS_CONF } from "../../config/xhs.js";
import { createLogger } from "../../logging/index.js";

const log = createLogger();

export interface CaptureQuality {
	htmlLength: number; // HTML 总长度
	contentLength: number; // 纯文本内容长度
	hasTitle: boolean; // 是否有标题
	hasImages: boolean; // 是否有图片
	score: number; // 质量分数 0-100
}

export interface CaptureMetadata {
	timestamp: string; // ISO 8601 时间戳
	url: string; // 页面 URL
	quality: CaptureQuality; // 质量评分
	waitTimeMs: number; // 等待时间（毫秒）
}

/**
 * 验证抓取内容质量
 * @param page Playwright Page 对象
 * @returns 质量评分对象
 */
export async function validateCaptureQuality(page: Page): Promise<CaptureQuality> {
	const html = await page.content();
	const htmlLength = html.length;

	// 使用 page.evaluate 在浏览器上下文中执行
	const metrics = await page.evaluate(() => {
		const body = document.body;
		const contentLength = body?.innerText?.length || 0;
		const hasTitle = !!document.title && document.title.trim().length > 0;
		const hasImages = document.querySelectorAll("img").length > 0;

		return { contentLength, hasTitle, hasImages };
	});

	// 计算质量分数（0-100）
	let score = 0;

	// HTML 长度贡献 25 分（> 10000 字节）
	if (htmlLength > 10000) {
		score += 25;
	} else if (htmlLength > 5000) {
		score += 15;
	} else if (htmlLength > 1000) {
		score += 5;
	}

	// 内容长度贡献 35 分（> minContentLength）
	const minContentLength = XHS_CONF.capture.minContentLength;
	if (metrics.contentLength > minContentLength * 2) {
		score += 35;
	} else if (metrics.contentLength > minContentLength) {
		score += 25;
	} else if (metrics.contentLength > minContentLength / 2) {
		score += 10;
	}

	// 标题贡献 20 分
	if (metrics.hasTitle) {
		score += 20;
	}

	// 图片贡献 20 分
	if (metrics.hasImages) {
		score += 20;
	}

	return {
		htmlLength,
		contentLength: metrics.contentLength,
		hasTitle: metrics.hasTitle,
		hasImages: metrics.hasImages,
		score,
	};
}

/**
 * 渐进式等待策略（不阻塞流程）
 * @param page Playwright Page 对象
 */
export async function progressiveWait(page: Page): Promise<void> {
	const startTime = Date.now();

	// 阶段 1：等待网络空闲（最多 waitNetworkIdleMs）
	try {
		await page.waitForLoadState("networkidle", {
			timeout: XHS_CONF.capture.waitNetworkIdleMs,
		});
		log.debug({ durationMs: Date.now() - startTime }, "网络空闲等待完成");
	} catch {
		log.debug({ durationMs: Date.now() - startTime }, "网络空闲等待超时（继续执行）");
	}

	// 阶段 2：等待内容长度达标（最多 waitContentMs）
	try {
		const minContentLength = XHS_CONF.capture.minContentLength;
		await page.waitForFunction(
			(minLen) => {
				const body = document.body;
				return body && body.innerText && body.innerText.length > minLen;
			},
			minContentLength,
			{ timeout: XHS_CONF.capture.waitContentMs },
		);
		log.debug({ durationMs: Date.now() - startTime }, "内容长度等待完成");
	} catch {
		log.debug({ durationMs: Date.now() - startTime }, "内容长度等待超时（继续执行）");
	}

	const totalWaitMs = Date.now() - startTime;
	log.debug({ totalWaitMs }, "渐进式等待完成");
}
