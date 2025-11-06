/**
 * RoxyBrowser + Playwright 集成层（最佳实践）
 *
 * 使用 Playwright 官方推荐的 CDP 连接方式
 * 直接与 RoxyBrowser API 集成，无需额外抽象
 */

import type { BrowserContext, chromium as ChromiumType } from "playwright";
import { ServiceContainer } from "../core/container.js";
import * as Pages from "./pages.js";
import { ensureDir, pathJoin } from "./artifacts.js";

export interface BrowserConnectionOptions {
	dirId: string;
	workspaceId?: string;
}

/**
 * RoxyBrowser 连接管理器
 *
 * 职责：
 * 1. 通过 RoxyBrowser API 获取 CDP endpoint
 * 2. 使用 Playwright 连接到浏览器
 * 3. 管理 BrowserContext 生命周期
 */
export class RoxyBrowserManager {
	private contexts = new Map<string, BrowserContext>();
	private chromium: typeof ChromiumType | null = null;

	constructor(private container: ServiceContainer) {}

	/**
	 * 获取或创建 BrowserContext
	 */
	async getContext(dirId: string, opts?: { workspaceId?: string }): Promise<BrowserContext> {
		const cacheKey = this.getCacheKey(dirId, opts?.workspaceId);

		// 复用已存在的 context
		const cached = this.contexts.get(cacheKey);
		if (cached) {
			const browser = cached.browser();
			if (browser && !browser.isConnected()) {
				this.contexts.delete(cacheKey);
			} else {
				return cached;
			}
		}

		// 创建新连接
		const roxyClient = this.container.createRoxyClient();
		const logger = this.container.createLogger({ module: "roxyBrowser" });

		try {
			// 1. 通过 RoxyBrowser API 启动或获取浏览器
			const connectionInfo = await roxyClient.open(dirId, undefined, opts?.workspaceId);

			if (!connectionInfo.ws) {
				throw new Error(`RoxyBrowser 未返回 CDP endpoint: dirId=${dirId}`);
			}

			// 2. 使用 Playwright 官方 CDP 连接
			if (!this.chromium) {
				const { chromium } = await import("playwright");
				this.chromium = chromium;
			}

			const browser = await this.chromium.connectOverCDP(connectionInfo.ws);

			// 3. 获取默认 context（RoxyBrowser 的持久化 context）
			const contexts = browser.contexts();
			if (contexts.length === 0) {
				throw new Error(`未找到 BrowserContext: dirId=${dirId}`);
			}

			const context = contexts[0];
			this.contexts.set(cacheKey, context);

			logger.info({ dirId, workspaceId: opts?.workspaceId }, "已连接到 RoxyBrowser");

			return context;
		} catch (error) {
			logger.error({ dirId, error: String(error) }, "连接 RoxyBrowser 失败");
			throw error;
		}
	}

	/**
	 * 关闭指定的 BrowserContext
	 */
	async closeContext(dirId: string, workspaceId?: string): Promise<void> {
		const cacheKey = this.getCacheKey(dirId, workspaceId);
		const context = this.contexts.get(cacheKey);

		if (context) {
			try {
				const browser = context.browser();
				if (browser) {
					await browser.close();
				}
			} catch {
				// 忽略关闭错误
			}
			this.contexts.delete(cacheKey);
		}

		// 通知 RoxyBrowser API
		try {
			await this.container.createRoxyClient().close(dirId);
		} catch {
			// 忽略关闭错误
		}
	}

	/**
	 * 列出页面
	 */
	async listPages(dirId: string, opts?: { workspaceId?: string }) {
		const context = await this.getContext(dirId, opts);
		return { pages: Pages.listPages(context) };
	}

	/**
	 * 创建新页面
	 */
	async createPage(dirId: string, url?: string, opts?: { workspaceId?: string }) {
		const context = await this.getContext(dirId, opts);
		const page = await Pages.newPage(context, url);
		const index = context.pages().findIndex((p) => p === page);
		return {
			index: index >= 0 ? index : context.pages().length - 1,
			url: page.url(),
		};
	}

	/**
	 * 关闭页面
	 */
	async closePage(dirId: string, pageIndex?: number, opts?: { workspaceId?: string }) {
		const context = await this.getContext(dirId, opts);
		const closed = await Pages.closePage(context, pageIndex);
		return {
			closed,
			closedIndex: closed ? (pageIndex ?? Math.max(0, context.pages().length - 1)) : undefined,
		};
	}

	/**
	 * 导航
	 */
	async navigate(dirId: string, url: string, pageIndex?: number, opts?: { workspaceId?: string }) {
		const context = await this.getContext(dirId, opts);
		const page = await Pages.ensurePage(context, { pageIndex });
		await page.goto(url, { waitUntil: "domcontentloaded" });
		return { url: page.url() };
	}

	/**
	 * 截图
	 */
	async screenshot(
		dirId: string,
		pageIndex?: number,
		fullPage: boolean = true,
		opts?: { workspaceId?: string },
	) {
		const context = await this.getContext(dirId, opts);
		const page = await Pages.ensurePage(context, { pageIndex });

		const outRoot = pathJoin("artifacts", dirId, "actions");
		await ensureDir(outRoot);

		const path = pathJoin(outRoot, `screenshot-${Date.now()}.png`);
		await page.screenshot({ path, fullPage });

		const { readFile } = await import("node:fs/promises");
		const buffer = await readFile(path);

		return { path, buffer };
	}

	private getCacheKey(dirId: string, workspaceId?: string): string {
		return workspaceId ? `${workspaceId}:${dirId}` : dirId;
	}
}
