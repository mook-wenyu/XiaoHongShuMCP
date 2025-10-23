/* 中文注释：Playwright 连接器，多窗口=多上下文（账号=默认Context），多页=同Context多Page */
import { chromium, type Browser, type BrowserContext, type Page } from "playwright";
import type { ILogger } from "../contracts/ILogger.js";
import type { IRoxyClient } from "../contracts/IRoxyClient.js";
import type { IPlaywrightConnector } from "../contracts/IPlaywrightConnector.js";

export interface ConnectResult {
	browser: Browser;
	context: BrowserContext;
}
export interface OpenOptions {
	workspaceId?: string;
}

/**
 * Playwright 连接器
 *
 * 管理 Playwright 浏览器连接，通过 CDP 协议连接到 Roxy 打开的浏览器窗口。
 * 实现 IPlaywrightConnector 接口，支持依赖注入。
 *
 * @remarks
 * 核心概念：
 * - 多窗口 = 多上下文（每个 dirId 对应一个独立的 BrowserContext）
 * - 多页 = 同上下文多 Page（同一账号可打开多个标签页）
 *
 * 使用示例：
 * ```typescript
 * const connector = new PlaywrightConnector(roxyClient, logger);
 * await connector.withContext('dirId123', async (ctx) => {
 *   const page = await ctx.newPage();
 *   await page.goto('https://example.com');
 * });
 * ```
 */
export class PlaywrightConnector implements IPlaywrightConnector {
	/**
	 * 构造函数
	 * @param roxy Roxy API 客户端接口
	 * @param logger 日志记录器（可选）
	 */
	constructor(private roxy: IRoxyClient, private logger?: ILogger) {}

	async connect(dirId: string, opts?: OpenOptions): Promise<ConnectResult> {
		// 1) 确保窗口已 open（若已存在则复用），拿到 ws 端点（可选 workspaceId 透传）
		this.logger?.debug({ dirId, workspaceId: opts?.workspaceId }, "连接到浏览器");

		const opened = await this.roxy.ensureOpen(dirId, opts?.workspaceId);
		const ws = opened.ws;
		if (!ws) throw new Error(`Roxy 未返回 ws，dirId=${dirId}`);

		this.logger?.debug({ dirId, ws }, "通过 CDP 连接到远程浏览器");

		// 2) 连接到远程浏览器（CDP）
		const browser = await chromium.connectOverCDP(ws);
		const contexts = browser.contexts();
		if (contexts.length === 0) {
			throw new Error("连接成功但未发现默认上下文");
		}
		const context = contexts[0];

		this.logger?.info({ dirId, contextsCount: contexts.length }, "已连接到浏览器");
		return { browser, context };
	}

	async withContext<T>(
		dirId: string,
		fn: (ctx: BrowserContext) => Promise<T>,
		opts?: OpenOptions
	): Promise<T> {
		let browser: Browser | undefined;
		try {
			const { browser: b, context } = await this.connect(dirId, opts);
			browser = b;
			this.logger?.debug({ dirId }, "执行上下文函数");
			return await fn(context);
		} finally {
			try {
				await browser?.close();
				this.logger?.debug({ dirId }, "已关闭浏览器连接");
			} catch (e) {
				this.logger?.warn({ dirId, err: e }, "browser.close 失败，忽略");
			}
			try {
				await this.roxy.close(dirId);
			} catch (e) {
				this.logger?.warn({ dirId, err: e }, "roxy.close 失败，忽略");
			}
		}
	}

	async newPage(context: BrowserContext): Promise<Page> {
		// 多页=同 Context 新建 Page
		this.logger?.debug("创建新页面");
		return context.newPage();
	}
}
