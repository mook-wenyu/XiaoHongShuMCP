import { describe, it, expect, beforeAll, afterAll, beforeEach } from "vitest";
import { chromium, type Browser, type Page } from "playwright";
import { findAndOpenNoteByKeywords } from "../../../src/domain/xhs/navigation";

let browser: Browser;
let page: Page;

const HTML = `
<!doctype html>
<html><head><meta charset="utf-8"><title>xhs mock</title></head>
<body>
<div class="feeds-container" style="width:100%;height:1600px;">
  <section class="note-item" data-index="0">
    <div class="footer">
      <a class="title"><span>为什么独立游戏排斥“纯策划”</span></a>
    </div>
    <a class="cover" href="/search_result/681cb2e000000000210183d2" onclick="window.__openModal();return false;">open</a>
  </section>
  <section class="note-item" data-index="1">
    <div class="footer"><a class="title"><span>独立游戏 和 gamejam 的纯策划都是骗人的</span></a></div>
    <a class="cover" href="/search_result/678cd47c000000002100111f" onclick="window.__openModal();return false;">open</a>
  </section>
</div>
<script>
  window.__openModal = function(){
    if(!document.querySelector('.note-detail-mask')){
      const d=document.createElement('div'); d.className='note-detail-mask'; document.body.appendChild(d);
    }
  };
</script>
</body></html>`;

async function setupMock(page: Page){
  await page.goto("https://www.xiaohongshu.com/explore", { waitUntil: "domcontentloaded" });
  await page.setContent(HTML, { waitUntil: "domcontentloaded" });
}

describe("xhs_select_note · keywords any-of integration (mock DOM)", () => {
  beforeAll(async () => { browser = await chromium.launch(); });
  afterAll(async () => { await browser.close(); });
  beforeEach(async () => {
    page = await browser.newPage({ viewport: { width: 1200, height: 800 } });
    process.env.XHS_FEED_WAIT_API_MS = "30";
    process.env.XHS_SEARCH_WAIT_API_MS = "30";
    process.env.XHS_SCROLL_USE_API_AFTER = "false";
    process.env.XHS_SCROLL_STEP_RATIO = "0.45";
    process.env.XHS_SCROLL_OVERLAP_ANCHORS = "3";
    process.env.XHS_SCROLL_OVERLAP_RATIO = "0.25";
    process.env.XHS_SELECT_MAX_MS = "2000";
    await setupMock(page);
  });

  it("matches when any keyword is contained and opens modal", async () => {
    const keywords = ["摄影", "游戏"];
    const r = await findAndOpenNoteByKeywords(page as any, keywords, { maxScrolls: 2, useApiAfterScroll: false, settleMs: 50 });
    expect(r.ok).toBe(true);
    expect(r.modalOpen).toBe(true);
  });

  it("fails when none of the keywords are present", async () => {
    const keywords = ["摄影"];
    const r = await findAndOpenNoteByKeywords(page as any, keywords, { maxScrolls: 1, useApiAfterScroll: false, settleMs: 50 });
    expect(r.ok).toBe(false);
  });
});
