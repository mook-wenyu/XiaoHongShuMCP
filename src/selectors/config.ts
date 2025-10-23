/* 中文注释：选择器配置加载器（按域名文件，如 xiaohongshu.json / zhihu.json）
 * 功能：
 *  - 根据 Page.url() 推导平台 slug（去除子域与 TLD，例如 www.xiaohongshu.com -> xiaohongshu）
 *  - 从 `SELECTOR_CONFIG_DIR`（默认 `selectors/`）加载 `<slug>.json`
 *  - Zod 校验并缓存（LRU+TTL），支持环境变量 `SELECTOR_CONFIG_DISABLED` 关闭
 *  - 提供按 selectorId 取 candidates 的方法，失败返回 undefined（不干扰原路径）
 */
import { readFile } from "node:fs/promises";
import { resolve } from "node:path";
import { z } from "zod";
import type { Page } from "playwright";
import type { TargetHints } from "./types.js";

const TTL_MS = Number(process.env.SELECTOR_CONFIG_TTL_MS || 60_000);
const MAX_CACHE = Number(process.env.SELECTOR_CONFIG_CACHE || 8);
const CONF_DIR = process.env.SELECTOR_CONFIG_DIR || "selectors";
const DISABLED = /^true$/i.test(process.env.SELECTOR_CONFIG_DISABLED || "false");
const WATCH_ENABLED = process.env.SELECTOR_CONFIG_WATCH ? /^true$/i.test(process.env.SELECTOR_CONFIG_WATCH) : (process.env.NODE_ENV === 'development');

// JSON Schema（运行时校验）
const TextExpr = z.union([
  z.string(),
  z.object({ exact: z.string(), caseSensitive: z.boolean().optional() }),
  z.object({ contains: z.string(), caseSensitive: z.boolean().optional() }),
  z.object({ regex: z.string() }),
]);

const PreferEnum = z.enum(["role","label","placeholder","testId","title","alt","text","selector"]);

const TargetHintsSchema: z.ZodType<TargetHints> = z.lazy(() => z.object({
  id: z.string().optional(),
  selector: z.string().optional(),
  role: z.string().optional(),
  label: z.string().optional(),
  placeholder: z.string().optional(),
  text: TextExpr.optional(),
  testId: z.string().optional(),
  name: TextExpr.optional(),
  hasText: z.union([z.string(), z.object({ regex: z.string() })]).optional(),
  has: z.any().optional(),
  within: z.string().optional(),
  nth: z.union([z.literal("first"), z.literal("last"), z.number()]).optional(),
  visible: z.boolean().optional(),
  timeoutMs: z.number().int().positive().optional(),
  prefer: z.array(PreferEnum).optional(),
  alternatives: z.array(z.any()).optional(),
}));

// 扩展候选：条件/权重（可选）
const CandidateExt = TargetHintsSchema.and(z.object({
  weight: z.number().min(0).max(1).optional(),
  conditions: z.object({
    locale: z.string().optional(),
    abVariant: z.string().optional(),
    viewport: z.object({ width: z.number().int().positive(), height: z.number().int().positive() }).optional(),
  }).partial().optional(),
}));

const SelectorEntry = z.object({
  prefer: z.array(PreferEnum).optional(),
  candidates: z.array(CandidateExt).min(1),
});

const SelectorsJson = z.object({
  platform: z.string(), // 例如 "xiaohongshu"
  version: z.number().int().positive().default(1),
  selectors: z.record(SelectorEntry),
});

export type SelectorsConfig = z.infer<typeof SelectorsJson>;

// 简易 LRU+TTL 缓存
const cache = new Map<string, { at: number; data: SelectorsConfig }>();
function getNow() { return Date.now(); }
function cacheGet(key: string): SelectorsConfig | undefined {
  const e = cache.get(key);
  if (!e) return undefined;
  if (getNow() - e.at > TTL_MS) { cache.delete(key); return undefined; }
  // LRU：触摸
  cache.delete(key); cache.set(key, e);
  return e.data;
}
function cachePut(key: string, data: SelectorsConfig) {
  if (cache.size >= MAX_CACHE) {
    const firstKey = cache.keys().next().value as string | undefined;
    if (firstKey) cache.delete(firstKey);
  }
  cache.set(key, { at: getNow(), data });
}

// dev 监听：文件变更即清缓存
let watcherStarted = false;
function ensureWatch() {
  if (!WATCH_ENABLED || watcherStarted) return;
  watcherStarted = true;
  try {
    const { watch } = require('node:fs');
    watch(CONF_DIR, { recursive: false }, () => {
      try { cache.clear(); } catch {}
    });
  } catch {}
}
ensureWatch();

export function domainSlugFromUrl(url: string): string | undefined {
  try {
    const u = new URL(url);
    const host = u.hostname.toLowerCase(); // e.g., m.xiaohongshu.com
    // 简化：取二级域名（去子域与 TLD）；若无法解析，则取最左非 www 的段
    const parts = host.split(".").filter(Boolean);
    if (parts.length >= 2) return parts[parts.length - 2];
    if (parts.length === 1) return parts[0] === "www" ? undefined : parts[0];
    return undefined;
  } catch { return undefined; }
}

async function loadSelectorsFile(slug: string): Promise<SelectorsConfig | undefined> {
  const cached = cacheGet(slug);
  if (cached) return cached;
  try {
    const p = resolve(CONF_DIR, `${slug}.json`);
    const raw = await readFile(p, "utf-8");
    const json = JSON.parse(raw);
    const data = SelectorsJson.parse(json);
    cachePut(slug, data);
    return data;
  } catch {
    return undefined;
  }
}

export async function loadSelectorsForPage(page: Page, explicitSlug?: string): Promise<SelectorsConfig | undefined> {
  if (DISABLED) return undefined;
  const slug = explicitSlug || domainSlugFromUrl(page.url());
  if (!slug) return undefined;
  return await loadSelectorsFile(slug);
}

export async function getCandidatesById(page: Page, id: string, explicitSlug?: string): Promise<TargetHints[] | undefined> {
  const conf = await loadSelectorsForPage(page, explicitSlug);
  if (!conf) return undefined;
  const entry = conf.selectors[id];
  if (!entry) return undefined;

  // 上下文：locale 与 viewport
  let locale: string | undefined;
  try { locale = await page.evaluate(() => navigator.language).catch(() => undefined) as any; } catch {}
  const vp = page.viewportSize?.() as any;

  // 条件筛选
  const filtered = (entry.candidates as any[]).filter((c) => {
    const cond = c.conditions as any | undefined;
    if (!cond) return true;
    if (cond.locale && locale && cond.locale.toLowerCase() !== String(locale).toLowerCase()) return false;
    if (cond.abVariant && process.env.AB_VARIANT && cond.abVariant !== process.env.AB_VARIANT) return false;
    if (cond.viewport && vp && (vp.width < cond.viewport.width || vp.height < cond.viewport.height)) return false;
    return true;
  });

  // 权重排序（降序），同分按声明顺序
  filtered.sort((a, b) => (b.weight ?? 0) - (a.weight ?? 0));

  // 将 prefer 下沉到每个候选（不覆盖候选内的 prefer）
  const prefer = entry.prefer;
  return filtered.map(c => (prefer && !c.prefer ? { ...c, prefer } : c));
}
