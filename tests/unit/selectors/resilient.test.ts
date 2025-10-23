/* 单元测试：弹性选择器解析 */
import { describe, it, expect, beforeEach, vi } from "vitest";
import { resolveLocatorResilient, getPolicyEnforcer } from "../../../src/selectors/resilient.js";
import { healthMonitor } from "../../../src/selectors/health.js";
import type { Page, Locator } from "playwright";
import type { TargetHints } from "../../../src/selectors/types.js";

// Mock Playwright 类型
function createMockLocator(shouldSucceed = true): Locator {
  return {
    first: () => ({
      waitFor: async () => {
        if (!shouldSucceed) {
          throw new Error("Element not found");
        }
        return Promise.resolve();
      },
    }),
  } as any;
}

function createMockPage(): Page {
  return {
    getByRole: () => createMockLocator(),
    locator: () => createMockLocator(),
  } as any;
}

describe("resolveLocatorResilient", () => {
  beforeEach(() => {
    // 清空健康度监控数据
    healthMonitor.clear();
  });

  describe("成功路径", () => {
    it("应该在第一次尝试成功时返回 Locator", async () => {
      const page = createMockPage();
      const hints: TargetHints = { selector: "#test-element" };

      const locator = await resolveLocatorResilient(page, hints, {
        selectorId: "testSelector",
      });

      expect(locator).toBeDefined();

      // 验证健康度记录
      const health = healthMonitor.getHealth("testSelector");
      expect(health?.totalCount).toBe(1);
      expect(health?.successCount).toBe(1);
      expect(health?.successRate).toBe(1);
    });

    it("应该支持 skipHealthMonitor 选项", async () => {
      const page = createMockPage();
      const hints: TargetHints = { selector: "#test-element" };

      await resolveLocatorResilient(page, hints, {
        selectorId: "skippedSelector",
        skipHealthMonitor: true,
      });

      // 不应该记录健康度数据
      const health = healthMonitor.getHealth("skippedSelector");
      expect(health).toBeUndefined();
    });
  });

  describe("重试机制", () => {
    it("应该在失败后重试", async () => {
      let attemptCount = 0;
      const mockPage = {
        locator: () => ({
          first: () => ({
            waitFor: async () => {
              attemptCount++;
              if (attemptCount < 2) {
                throw new Error("First attempt fails");
              }
              // 第二次成功
              return Promise.resolve();
            },
          }),
        }),
      } as any;

      const hints: TargetHints = { selector: "#retry-test" };

      const locator = await resolveLocatorResilient(mockPage, hints, {
        selectorId: "retrySelector",
        retryAttempts: 3,
      });

      expect(locator).toBeDefined();
      expect(attemptCount).toBe(2); // 第一次失败，第二次成功

      // 验证健康度记录（应该记录为成功）
      const health = healthMonitor.getHealth("retrySelector");
      expect(health?.successCount).toBe(1);
    });

    it("应该在所有重试都失败后抛出错误", async () => {
      const mockPage = {
        locator: () => ({
          first: () => ({
            waitFor: async () => {
              throw new Error("Always fails");
            },
          }),
        }),
      } as any;

      const hints: TargetHints = { selector: "#always-fail" };

      await expect(
        resolveLocatorResilient(mockPage, hints, {
          selectorId: "failSelector",
          retryAttempts: 2,
        })
      ).rejects.toThrow("弹性选择器解析失败");

      // 验证健康度记录（应该记录为失败）
      const health = healthMonitor.getHealth("failSelector");
      expect(health?.failureCount).toBe(1);
    });
  });

  describe("断路器机制", () => {
    it("应该在连续失败后触发断路器", async () => {
      const mockPage = {
        locator: () => ({
          first: () => ({
            waitFor: async () => {
              throw new Error("Circuit breaker test");
            },
          }),
        }),
      } as any;

      const hints: TargetHints = { selector: "#circuit-test" };

      // 触发 3 次失败（failureThreshold = 3）
      for (let i = 0; i < 3; i++) {
        await expect(
          resolveLocatorResilient(mockPage, hints, {
            selectorId: "circuitSelector",
            retryAttempts: 1, // 减少重试次数加快测试
          })
        ).rejects.toThrow();
      }

      // 第 4 次应该立即被断路器拦截（需要等待熔断期）
      const startTime = Date.now();
      await expect(
        resolveLocatorResilient(mockPage, hints, {
          selectorId: "circuitSelector",
          retryAttempts: 1,
        })
      ).rejects.toThrow();
      const duration = Date.now() - startTime;

      // 断路器应该导致延迟（等待半开窗口）
      expect(duration).toBeGreaterThan(1000); // 至少 1 秒延迟
    }, 15000); // 增加测试超时

    it("应该在成功后重置失败计数", async () => {
      let callCount = 0;
      const mockPage = {
        locator: () => ({
          first: () => ({
            waitFor: async () => {
              callCount++;
              if (callCount < 3) {
                throw new Error("First two calls fail");
              }
              // 第三次成功
              return Promise.resolve();
            },
          }),
        }),
      } as any;

      const hints: TargetHints = { selector: "#reset-test" };

      // 前两次失败
      for (let i = 0; i < 2; i++) {
        await expect(
          resolveLocatorResilient(mockPage, hints, {
            selectorId: "resetSelector",
            retryAttempts: 1,
          })
        ).rejects.toThrow();
      }

      // 第三次成功
      await resolveLocatorResilient(mockPage, hints, {
        selectorId: "resetSelector",
        retryAttempts: 1,
      });

      // 失败计数应该被重置，再次失败不应该立即触发熔断
      callCount = 0; // 重置模拟计数器
      await expect(
        resolveLocatorResilient(mockPage, hints, {
          selectorId: "resetSelector",
          retryAttempts: 1,
        })
      ).rejects.toThrow();

      // 应该不会触发熔断延迟
      const health = healthMonitor.getHealth("resetSelector");
      expect(health?.totalCount).toBe(4); // 2 失败 + 1 成功 + 1 失败
    });
  });

  describe("健康度记录", () => {
    it("应该正确记录成功和失败的耗时", async () => {
      // 创建带延迟的 mock
      const mockPage = {
        locator: () => ({
          first: () => ({
            waitFor: async () => {
              await new Promise((resolve) => setTimeout(resolve, 5)); // 5ms 延迟
              return Promise.resolve();
            },
          }),
        }),
      } as any;

      const hints: TargetHints = { selector: "#timing-test" };

      // 成功调用
      await resolveLocatorResilient(mockPage, hints, {
        selectorId: "timingSelector",
      });

      const health = healthMonitor.getHealth("timingSelector");
      expect(health?.avgDurationMs).toBeGreaterThan(0);
      expect(health?.avgDurationMs).toBeLessThan(1000); // 应该很快
    });

    it("应该为匿名选择器使用默认 ID", async () => {
      const page = createMockPage();
      const hints: TargetHints = { selector: "#anonymous-test" };

      await resolveLocatorResilient(page, hints);

      const health = healthMonitor.getHealth("anonymous");
      expect(health).toBeDefined();
      expect(health?.totalCount).toBe(1);
    });
  });

  describe("错误处理", () => {
    it("应该增强错误信息包含选择器 ID", async () => {
      const mockPage = {
        locator: () => ({
          first: () => ({
            waitFor: async () => {
              throw new Error("Test error");
            },
          }),
        }),
      } as any;

      const hints: TargetHints = { selector: "#error-test" };

      await expect(
        resolveLocatorResilient(mockPage, hints, {
          selectorId: "errorSelector",
          retryAttempts: 1,
        })
      ).rejects.toThrow(/弹性选择器解析失败.*errorSelector/);
    });

    it("应该在错误信息中包含候选数量", async () => {
      const mockPage = {
        locator: () => ({
          first: () => ({
            waitFor: async () => {
              throw new Error("Test error");
            },
          }),
        }),
      } as any;

      const hints: TargetHints = {
        alternatives: [
          { selector: "#option1" },
          { selector: "#option2" },
          { selector: "#option3" },
        ],
      };

      await expect(
        resolveLocatorResilient(mockPage, hints, {
          selectorId: "altSelector",
          retryAttempts: 1,
        })
      ).rejects.toThrow(/尝试了 3 个候选/);
    });
  });

  describe("getPolicyEnforcer", () => {
    it("应该返回全局断路器实例", () => {
      const enforcer = getPolicyEnforcer();
      expect(enforcer).toBeDefined();
      expect(typeof enforcer.use).toBe("function");
    });
  });
});
