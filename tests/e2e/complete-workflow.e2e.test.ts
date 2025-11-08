/**
 * 端到端测试：完整的笔记选择和内容提取流程
 *
 * 测试场景：
 * 1. 搜索关键词 → 找到匹配卡片 → 点击打开笔记 → 提取内容
 * 2. 验证整个流程的端到端一致性
 */
import { describe, it, expect, beforeAll, afterAll } from "vitest";
import { chromium, type Browser, type Page } from "playwright";
import { resolveClickableInCard } from "../../src/domain/xhs/click";
import { isDetailUrl } from "../../src/domain/xhs/detail-url";
import { resolveContainerSelector, collectVisibleCards } from "../../src/selectors/card";

let browser: Browser;
let page: Page;

// Mock HTML：模拟小红书搜索结果页面
const MOCK_HTML = `
<!doctype html>
<html><head><meta charset="utf-8"><title>小红书搜索 - Mock</title></head>
<body>
<div class="search-container" style="width:100%;height:2000px;">
  <!-- 卡片 1: 匹配"美食"关键词 -->
  <section class="note-item" style="position:relative;height:300px;margin:20px;">
    <div class="footer">
      <a class="title"><span>成都美食探店攻略</span></a>
    </div>
    <a class="cover" href="/search_result/abc123def456" onclick="window.__openDetail('abc123def456');return false;">
      <img alt="成都美食探店" />
    </a>
  </section>

  <!-- 卡片 2: 匹配"旅游"关键词 -->
  <section class="note-item" style="position:relative;height:300px;margin:20px;">
    <div class="footer">
      <a class="title"><span>云南旅游必去景点</span></a>
    </div>
    <a class="cover" href="/explore/xyz789abc123" onclick="window.__openDetail('xyz789abc123');return false;">
      <img alt="云南旅游" />
    </a>
  </section>

  <!-- 卡片 3: 不匹配任何关键词 -->
  <section class="note-item" style="position:relative;height:300px;margin:20px;">
    <div class="footer">
      <a class="title"><span>数码产品推荐</span></a>
    </div>
    <a class="cover" href="/explore/qwe456rty789" onclick="window.__openDetail('qwe456rty789');return false;">
      <img alt="数码产品" />
    </a>
  </section>
</div>

<!-- 模拟详情页模态 -->
<div id="note-detail-modal" class="note-detail-mask" style="display:none;position:fixed;top:0;left:0;right:0;bottom:0;background:rgba(0,0,0,0.8);z-index:9999;">
  <div class="note-detail-content" style="background:white;margin:50px auto;padding:20px;max-width:800px;">
    <h1 id="detail-title">笔记标题</h1>
    <div id="detail-content">笔记内容</div>
    <div id="detail-tags"></div>
    <button onclick="window.__closeDetail()">关闭</button>
  </div>
</div>

<script>
  // 模拟笔记数据
  window.__notes = {
    'abc123def456': {
      title: '成都美食探店攻略',
      content: '成都必吃的10家餐厅，从火锅到串串，从小吃到甜品...',
      tags: ['美食', '成都', '探店'],
      author: '美食探险家',
      likes: 1234,
      collects: 567,
      comments: 89,
      shares: 45
    },
    'xyz789abc123': {
      title: '云南旅游必去景点',
      content: '云南最美的10个景点，大理、丽江、香格里拉...',
      tags: ['旅游', '云南', '攻略'],
      author: '旅行达人',
      likes: 2345,
      collects: 789,
      comments: 123,
      shares: 67
    },
    'qwe456rty789': {
      title: '数码产品推荐',
      content: '2024年最值得购买的数码产品盘点...',
      tags: ['数码', '科技', '评测'],
      author: '科技博主',
      likes: 3456,
      collects: 890,
      comments: 156,
      shares: 78
    }
  };

  // 模拟打开详情
  window.__openDetail = function(noteId) {
    const note = window.__notes[noteId];
    if (!note) return;

    const modal = document.getElementById('note-detail-modal');
    document.getElementById('detail-title').textContent = note.title;
    document.getElementById('detail-content').textContent = note.content;
    document.getElementById('detail-tags').textContent = note.tags.join(', ');

    modal.style.display = 'block';
    window.__currentNoteId = noteId;

    // 确定性行为：仅通过模态打开，不改变 URL
    // 这确保测试结果可预测且稳定
  };

  // 模拟关闭详情
  window.__closeDetail = function() {
    document.getElementById('note-detail-modal').style.display = 'none';
    window.__currentNoteId = null;
    window.history.pushState({}, '', '/explore');
  };
</script>
</body></html>`;

