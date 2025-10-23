/* 中文：最小 Playwright 打开小红书（降级方案，Chromium） */
import { chromium } from "playwright";

(async () => {
	const url = process.argv[2] || "https://www.xiaohongshu.com";
	const browser = await chromium.launch({ headless: false });
	const context = await browser.newContext();
	const page = await context.newPage();
	await page.goto(url, { waitUntil: "domcontentloaded" });
	// 保持窗口开启，用户手动关闭即可；若需自动退出可调整等待时间
	await page.waitForTimeout(60 * 60 * 1000);
	await browser.close();
})().catch((err) => {
	console.error("open-xhs 错误:", err);
	process.exit(1);
});
