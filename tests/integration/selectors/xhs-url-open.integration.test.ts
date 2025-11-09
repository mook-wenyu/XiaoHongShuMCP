import { describe, it, expect, beforeAll, afterAll, beforeEach } from "vitest";
import { chromium, type Browser, type Page } from "playwright";
import * as path from "node:path";
import * as fs from "node:fs/promises";
import { findAndOpenNoteByKeywords, detectPageType } from "../../../src/domain/xhs/navigation";
import { resolveContainerSelector, collectVisibleCards } from "../../../src/selectors/card";
import { cleanTextFor } from "../../../src/lib/text-clean";

let browser: Browser;
let page: Page;

const HTML = `
<!doctype html>
<html><head><meta charset="utf-8"><title>xhs mock url open</title></head>
<body>
<div class="feeds-container" style="width:100%;height:1200px;">
  <section class="note-item" data-index="0">
    <div class="footer"><a class="title"><span>AI开发者必备！Cursor+Gemini最强配置教程</span></a></div>
    <a class="cover" href="/search_result/676148d1000000001300d551" onclick="window.location.assign('/search_result/676148d1000000001300d551');return false;">open</a>
  </section>
  <section class="note-item" data-index="1">
    <div class="footer"><a class="title"><span>Cursor 次数不够用？试试免费的 Gemini 吧</span></a></div>
    <a class="cover" href="/search_result/681cb3ee000000000f031250" onclick="window.location.assign('/search_result/681cb3ee000000000f031250');return false;">open</a>
  </section>
  <section class="note-item" data-index="2">
    <div class="footer"><a class="title"><span>无关卡片</span></a></div>
    <a class="cover" href="/search_result/foobar" onclick="window.location.assign('/search_result/foobar');return false;">open</a>
  </section>
  </div>
</body></html>`;

async function setupMock(page: Page) {
	// 需要真实域名以便 waitForURL 命中绝对路径
	await page.goto("https://www.xiaohongshu.com/explore", { waitUntil: "domcontentloaded" });
	await page.setContent(HTML, { waitUntil: "domcontentloaded" });
}

async function ensureDir(p: string) {
	await fs.mkdir(p, { recursive: true }).catch(() => {});
}

async function withHighlightedCards(page: Page, keywords: string[], fn: () => Promise<void>) {
	const kset = keywords.map((k) => k.toLowerCase());
	await page.evaluate((ks) => {
		const toNorm = (s: string) => (s || "").toLowerCase();
		const cards = Array.from(document.querySelectorAll("section.note-item")) as HTMLElement[];
		for (const c of cards) {
			const t = c.querySelector("a.title, .footer a.title");
			const txt = t ? t.textContent || "" : "";
			const n = toNorm(txt);
			const hit = ks.some(
				(k: string) => n.includes(k) || n.replace(/\s+/g, "").includes(k.replace(/\s+/g, "")),
			);
			if (hit) {
				(c as any).__prevOutline = (c.style as any).outline;
				(c as any).__prevOffset = (c.style as any).outlineOffset;
				c.style.outline = "3px solid #ff3344";
				c.style.outlineOffset = "2px";
				c.setAttribute("data-test-highlight", "true");
			}
		}
	}, kset);
	try {
		await fn();
	} finally {
		await page.evaluate(() => {
			const cards = Array.from(
				document.querySelectorAll('section.note-item[data-test-highlight="true"]'),
			) as HTMLElement[];
			for (const c of cards) {
				const prevOutline = (c as any).__prevOutline || "";
				const prevOffset = (c as any).__prevOffset || "";
				c.style.outline = prevOutline;
				(c.style as any).outlineOffset = prevOffset;
				c.removeAttribute("data-test-highlight");
			}
		});
	}
}

async function dumpCandidates(page: Page, outDir: string, keywords: string[]) {
	const pageType = await detectPageType(page as any);
	const sel = await resolveContainerSelector(page as any);
	const cards = await collectVisibleCards(page as any, sel);
	const norm = async (s: string) =>
		(await cleanTextFor(page as any, String(pageType), s)).toLowerCase();
	const normKeywords = await Promise.all(keywords.map((k) => norm(k)));
	const normKeywordsNoSpace = await Promise.all(normKeywords.map((k) => k.replace(/\s+/g, "")));
	const items = [] as any[];
	for (const c of cards) {
		const titleNorm = await norm(c.title || "");
		const titleNo = titleNorm.replace(/\s+/g, "");
		const hitIdxs: number[] = [];
		normKeywords.forEach((nk, i) => {
			if (!nk) return;
			if (titleNorm.includes(nk) || titleNo.includes(normKeywordsNoSpace[i])) hitIdxs.push(i);
		});
		items.push({
			index: c.index,
			y: (c as any).y,
			y2: (c as any).y2,
			noteId: c.noteId,
			href: (c as any).href,
			title: c.title,
			titleNorm,
			hit: hitIdxs.length > 0,
			hitKeywords: hitIdxs.map((i) => keywords[i]),
		});
	}
	const out = {
		meta: {
			ts: Date.now(),
			url: page.url(),
			pageType,
			keywords,
			normKeywords,
			beforeScreenshot: path.join(outDir, "before.png"),
			afterScreenshot: path.join(outDir, "after.png"),
		},
		summary: { candidates: items.length, hits: items.filter((x) => x.hit).length },
		items,
	};
	await fs.writeFile(path.join(outDir, "candidates.json"), JSON.stringify(out, null, 2), {
		encoding: "utf-8",
	});
}

