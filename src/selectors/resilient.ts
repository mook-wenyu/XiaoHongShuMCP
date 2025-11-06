/* 中文说明：选择器韧性解析（重试、断路器与健康度监控）*/
import type { Page, Locator } from "playwright";
import { withRetry } from "../lib/retry.js";
import { PolicyEnforcer } from "../services/policy.js";
import { resolveLocatorAsync, type ResolveOptions } from "./selector.js";
import { healthMonitor } from "./health.js";
import type { TargetHints } from "./types.js";
import { domainSlugFromUrl } from "../lib/url.js";
import { appendHealth } from "./health-sink.js";

export interface ResilientOptions extends ResolveOptions {
  selectorId?: string; // 选择器标识符（用于健康度与断路器）
  retryAttempts?: number; // 重试次数（默认 3）
  retryBaseMs?: number; // 重试基础等待 ms（默认 200）
  retryMaxMs?: number; // 重试最大等待 ms（默认 2000）
  skipHealthMonitor?: boolean; // 跳过健康度监控
  verifyTimeoutMs?: number; // 验证 Locator 的超时时间（默认 1000ms）
  platformSlug?: string; // 可选：平台 slug 覆盖
}

// 全局断路器实例（QPS 限流 + 熔断）
const policyEnforcer = new PolicyEnforcer({
  qps: Number(process.env.SELECTOR_BREAKER_QPS || 10),
  failureThreshold: Number(process.env.SELECTOR_BREAKER_FAILURES || 3),
  openSeconds: Number(process.env.SELECTOR_BREAKER_OPEN_SECONDS || 1.2),
});

export async function resolveLocatorResilient(
  page: Page,
  hints: TargetHints,
  opts: ResilientOptions = {}
): Promise<Locator> {
  const selectorId = (hints as any).id || opts.selectorId || "anonymous";
  const startTime = Date.now();
  const skipHealthMonitor = opts.skipHealthMonitor ?? false;

  // 允许通过环境变量覆盖默认重试与超时配置（保持向后兼容）
  const retryAttempts = Number(process.env.SELECTOR_RETRY_ATTEMPTS ?? (opts.retryAttempts ?? 3));
  const retryBaseMs = Number(process.env.SELECTOR_RETRY_BASE_MS ?? (opts.retryBaseMs ?? 200));
  const retryMaxMs = Number(process.env.SELECTOR_RETRY_MAX_MS ?? (opts.retryMaxMs ?? 2000));
  const verifyTimeoutMs = Number(process.env.SELECTOR_VERIFY_TIMEOUT_MS ?? (opts.verifyTimeoutMs ?? 1000));

  try {
    const locator = await policyEnforcer.use(selectorId, async () => {
      return await withRetry(
        async (attempt) => {
          const loc = await resolveLocatorAsync(page, hints as any, {
            aliases: opts.aliases,
            probeTimeoutMs: opts.probeTimeoutMs,
          });

          // 验证 Locator 可用性（等待元素附加到 DOM）
          try {
            await loc.first().waitFor({ state: "attached", timeout: verifyTimeoutMs });
          } catch (err) {
            const emsg = err instanceof Error ? err.message : String(err);
            const e = new Error(`选择器验证失败(尝试 ${attempt + 1}/${retryAttempts}): ${emsg}`);
            (e as any).name = /timeout/i.test(emsg) ? "TIMEOUT" : "LOCATOR_NOT_FOUND";
            throw e;
          }

          return loc;
        },
        { attempts: retryAttempts, baseMs: retryBaseMs, maxMs: retryMaxMs, jitter: true }
      );
    });

    // 成功记录
    const durationMs = Date.now() - startTime;
    if (!skipHealthMonitor) {
      healthMonitor.record(selectorId, true, durationMs);
      try { await appendHealth({ ts: Date.now(), selectorId, ok: true, durationMs, url: page.url(), slug: domainSlugFromUrl(page.url()) }); } catch {}
    }
    return locator;
  } catch (err) {
    // 失败记录
    const durationMs = Date.now() - startTime;
    if (!skipHealthMonitor) {
      healthMonitor.record(selectorId, false, durationMs);
      try { const code = err instanceof Error ? ((err as any).name ?? err.name) : undefined;
        await appendHealth({ ts: Date.now(), selectorId, ok: false, durationMs, url: page.url(), slug: domainSlugFromUrl(page.url()), errorCode: code }); } catch {}
    }
    const errorMessage = err instanceof Error ? err.message : String(err);
    throw new Error(`韧性选择器解析失败[${selectorId}]: ${errorMessage}`);
  }
}

export function getPolicyEnforcer(): PolicyEnforcer { return policyEnforcer; }
