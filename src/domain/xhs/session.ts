/* 中文注释：小红书会话检查（非侵入式）
 * 策略：优先通过 Context cookies 判断是否存在 xiaohongshu.com 相关 Cookie；
 * 可选：若 cookies 为空，尝试打开首页并等待最小内容，再次检查。
 * 注意：不使用敏感选择器；该方法仅作为弱信号。
 */
import type { BrowserContext } from "playwright";

export async function checkSession(ctx: BrowserContext) {
	const hasCookie = (domains: string[], cookies: Awaited<ReturnType<BrowserContext["cookies"]>>) =>
		cookies.some((c) => domains.some((d) => (c.domain || "").includes(d)));

	let cookies = await ctx.cookies();
	let ok = hasCookie(["xiaohongshu.com", "xhs.com"], cookies);
	if (ok) return { loggedIn: true, via: "cookies" };

	// 退一步：轻探测首页，不依赖选择器
	try {
		const page = await ctx.newPage();
		await page.goto("https://www.xiaohongshu.com", { waitUntil: "domcontentloaded" });
		try {
			await page.waitForLoadState("networkidle", { timeout: 5000 });
		} catch {}
		cookies = await ctx.cookies();
		await page.close();
		ok = hasCookie(["xiaohongshu.com", "xhs.com"], cookies);
		return { loggedIn: ok, via: "cookies+home" };
	} catch {
		return { loggedIn: false, via: "error" };
	}
}
