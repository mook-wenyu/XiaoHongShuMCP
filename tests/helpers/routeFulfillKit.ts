/* 中文注释：路由拦截工具（测试用）
 * 使用 Playwright 的 route.fulfill 注入本地夹带物，稳定端到端集成测试。
 */
import type { BrowserContext, Route } from "playwright";
import { readFile } from "node:fs/promises";
import { resolve as pathResolve } from "node:path";

export type XhsFixtures = {
    root: string; // 夹带根路径，例如 tests/fixtures/xhs
    variant?: "live" | "synthetic"; // 高保真快照或合成页面
};

async function fulfillHtml(route: Route, absPath: string) {
	const body = await readFile(absPath);
	await route.fulfill({ status: 200, body, contentType: "text/html; charset=utf-8" });
}

async function fulfillJson(route: Route, absPath: string) {
	const body = await readFile(absPath, { encoding: "utf-8" });
	await route.fulfill({ status: 200, body, contentType: "application/json; charset=utf-8" });
}

export async function installXhsRoutes(ctx: BrowserContext, fixtures: XhsFixtures) {
    const variant = fixtures.variant ?? (String(process.env.XHS_FIXTURE || "").toLowerCase() === "live" ? "live" : "synthetic");
    const file = (name: string) => {
        if (variant === "live") {
            if (name === "explore") return pathResolve(fixtures.root, "live", "explore.live.html");
            if (name === "search_result") return pathResolve(fixtures.root, "live", "search_result.live.html");
        }
        return pathResolve(fixtures.root, `${name}.html`);
    };

    // 发现页
    await ctx.route("https://www.xiaohongshu.com/explore", async (route) => {
        await fulfillHtml(route, file("explore"));
    });
    await ctx.route("https://www.xiaohongshu.com/explore?*", async (route) => {
        await fulfillHtml(route, file("explore"));
    });

    // 搜索结果页
    await ctx.route("https://www.xiaohongshu.com/search_result?*", async (route) => {
        await fulfillHtml(route, file("search_result"));
    });

    // API 回执（homefeed / search notes）
    await ctx.route(/\/api\/sns\/web\/v1\/homefeed.*/, async (route) => {
        await fulfillJson(route, pathResolve(fixtures.root, "homefeed.json"));
    });
    await ctx.route(/\/api\/sns\/web\/v1\/search\/notes.*/, async (route) => {
        await fulfillJson(route, pathResolve(fixtures.root, "search_notes.json"));
    });
}
