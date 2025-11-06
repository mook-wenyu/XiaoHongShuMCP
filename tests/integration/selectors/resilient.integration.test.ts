/* 中文注释：韧性选择器集成测试（真实浏览器场景模拟） */
import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { resolveLocatorResilient } from "../../../src/selectors/resilient.js";
import { healthMonitor } from "../../../src/selectors/health.js";
import { generateHealthReport, logHealthReport } from "../../../src/selectors/report.js";
import type { Page, Locator } from "playwright";
import type { TargetHints } from "../../../src/selectors/types.js";

// Mock Page 对象（模拟真实浏览器场景）
function createMockPage(options: {
	shouldSucceed?: boolean;
	failUntilAttempt?: number;
	delay?: number;
}): Page {
	const { shouldSucceed = true, failUntilAttempt = 0, delay = 0 } = options;
	let attemptCount = 0;

	return {
		getByRole: () => createMockLocator(shouldSucceed, failUntilAttempt, delay, attemptCount),
		locator: () => ({
			first: () => ({
				waitFor: async () => {
					attemptCount++;
					if (delay > 0) {
						await new Promise((resolve) => setTimeout(resolve, delay));
					}
					if (attemptCount <= failUntilAttempt) {
						throw new Error(`Mock failure (attempt ${attemptCount})`);
					}
					if (!shouldSucceed) {
						throw new Error("Mock element not found");
					}
					return Promise.resolve();
				},
			}),
		}),
	} as any;
}

function createMockLocator(
	shouldSucceed: boolean,
	failUntilAttempt: number,
	delay: number,
	attemptCount: number,
): Locator {
	return {
		first: () => ({
			waitFor: async () => {
				if (delay > 0) {
					await new Promise((resolve) => setTimeout(resolve, delay));
				}
				if (attemptCount <= failUntilAttempt) {
					throw new Error(`Mock failure (attempt ${attemptCount})`);
				}
				if (!shouldSucceed) {
					throw new Error("Element not found");
				}
				return Promise.resolve();
			},
		}),
	} as any;
}

