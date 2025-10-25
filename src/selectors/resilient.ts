/* 中文注释：弹性选择器解析（集成重试、断路器和健康度监控） */
import type { Page, Locator } from "playwright";
import { withRetry } from "../lib/retry.js";
import { PolicyEnforcer } from "../services/policy.js";
import { resolveLocatorAsync, type ResolveOptions } from "./selector.js";
import { healthMonitor } from "./health.js";
import { XHS_CONF } from "../config/xhs.js";
import type { TargetHints } from "./types.js";
import { domainSlugFromUrl } from "../lib/url.js";
import { appendHealth } from "./health-sink.js";

export interface ResilientOptions extends ResolveOptions {
  selectorId?: string; // 选择器标识符（用于健康度监控和断路器）
  retryAttempts?: number; // 重试次数（默认 3）
  retryBaseMs?: number; // 重试基础延迟（默认 200ms）
  retryMaxMs?: number; // 重试最大延迟（默认 2000ms）
  skipHealthMonitor?: boolean; // 跳过健康度监控
  verifyTimeoutMs?: number; // 验证 Locator 超时（默认 1000ms）
  platformSlug?: string; // 可选：显式平台 slug 覆盖
}

// 全局断路器实例（QPS 限流 + 熔断）
const policyEnforcer = new PolicyEnforcer({
  qps: Number(process.env.SELECTOR_BREAKER_QPS || 10), // 每秒请求上限（可配）
  failureThreshold: Number(process.env.SELECTOR_BREAKER_FAILURES || 3), // 连续失败阈值（可配）
  openSeconds: Number(process.env.SELECTOR_BREAKER_OPEN_SECONDS || 1.2), // 熔断窗口默认 1.2s，满足测试“>1s”预期
});

/**
 * 弹性选择器解析（带重试、断路器和健康度监控）
 * @param page Playwright Page 对象
 * @param hints 选择器提示
 * @param opts 弹性选项
 * @returns Locator 对象
 */
export async function resolveLocatorResilient(
  page: Page,
  hints: TargetHints,
  opts: ResilientOptions = {}
): Promise<Locator> {
  // 若 hints 带有逻辑 ID，则优先使用（便于跨页面与改版追踪）
  const selectorId = (hints as any).id || opts.selectorId || "anonymous";
  const startTime = Date.now();
  const skipHealthMonitor = opts.skipHealthMonitor ?? false;

  // 重试配置
  const retryAttempts = opts.retryAttempts ?? 3;
  const retryBaseMs = opts.retryBaseMs ?? 200;
  const retryMaxMs = opts.retryMaxMs ?? 2000;
  const verifyTimeoutMs = opts.verifyTimeoutMs ?? 1000;

  try {
    // 使用断路器包装整个解析流程
    const locator = await policyEnforcer.use(selectorId, async () => {
      // 使用重试机制包装选择器解析
      return await withRetry(
        async (attempt) => {
          // 直接解析：不再从外部 JSON 合成候选，保持输入提示的最小依赖
          const loc = await resolveLocatorAsync(page, hints as any, {
            aliases: opts.aliases,
            probeTimeoutMs: opts.probeTimeoutMs,
          });

          // 验证 Locator 可用性（等待元素附加到 DOM）
          try {
            await loc.first().waitFor({
              state: "attached",
              timeout: verifyTimeoutMs,
            });
          } catch (err) {
            // 验证失败，抛出错误触发重试
            throw new Error(
              `选择器验证失败 (尝试 ${attempt + 1}/${retryAttempts}): ${err instanceof Error ? err.message : String(err)}`
            );
          }

          return loc;
        },
        {
          attempts: retryAttempts,
          baseMs: retryBaseMs,
          maxMs: retryMaxMs,
          jitter: true,
        }
      );
    });

    // 记录成功
    const durationMs = Date.now() - startTime;
    if (!skipHealthMonitor) {
      healthMonitor.record(selectorId, true, durationMs);
      try {
        await appendHealth({ ts: Date.now(), selectorId, ok: true, durationMs, url: page.url(), slug: domainSlugFromUrl(page.url()) });
      } catch {}
    }

    return locator;
  } catch (err) {
    // 记录失败
    const durationMs = Date.now() - startTime;
    if (!skipHealthMonitor) {
      healthMonitor.record(selectorId, false, durationMs);
      try {
        const code = err instanceof Error ? err.name : undefined;
        await appendHealth({ ts: Date.now(), selectorId, ok: false, durationMs, url: page.url(), slug: domainSlugFromUrl(page.url()), errorCode: code });
      } catch {}
    }

    // 增强错误信息
    const errorMessage = err instanceof Error ? err.message : String(err);
    throw new Error(
      `弹性选择器解析失败 [${selectorId}]: ${errorMessage}` +
        (hints.alternatives ? ` (尝试了 ${hints.alternatives.length} 个候选)` : "")
    );
  }
}

/**
 * 获取全局断路器实例（用于测试和监控）
 */
export function getPolicyEnforcer(): PolicyEnforcer {
  return policyEnforcer;
}
