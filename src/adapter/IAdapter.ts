import type { BrowserContext } from "playwright";

export interface OpenOptions { workspaceId?: string }

export interface IAdapter {
  // 窗口/会话管理
  open(dirId: string, opts?: OpenOptions): Promise<{ context: BrowserContext }>
  getContext(dirId: string, opts?: OpenOptions): Promise<{ context: BrowserContext }>
  listPages(dirId: string, opts?: OpenOptions): Promise<{ pages: { index: number; url: string; isClosed?: boolean }[] }>
  createPage(dirId: string, url?: string, opts?: OpenOptions): Promise<{ index: number; url?: string }>
  closePage(dirId: string, pageIndex?: number, opts?: OpenOptions): Promise<{ closed: boolean; closedIndex?: number }>
  close(dirId: string): Promise<void>

  // 页面动作（最小必要）
  navigate(dirId: string, url: string, pageIndex?: number, opts?: OpenOptions): Promise<{ url: string }>
  screenshot(dirId: string, pageIndex?: number, fullPage?: boolean, opts?: OpenOptions): Promise<{ path: string; buffer: Buffer }>
}
