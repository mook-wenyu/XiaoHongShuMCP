import type { Browser, BrowserContext, Page } from "playwright";

/**
 * Playwright 浏览器连接器接口
 *
 * 封装 Playwright 与远程浏览器的连接管理，支持多窗口=多上下文模型。
 *
 * @remarks
 * 架构模型：
 * - 多窗口 = 多上下文（一个账号 = 一个 Context）
 * - 多页 = 同 Context 多 Page
 *
 * @example
 * ```typescript
 * const connector: IPlaywrightConnector = new PlaywrightConnector(roxyClient);
 * const { browser, context } = await connector.connect('dirId123');
 * const result = await connector.withContext('dirId123', async (ctx) => {
 *   const page = await connector.newPage(ctx);
 *   await page.goto('https://example.com');
 *   return { ok: true };
 * });
 * ```
 */
export interface IPlaywrightConnector {
	/**
	 * 连接到远程浏览器（通过 CDP）
	 * @param dirId 窗口标识符
	 * @param opts 连接选项
	 * @returns 浏览器和上下文实例
	 */
	connect(
		dirId: string,
		opts?: { workspaceId?: string }
	): Promise<{ browser: Browser; context: BrowserContext }>;

	/**
	 * 在临时上下文中执行操作（自动管理连接生命周期）
	 * @param dirId 窗口标识符
	 * @param fn 回调函数
	 * @param opts 连接选项
	 * @returns 回调函数的返回值
	 */
	withContext<T>(
		dirId: string,
		fn: (ctx: BrowserContext) => Promise<T>,
		opts?: { workspaceId?: string }
	): Promise<T>;

	/**
	 * 在现有上下文中创建新页面
	 * @param context 浏览器上下文
	 * @returns 新页面实例
	 */
	newPage(context: BrowserContext): Promise<Page>;
}
