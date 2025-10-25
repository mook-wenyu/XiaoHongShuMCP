import type { Page } from 'playwright';
// 外部 JSON 配置已移除，此处直接使用内置容器/锚点默认规则

export type CardInfo = { index: number; noteId?: string; title: string; text: string; y: number; h?: number; y2?: number };

export async function resolveContainerSelector(page: Page): Promise<string> {
  // 统一默认：常见卡片容器选择器集合（可按需要扩展）
  const sel = 'section.note-item, .note-item, .List-item, article, .Card';
  return sel;
}

// 已弃用：ID 精确锚点解析。人类点击仅允许封面或标题，不再直接使用 ID 锚点。
// 保留空实现以防外部误用导出（若仍有导入，则抛出错误以尽早暴露问题）。
export async function resolveAnchorSelectorForId(_page: Page, _id: string): Promise<string> {
  throw new Error('resolveAnchorSelectorForId is removed: use cover/title clickable within card container');
}

export async function collectVisibleCards(page: Page, containerSel: string): Promise<CardInfo[]> {
  try {
    const results = await page.locator(containerSel).evaluateAll((els: Element[]) => {
      const h = window.innerHeight || 0;
      const out: Array<{ index: number; noteId?: string; title: string; text: string; y: number; h?: number; y2?: number }> = [];
      els.forEach((el, index) => {
        const rect = (el as HTMLElement).getBoundingClientRect?.();
        const visible = !!rect && rect.height > 30 && rect.bottom > 0 && rect.top < h * 0.98 && (el as HTMLElement).offsetParent !== null;
        if (!visible) return;
        const container = el as HTMLElement;
        const full = (container.textContent || '').trim().replace(/\s+/g, ' ');
        let title = '';
        const titleEl = container.querySelector('a.title, .footer .title, .title, h1, h2');
        if (titleEl) title = (titleEl.textContent || '').trim();
        if (!title) title = full.slice(0, 120);
        let noteId: string | undefined;
        const hrefEl = container.querySelector(
          "a[href*='/search_result/'], a[href*='/discovery/item/'], a[href*='/explore/'], a[href*='/question/'], a[href*='/p/'], a[href*='/zvideo/']"
        ) as HTMLAnchorElement | null;
        const href = hrefEl?.getAttribute('href') || '';
        const m = href.match(/(?:explore|discovery\/item|search_result|question|p|zvideo)\/(\w+)/);
        if (m && m[1]) noteId = m[1];
        out.push({ index, noteId, title, text: full, y: rect.top, h: rect.height, y2: rect.bottom });
      });
      out.sort((a, b) => a.y - b.y);
      return out;
    });
    return Array.isArray(results) ? results : [];
  } catch { return []; }
}