describe("端到端测试：笔记选择和内容提取", () => {
	beforeAll(async () => {
		browser = await chromium.launch({ headless: true });
	});

	afterAll(async () => {
		await browser?.close();
	});

	it("完整流程：搜索 → 找到卡片 → 点击打开 → 验证详情", async () => {
		page = await browser.newPage();

		// Step 1: 加载 Mock 页面
		await page.goto("about:blank");
		await page.setContent(MOCK_HTML, { waitUntil: "domcontentloaded" });
		await page.waitForTimeout(100);

		// Step 2: 收集所有卡片
		const containerSel = await resolveContainerSelector(page);
		const cards = await collectVisibleCards(page, containerSel);

		expect(cards.length).toBeGreaterThan(0);
		console.log(`✓ 找到 ${cards.length} 个卡片`);

		// Step 3: 搜索包含"美食"的卡片
		const keyword = "美食";
		const matchedCard = cards.find((card) =>
			card.title.toLowerCase().includes(keyword.toLowerCase())
		);

		expect(matchedCard).toBeDefined();
		expect(matchedCard?.title).toContain("成都美食");
		console.log(`✓ 匹配到卡片: "${matchedCard?.title}"`);

		// Step 4: 定位可点击元素
		const cardLocator = page.locator(containerSel).nth(matchedCard!.index);
		const clickable = await resolveClickableInCard(page, cardLocator, {
			id: matchedCard?.noteId,
		});

		expect(await clickable.count()).toBe(1);
		console.log(`✓ 定位到可点击元素`);

		// Step 5: 点击打开笔记
		const urlBefore = page.url();
		await clickable.click();
		await page.waitForTimeout(500); // 等待模态打开或 URL 变化

		// Step 6: 验证打开方式（模态或 URL）
		const urlAfter = page.url();
		const urlChanged = urlAfter !== urlBefore;

		let openMethod: "modal" | "url" | "unknown" = "unknown";

		if (urlChanged && isDetailUrl(urlAfter)) {
			openMethod = "url";
			console.log(`✓ 通过 URL 打开: ${urlAfter}`);
		} else {
			// 检查是否有模态出现
			const hasModal = await page.locator("#note-detail-modal[style*='display: block']").count();
			if (hasModal > 0) {
				openMethod = "modal";
				console.log(`✓ 通过模态打开`);
			}
		}

		expect(["modal", "url"]).toContain(openMethod);

		// Step 7: 验证详情内容（如果是模态）
		if (openMethod === "modal") {
			const detailTitle = await page.locator("#detail-title").textContent();
			const detailContent = await page.locator("#detail-content").textContent();

			expect(detailTitle).toContain("成都美食");
			expect(detailContent).toContain("火锅");
			console.log(`✓ 验证详情内容成功`);
		}

		await page.close();
	}, 15000);

	it("边界测试：无匹配卡片时的处理", async () => {
		page = await browser.newPage();

		await page.goto("about:blank");
		await page.setContent(MOCK_HTML, { waitUntil: "domcontentloaded" });
		await page.waitForTimeout(100);

		// 搜索不存在的关键词
		const containerSel = await resolveContainerSelector(page);
		const cards = await collectVisibleCards(page, containerSel);

		const keyword = "不存在的关键词12345";
		const matchedCard = cards.find((card) =>
			card.title.toLowerCase().includes(keyword.toLowerCase())
		);

		// 应该找不到匹配的卡片
		expect(matchedCard).toBeUndefined();
		console.log(`✓ 正确处理无匹配卡片的情况`);

		await page.close();
	}, 10000);

	it("并发测试：同时处理多个卡片", async () => {
		page = await browser.newPage();

		await page.goto("about:blank");
		await page.setContent(MOCK_HTML, { waitUntil: "domcontentloaded" });
		await page.waitForTimeout(100);

		// 收集所有卡片
		const containerSel = await resolveContainerSelector(page);
		const cards = await collectVisibleCards(page, containerSel);

		expect(cards.length).toBe(3);

		// 并发定位所有卡片的可点击元素
		const clickables = await Promise.all(
			cards.map(async (card, index) => {
				const cardLocator = page.locator(containerSel).nth(index);
				const clickable = await resolveClickableInCard(page, cardLocator);
				return {
					card,
					clickable,
					count: await clickable.count(),
				};
			})
		);

		// 验证所有卡片都能定位到可点击元素
		clickables.forEach((item, index) => {
			expect(item.count).toBeGreaterThan(0);
			console.log(`✓ 卡片 ${index + 1} "${item.card.title}" 定位成功`);
		});

		await page.close();
	}, 15000);

	it("性能测试：卡片收集和解析性能", async () => {
		page = await browser.newPage();

		await page.goto("about:blank");
		await page.setContent(MOCK_HTML, { waitUntil: "domcontentloaded" });
		await page.waitForTimeout(100);

		// 测试卡片收集性能
		const startCollect = Date.now();
		const containerSel = await resolveContainerSelector(page);
		const cards = await collectVisibleCards(page, containerSel);
		const collectTime = Date.now() - startCollect;

		console.log(`✓ 卡片收集耗时: ${collectTime}ms`);
		expect(collectTime).toBeLessThan(1000); // 应该在 1 秒内完成

		// 测试可点击元素解析性能
		const startResolve = Date.now();
		await Promise.all(
			cards.map(async (card, index) => {
				const cardLocator = page.locator(containerSel).nth(index);
				await resolveClickableInCard(page, cardLocator);
			})
		);
		const resolveTime = Date.now() - startResolve;

		console.log(`✓ 元素解析耗时: ${resolveTime}ms`);
		expect(resolveTime).toBeLessThan(2000); // 应该在 2 秒内完成

		await page.close();
	}, 15000);
});