describe("韧性选择器集成测试", () => {
	beforeEach(() => {
		// 清空健康度监控数据
		healthMonitor.clear();
	});

	afterEach(() => {
		vi.clearAllMocks();
	});

	describe("真实场景模拟", () => {
		it("应该在网络延迟场景下成功解析选择器", async () => {
			const page = createMockPage({
				shouldSucceed: true,
				delay: 100, // 模拟 100ms 网络延迟
			});

			const hints: TargetHints = { selector: "#delayed-element" };

			const startTime = Date.now();
			const locator = await resolveLocatorResilient(page, hints, {
				selectorId: "delayed-element",
				retryAttempts: 3,
			});

			const duration = Date.now() - startTime;

			expect(locator).toBeDefined();
			expect(duration).toBeGreaterThanOrEqual(100); // 至少延迟 100ms

			// 验证健康度记录
			const health = healthMonitor.getHealth("delayed-element");
			expect(health?.successCount).toBe(1);
			expect(health?.avgDurationMs).toBeGreaterThan(100);
		});

		it("应该在间歇性失败后重试成功", async () => {
			const page = createMockPage({
				shouldSucceed: true,
				failUntilAttempt: 2, // 前两次失败，第三次成功
			});

			const hints: TargetHints = { selector: "#flaky-element" };

			const locator = await resolveLocatorResilient(page, hints, {
				selectorId: "flaky-element",
				retryAttempts: 3,
			});

			expect(locator).toBeDefined();

			// 验证健康度记录（应该记录为成功）
			const health = healthMonitor.getHealth("flaky-element");
			expect(health?.successCount).toBe(1);
			expect(health?.totalCount).toBe(1);
		});

		it("应该在连续失败后触发断路器", async () => {
			const page = createMockPage({
				shouldSucceed: false,
			});

			const hints: TargetHints = { selector: "#broken-element" };

			// 触发 3 次失败（断路器阈值）
			for (let i = 0; i < 3; i++) {
				await expect(
					resolveLocatorResilient(page, hints, {
						selectorId: "broken-element",
						retryAttempts: 1,
					}),
				).rejects.toThrow();
			}

			// 验证健康度记录
			const health = healthMonitor.getHealth("broken-element");
			expect(health?.failureCount).toBe(3);
			expect(health?.successRate).toBe(0);

			// 第 4 次应该被断路器延迟（需要等待半开窗口）
			const startTime = Date.now();
			await expect(
				resolveLocatorResilient(page, hints, {
					selectorId: "broken-element",
					retryAttempts: 1,
				}),
			).rejects.toThrow();
			const duration = Date.now() - startTime;

			const openSeconds = Number(process.env.SELECTOR_BREAKER_OPEN_SECONDS || 0.2);
			const expectMs = Math.max(150, Math.floor(openSeconds * 1000) - 20);
			expect(duration).toBeGreaterThanOrEqual(expectMs); // 与配置对齐的断路器延迟
		}, 15000); // 增加测试超时
	});

	describe("健康度报告集成", () => {
		it("应该生成完整的健康度报告", async () => {
			healthMonitor.clear(); // 清除之前的数据

			const page1 = createMockPage({ shouldSucceed: true });
			const page2 = createMockPage({ shouldSucceed: false });

			// 执行多次选择器解析
			for (let i = 0; i < 5; i++) {
				await resolveLocatorResilient(
					page1,
					{ selector: "#healthy" },
					{
						selectorId: "healthy-selector",
					},
				);
			}

			for (let i = 0; i < 5; i++) {
				await expect(
					resolveLocatorResilient(
						page2,
						{ selector: "#unhealthy" },
						{
							selectorId: "unhealthy-selector",
							retryAttempts: 1,
						},
					),
				).rejects.toThrow();
			}

			// 生成报告
			const report = generateHealthReport({
				unhealthyThreshold: 0.7,
				minSampleSize: 3,
			});

			expect(report.totalSelectors).toBe(2);
			expect(report.healthyCount).toBe(1);
			expect(report.unhealthyCount).toBe(1);
			expect(report.unhealthySelectors).toHaveLength(1);
			expect(report.unhealthySelectors[0].selectorId).toBe("unhealthy-selector");
			expect(report.recommendations.length).toBeGreaterThan(0);
		}, 15000); // 增加超时时间

		it("应该在所有选择器健康时提供正面建议", async () => {
			const page = createMockPage({ shouldSucceed: true });

			for (let i = 0; i < 10; i++) {
				await resolveLocatorResilient(
					page,
					{ selector: "#perfect" },
					{
						selectorId: "perfect-selector",
					},
				);
			}

			const report = generateHealthReport({
				unhealthyThreshold: 0.7,
				minSampleSize: 5,
			});

			expect(report.unhealthyCount).toBe(0);
			expect(report.averageSuccessRate).toBe(1);
			expect(report.recommendations).toContain("所有选择器运行正常，无需优化");
		});

		it("应该过滤掉样本数不足的选择器", async () => {
			const page = createMockPage({ shouldSucceed: true });

			// 只执行 2 次（低于 minSampleSize）
			for (let i = 0; i < 2; i++) {
				await resolveLocatorResilient(
					page,
					{ selector: "#low-sample" },
					{
						selectorId: "low-sample-selector",
					},
				);
			}

			const report = generateHealthReport({
				minSampleSize: 5,
			});

			// 样本数不足的选择器不应出现在报告中
			expect(report.totalSelectors).toBe(0);
		});
	});

	describe("并发场景", () => {
		it("应该正确处理并发选择器解析", async () => {
			const page = createMockPage({ shouldSucceed: true });

			const hints: TargetHints = { selector: "#concurrent" };

			// 并发执行 10 个选择器解析
			const promises = Array.from({ length: 10 }, (_, _i) =>
				resolveLocatorResilient(page, hints, {
					selectorId: "concurrent-selector",
				}),
			);

			const results = await Promise.all(promises);

			expect(results).toHaveLength(10);
			results.forEach((loc) => expect(loc).toBeDefined());

			// 验证健康度记录
			const health = healthMonitor.getHealth("concurrent-selector");
			expect(health?.totalCount).toBe(10);
			expect(health?.successCount).toBe(10);
			expect(health?.successRate).toBe(1);
		});

		it("应该在并发场景下正确记录失败", async () => {
			const page = createMockPage({ shouldSucceed: false });

			const hints: TargetHints = { selector: "#concurrent-fail" };

			// 并发执行 5 个失败的选择器解析
			const promises = Array.from({ length: 5 }, () =>
				resolveLocatorResilient(page, hints, {
					selectorId: "concurrent-fail",
					retryAttempts: 1,
				}).catch(() => "failed"),
			);

			const results = await Promise.all(promises);

			expect(results.every((r) => r === "failed")).toBe(true);

			// 验证健康度记录
			const health = healthMonitor.getHealth("concurrent-fail");
			expect(health?.totalCount).toBe(5);
			expect(health?.failureCount).toBe(5);
			expect(health?.successRate).toBe(0);
		});
	});

	describe("性能基准", () => {
		it("成功的选择器解析应该在 500ms 内完成", async () => {
			const page = createMockPage({ shouldSucceed: true, delay: 10 });

			const hints: TargetHints = { selector: "#fast-element" };

			const startTime = Date.now();
			await resolveLocatorResilient(page, hints, {
				selectorId: "fast-element",
				retryAttempts: 3,
			});
			const duration = Date.now() - startTime;

			expect(duration).toBeLessThan(500);
		});

		it("带重试的选择器解析应该在合理时间内完成", async () => {
			const page = createMockPage({
				shouldSucceed: true,
				failUntilAttempt: 2,
				delay: 50,
			});

			const hints: TargetHints = { selector: "#retry-element" };

			const startTime = Date.now();
			await resolveLocatorResilient(page, hints, {
				selectorId: "retry-element",
				retryAttempts: 3,
				retryBaseMs: 100,
			});
			const duration = Date.now() - startTime;

			// 应该在 1 秒内完成（包括重试延迟）
			expect(duration).toBeLessThan(1000);
		});
	});

	describe("错误恢复", () => {
		it("应该在部分失败后恢复正常", async () => {
			healthMonitor.clear(); // 清除之前的数据

			let callCount = 0;
			const createDynamicPage = () =>
				createMockPage({
					shouldSucceed: callCount++ >= 3, // 前 3 次失败，之后成功
				});

			const hints: TargetHints = { selector: "#recovery" };

			// 前 3 次失败
			for (let i = 0; i < 3; i++) {
				await expect(
					resolveLocatorResilient(createDynamicPage(), hints, {
						selectorId: "recovery-selector",
						retryAttempts: 1,
					}),
				).rejects.toThrow();
			}

			// 第 4 次成功
			const locator = await resolveLocatorResilient(createDynamicPage(), hints, {
				selectorId: "recovery-selector",
				retryAttempts: 1,
			});

			expect(locator).toBeDefined();

			// 验证健康度
			const health = healthMonitor.getHealth("recovery-selector");
			expect(health?.totalCount).toBe(4);
			expect(health?.successCount).toBe(1);
			expect(health?.failureCount).toBe(3);
		}, 15000); // 增加超时时间
	});

	describe("日志集成", () => {
		it("应该正确记录健康度报告到日志", async () => {
			const page = createMockPage({ shouldSucceed: true });

			for (let i = 0; i < 5; i++) {
				await resolveLocatorResilient(
					page,
					{ selector: "#logged" },
					{
						selectorId: "logged-selector",
					},
				);
			}

			const report = generateHealthReport({ minSampleSize: 3 });

			// 记录到日志（不应抛出错误）
			expect(() => logHealthReport(report)).not.toThrow();

			expect(report.totalSelectors).toBeGreaterThan(0);
		});
	});
});
