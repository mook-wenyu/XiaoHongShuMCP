import type { IAdapter, OpenOptions } from "./IAdapter.js";
import type { BrowserContext } from "playwright";
import { ServiceContainer } from "../core/container.js";
import * as Pages from "../services/pages.js";
import { ensureDir, pathJoin } from "../services/artifacts.js";
import { createRequire } from "node:module";

/**
 * 官方适配器：优先使用 roxybrowser 官方 Playwright MCP 桥接包
 * - 运行时动态加载候选包；加载成功则使用官方 getContext/openContext
 * - 否则根据 OFFICIAL_ADAPTER_REQUIRED 决定是否允许继续（允许则浏览器相关工具不可用）
 */
export class OfficialAdapter implements IAdapter {
  constructor(private container: ServiceContainer) {}

  private officialTried = false;
  private official: undefined | {
    getContext?: (dirId: string, opts?: { workspaceId?: string }) => Promise<BrowserContext>
    openContext?: (dirId: string, opts?: { workspaceId?: string }) => Promise<BrowserContext>
  };
  private officialMeta: { name?: string; version?: string } = {};
  private static readonly MIN_OFFICIAL_VERSION = "0.1.0";

  private static cmpSemver(a?: string, b?: string): number | undefined {
    if (!a || !b) return undefined;
    const pa = a.split(".").map((x) => parseInt(x, 10));
    const pb = b.split(".").map((x) => parseInt(x, 10));
    for (let i = 0; i < Math.max(pa.length, pb.length); i++) {
      const ai = pa[i] || 0, bi = pb[i] || 0;
      if (ai > bi) return 1; if (ai < bi) return -1;
    }
    return 0;
  }

  private async tryResolveOfficial(): Promise<void> {
    if (this.officialTried) return;
    this.officialTried = true;
    const logger = this.container.createLogger({ module: "officialAdapter", useSilent: true });
    const candidates = [
      "@roxybrowser/playwright-mcp",
      "@roxybrowserlabs/playwright-mcp",
      "roxybrowser-playwright-mcp",
      "roxybrowser-mcp-playwright",
    ];
    for (const name of candidates) {
      try {
        const m = await import(name).catch(() => undefined) as any;
        if (m && (m.getContext || m.openContext)) {
          this.official = { getContext: m.getContext, openContext: m.openContext };
          this.officialMeta.name = name;
          try {
            const require = createRequire(import.meta.url);
            const pkg = require(name + "/package.json");
            if (pkg?.version && typeof pkg.version === "string") {
              this.officialMeta.version = pkg.version;
              const cmp = OfficialAdapter.cmpSemver(pkg.version, OfficialAdapter.MIN_OFFICIAL_VERSION);
              if (cmp !== undefined && cmp < 0) {
                logger.warn({ name, version: pkg.version, min: OfficialAdapter.MIN_OFFICIAL_VERSION }, "官方桥版本过低，建议升级");
              } else {
                logger.info({ name, version: pkg.version }, "已加载官方 Playwright MCP 桥");
              }
            } else {
              logger.info({ name }, "已加载官方桥（版本未知）");
            }
          } catch (e) {
            logger.debug?.({ name, err: String(e) }, "读取官方桥版本失败（忽略）");
          }
          return;
        }
      } catch (e) {
        logger.debug?.({ err: String(e) }, `official module load failed: ${name}`);
      }
    }

  }

  private async getCtx(dirId: string, opts?: OpenOptions): Promise<BrowserContext> {
    await this.tryResolveOfficial();
    if (this.official?.getContext) {
      return await this.official.getContext(dirId, { workspaceId: opts?.workspaceId });
    }
    if (this.official?.openContext) {
      return await this.official.openContext(dirId, { workspaceId: opts?.workspaceId });
    }
    const req = process.env.OFFICIAL_ADAPTER_REQUIRED;
    const required = req === undefined ? true : req === "true";
    const msg = "未检测到 roxybrowser 官方 Playwright MCP 桥接包（getContext/openContext 不可用）。\n"
      + "安装方法：npm i -S @roxybrowser/playwright-mcp（或兼容包）。\n"
      + "若处在内网/私有源环境，请在 .npmrc 配置 @roxybrowser:registry 并重试。\n"
      + "快速指引：npm run advisor:official（根据包管理器给出一键安装命令）。\n"
      + "如需临时跳过（不推荐生产）：设置 OFFICIAL_ADAPTER_REQUIRED=false 将继续运行，但浏览器相关工具将不可用。";
    if (required) throw new Error(msg);
    throw new Error(msg);
  }

  async open(dirId: string, opts?: OpenOptions) { return this.getContext(dirId, opts); }
  async getContext(dirId: string, opts?: OpenOptions) { const context = await this.getCtx(dirId, opts); return { context }; }
  async listPages(dirId: string, opts?: OpenOptions) { const ctx = await this.getCtx(dirId, opts); return { pages: Pages.listPages(ctx) }; }
  async createPage(dirId: string, url?: string, opts?: OpenOptions) { const ctx = await this.getCtx(dirId, opts); const p = await Pages.newPage(ctx, url); const idx = ctx.pages().findIndex(x=>x===p); return { index: idx >= 0 ? idx : ctx.pages().length - 1, url: p.url() }; }
  async closePage(dirId: string, pageIndex?: number, opts?: OpenOptions) { const ctx = await this.getCtx(dirId, opts); const ok = await Pages.closePage(ctx, pageIndex); return { closed: ok, closedIndex: ok ? (pageIndex ?? Math.max(0, ctx.pages().length - 1)) : undefined }; }
  async close(dirId: string) { try { await this.container.createRoxyClient().close(dirId); } catch {} }
  async navigate(dirId: string, url: string, pageIndex?: number, opts?: OpenOptions) { const ctx = await this.getCtx(dirId, opts); const page = await Pages.ensurePage(ctx, { pageIndex }); await page.goto(url, { waitUntil: "domcontentloaded" }); return { url: page.url() }; }
  async screenshot(dirId: string, pageIndex?: number, fullPage: boolean = true, opts?: OpenOptions) { const ctx = await this.getCtx(dirId, opts); const page = await Pages.ensurePage(ctx, { pageIndex }); const outRoot = pathJoin("artifacts", dirId, "actions"); await ensureDir(outRoot); const path = pathJoin(outRoot, `screenshot-${Date.now()}.png`); await page.screenshot({ path, fullPage }); const buffer = await (await import("node:fs/promises")).readFile(path); return { path, buffer }; }
}