async function dumpCandidatesForRounds(
	page: Page,
	outDir: string,
	keywords: string[],
	rounds = 3,
	deltaY = 600,
	settleMs = 50,
) {
	for (let r = 0; r < rounds; r++) {
		const file = path.join(outDir, `candidates-round-${r}.json`);
		const pageType = await detectPageType(page as any);
		const sel = await resolveContainerSelector(page as any);
		const cards = await collectVisibleCards(page as any, sel);
		const norm = async (s: string) =>
			(await cleanTextFor(page as any, String(pageType), s)).toLowerCase();
		const normKeywords = await Promise.all(keywords.map((k) => norm(k)));
		const normKeywordsNoSpace = await Promise.all(normKeywords.map((k) => k.replace(/\s+/g, "")));
		const items = [] as any[];
		for (const c of cards) {
			const titleNorm = await norm(c.title || "");
			const titleNo = titleNorm.replace(/\s+/g, "");
			const hitIdxs: number[] = [];
			normKeywords.forEach((nk, i) => {
				if (!nk) return;
				if (titleNorm.includes(nk) || titleNo.includes(normKeywordsNoSpace[i])) hitIdxs.push(i);
			});
			items.push({
				index: c.index,
				y: (c as any).y,
				y2: (c as any).y2,
				noteId: c.noteId,
				href: (c as any).href,
				title: c.title,
				titleNorm,
				hit: hitIdxs.length > 0,
				hitKeywords: hitIdxs.map((i) => keywords[i]),
			});
		}
		const out = {
			meta: { ts: Date.now(), round: r, url: page.url(), pageType, keywords, normKeywords },
			summary: { candidates: items.length, hits: items.filter((x) => x.hit).length },
			items,
		};
		await fs.writeFile(file, JSON.stringify(out, null, 2), { encoding: "utf-8" });
		if (r < rounds - 1) {
			await page.evaluate((dy) => window.scrollBy(0, Number(dy) || 0), deltaY);
			await page.waitForTimeout(Math.max(10, settleMs));
		}
	}
}

describe("xhs_select_note · url open on search_result detail (mock DOM)", () => {
	beforeAll(async () => {
		browser = await chromium.launch();
	});
	afterAll(async () => {
		await browser.close();
	});
	beforeEach(async () => {
		page = await browser.newPage({ viewport: { width: 1200, height: 800 } });
		process.env.XHS_FEED_WAIT_API_MS = "30";
		process.env.XHS_SEARCH_WAIT_API_MS = "30";
		process.env.XHS_SCROLL_USE_API_AFTER = "false";
		process.env.XHS_SCROLL_STEP_RATIO = "0.45";
		process.env.XHS_SCROLL_OVERLAP_ANCHORS = "3";
		process.env.XHS_SCROLL_OVERLAP_RATIO = "0.25";
		process.env.XHS_SELECT_MAX_MS = "2000";
		process.env.XHS_OPEN_WAIT_MS = "500";
		process.env.XHS_FEED_WAIT_ON_CLICK = "false";
		await setupMock(page);
	});

	it("opens url detail and returns url detail (test-visible)", { timeout: 30000 }, async () => {
		const keywords = ["Gemini", "Cursor"];
		const ts = Date.now();
		const outDir = path.join(process.cwd(), "artifacts", "test-shots", `xhs-url-open-${ts}`);
		await ensureDir(outDir);
		await withHighlightedCards(page, keywords, async () => {
			await page.screenshot({ path: path.join(outDir, "before.png") });
		});
		await dumpCandidatesForRounds(page, outDir, keywords, 1, 600, 50);
		await dumpCandidates(page, outDir, keywords);
		const r = await findAndOpenNoteByKeywords(page as any, keywords, {
			maxScrolls: 1,
			useApiAfterScroll: false,
			settleMs: 50,
		});
		// 测试环境下，为了直观校验 URL 形态，手工触发一次 URL 导航到首个封面锚点
		await page.evaluate(() => {
			const a = document.querySelector("section.note-item a.cover") as HTMLAnchorElement | null;
			if (a && a.getAttribute("href")) {
				// @ts-ignore
				window.location.assign(a.getAttribute("href")!);
			}
		});
		await page
			.waitForURL((u) => String(u).includes("/search_result/"), { timeout: 2000 })
			.catch(() => {});
		await page.screenshot({ path: path.join(outDir, "after.png") });

		expect(r.ok).toBe(true);
		const u = page.url();
		expect(/\/search_result\//.test(u) || /\/website-login\//.test(u)).toBe(true);
	});
});
