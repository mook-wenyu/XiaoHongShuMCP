/* 诊断脚本：导出滚动各轮次的可视批候选列表（仅本地诊断使用）
 * 用法：
 *   tsx scripts/diagnose-candidates.ts --dirId=<id> --keywords=Gemini,Cursor --rounds=3 --deltaY=600 --settle=100
 *   可选：--target=explore|discover|search --search=关键词（当 target=search 时生效）
 */
import "dotenv/config";
import * as fs from "node:fs/promises";
import * as path from "node:path";
import { ConfigProvider } from "../src/config/ConfigProvider.js";
import { ServiceContainer } from "../src/core/container.js";
import { RoxyBrowserManager } from "../src/services/roxyBrowser.js";
import { ensurePage } from "../src/services/pages.js";
import { resolveContainerSelector, collectVisibleCards } from "../src/selectors/card.js";
import { detectPageType, ensureDiscoverPage, PageType } from "../src/domain/xhs/navigation.js";
import { cleanTextFor } from "../src/lib/text-clean.js";

function arg(name: string, def?: string) {
	const a = process.argv.find((x) => x.startsWith(`--${name}=`));
	return a ? a.split("=", 2)[1] : def;
}

async function navigateToTarget(page: any, target?: string, searchQuery?: string) {
	const t = (target || "").toLowerCase();
	if (!t) return;
	if (t === "explore") {
		await page.goto("https://www.xiaohongshu.com/explore", { waitUntil: "domcontentloaded" });
		return;
	}
	if (t === "discover") {
		await ensureDiscoverPage(page);
		return;
	}
	if (t === "search") {
		const q = encodeURIComponent(searchQuery || "");
		const url = q
			? `https://www.xiaohongshu.com/search_result?keyword=${q}`
			: "https://www.xiaohongshu.com/search_result";
		await page.goto(url, { waitUntil: "domcontentloaded" });
		return;
	}
}

async function main() {
	const dirId = arg("dirId") || process.env.ROXY_DIR_IDS?.split(",")[0] || "user";
	const keywordsStr = arg("keywords") || "Gemini,Cursor";
	const rounds = Number(arg("rounds") || 3);
	const deltaY = Number(arg("deltaY") || 600);
	const settle = Number(arg("settle") || 100);
	const workspaceId = arg("workspaceId");
	const target = arg("target");
	const searchQuery = arg("search");

	const cfg = ConfigProvider.load().getConfig();
	const container = new ServiceContainer(cfg as any, { loggerSilent: true });
	const manager: RoxyBrowserManager = container.createRoxyBrowserManager();
	const context = await manager.getContext(dirId, { workspaceId });
	const page = await ensurePage(context, {});

	// 若指定页面目标，先导航到目标页；否则保持当前页，不在三类页则进入 explore
	try {
		if (target) {
			await navigateToTarget(page, target, searchQuery || keywordsStr.split(",")[0] || "");
		} else {
			const pType = await detectPageType(page as any);
			if (
				pType !== PageType.ExploreHome &&
				pType !== PageType.Discover &&
				pType !== PageType.Search
			) {
				await page.goto("https://www.xiaohongshu.com/explore", { waitUntil: "domcontentloaded" });
			}
		}
	} catch {}

	const keywords = keywordsStr
		.split(",")
		.map((s) => s.trim())
		.filter(Boolean);
	const ts = Date.now();
	const outRoot = path.join(process.cwd(), "artifacts", dirId, "diagnostics", String(ts));
	await fs.mkdir(outRoot, { recursive: true }).catch(() => {});

	const normAll = async (pageType: string | undefined, arr: string[]) => {
		const out: string[] = [];
		for (const s of arr) out.push((await cleanTextFor(page as any, pageType, s)).toLowerCase());
		return out;
	};

	for (let r = 0; r < rounds; r++) {
		const pageTypeNow = String(await detectPageType(page as any));
		const sel = await resolveContainerSelector(page as any);
		const cards = await collectVisibleCards(page as any, sel);
		const normKeywords = await normAll(pageTypeNow, keywords);
		const normKeywordsNo = normKeywords.map((k) => k.replace(/\s+/g, ""));
		const items: any[] = [];
		for (const c of cards) {
			const titleNorm = (await cleanTextFor(page as any, pageTypeNow, c.title || "")).toLowerCase();
			const titleNo = titleNorm.replace(/\s+/g, "");
			const hitIdxs: number[] = [];
			normKeywords.forEach((nk, i) => {
				if (!nk) return;
				if (titleNorm.includes(nk) || titleNo.includes(normKeywordsNo[i])) hitIdxs.push(i);
			});
			items.push({
				index: c.index,
				y: (c as any).y,
				y2: (c as any).y2,
				noteId: c.noteId,
				href: (c as any).href,
				title: c.title,
				titleRaw: (c as any).titleRaw,
				coverAlt: (c as any).coverAlt,
				titleSource: (c as any).titleSource,
				titleNorm,
				hit: hitIdxs.length > 0,
				hitKeywords: hitIdxs.map((i) => keywords[i]),
			});
		}
		const out = {
			meta: {
				ts: Date.now(),
				round: r,
				url: page.url(),
				pageType: pageTypeNow,
				keywords,
				normKeywords,
				target: target || null,
			},
			summary: { candidates: items.length, hits: items.filter((x) => x.hit).length },
			items,
		};
		const file = path.join(outRoot, `candidates-round-${r}.json`);
		await fs.writeFile(file, JSON.stringify(out, null, 2), { encoding: "utf-8" });

		// 下一轮滚动
		if (r < rounds - 1) {
			await page.evaluate((dy) => window.scrollBy(0, Number(dy) || 0), deltaY);
			await page.waitForTimeout(Math.max(10, settle));
		}
	}

	// 输出结果目录
	process.stderr.write(JSON.stringify({ ok: true, dir: outRoot }, null, 2) + "\n");
}

main().catch((e) => {
	process.stderr.write(JSON.stringify({ ok: false, error: String(e?.message || e) }) + "\n");
	process.exit(1);
});
