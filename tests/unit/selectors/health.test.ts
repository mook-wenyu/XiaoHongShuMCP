/* 单元测试：选择器健康度监控系统 */
import { describe, it, expect, beforeEach } from "vitest";
import { SelectorHealthMonitor } from "../../../src/selectors/health.js";

describe("SelectorHealthMonitor", () => {
	let monitor: SelectorHealthMonitor;

	beforeEach(() => {
		monitor = new SelectorHealthMonitor();
	});

	describe("record() - 记录选择器使用情况", () => {
		it("应该正确记录成功的选择器使用", () => {
			monitor.record("testSelector", true, 100);

			const health = monitor.getHealth("testSelector");
			expect(health).toBeDefined();
			expect(health!.totalCount).toBe(1);
			expect(health!.successCount).toBe(1);
			expect(health!.failureCount).toBe(0);
			expect(health!.successRate).toBe(1);
			expect(health!.avgDurationMs).toBe(100);
		});

		it("应该正确记录失败的选择器使用", () => {
			monitor.record("testSelector", false, 200);

			const health = monitor.getHealth("testSelector");
			expect(health).toBeDefined();
			expect(health!.totalCount).toBe(1);
			expect(health!.successCount).toBe(0);
			expect(health!.failureCount).toBe(1);
			expect(health!.successRate).toBe(0);
			expect(health!.avgDurationMs).toBe(200);
		});

		it("应该累加多次记录", () => {
			monitor.record("testSelector", true, 100);
			monitor.record("testSelector", true, 200);
			monitor.record("testSelector", false, 300);

			const health = monitor.getHealth("testSelector");
			expect(health!.totalCount).toBe(3);
			expect(health!.successCount).toBe(2);
			expect(health!.failureCount).toBe(1);
			expect(health!.successRate).toBeCloseTo(2 / 3, 2);
			expect(health!.avgDurationMs).toBeCloseTo(200, 1); // (100+200+300)/3
		});

		it("应该限制 durations 数组长度为 100", () => {
			// 记录 150 次
			for (let i = 0; i < 150; i++) {
				monitor.record("testSelector", true, i * 10);
			}

			const health = monitor.getHealth("testSelector");
			expect(health!.totalCount).toBe(150);

			// 平均值应该基于最近 100 次：50*10 到 149*10
			// 平均 = (50+51+...+149) * 10 / 100
			const expectedSum = Array.from({ length: 100 }, (_, i) => (i + 50) * 10).reduce(
				(sum, val) => sum + val,
				0,
			);
			const expectedAvg = expectedSum / 100;
			expect(health!.avgDurationMs).toBeCloseTo(expectedAvg, 1);
		});

		it("应该更新 lastUsed 时间", async () => {
			const before = new Date();
			await new Promise((resolve) => setTimeout(resolve, 10));

			monitor.record("testSelector", true, 100);

			const health = monitor.getHealth("testSelector");
			expect(health!.lastUsed.getTime()).toBeGreaterThanOrEqual(before.getTime());
		});
	});

	describe("getHealth() - 获取健康度数据", () => {
		it("应该返回 undefined 对于不存在的选择器", () => {
			const health = monitor.getHealth("nonexistent");
			expect(health).toBeUndefined();
		});

		it("应该正确计算成功率", () => {
			monitor.record("selector1", true, 100);
			monitor.record("selector1", true, 100);
			monitor.record("selector1", false, 100);
			monitor.record("selector1", false, 100);

			const health = monitor.getHealth("selector1");
			expect(health!.successRate).toBe(0.5);
		});

		it("应该返回完整的健康度数据结构", () => {
			monitor.record("testSelector", true, 150);

			const health = monitor.getHealth("testSelector");
			expect(health).toMatchObject({
				selectorId: "testSelector",
				totalCount: expect.any(Number),
				successCount: expect.any(Number),
				failureCount: expect.any(Number),
				successRate: expect.any(Number),
				avgDurationMs: expect.any(Number),
				lastUsed: expect.any(Date),
			});
		});
	});

	describe("reportUnhealthy() - 报告不健康的选择器", () => {
		beforeEach(() => {
			// 准备测试数据
			// selector1: 100% 成功率
			monitor.record("selector1", true, 100);
			monitor.record("selector1", true, 100);

			// selector2: 50% 成功率（不健康）
			monitor.record("selector2", true, 100);
			monitor.record("selector2", false, 100);

			// selector3: 30% 成功率（非常不健康）
			monitor.record("selector3", true, 100);
			monitor.record("selector3", false, 100);
			monitor.record("selector3", false, 100);
			monitor.record("selector3", false, 100);
			monitor.record("selector3", false, 100);
			monitor.record("selector3", false, 100);
			monitor.record("selector3", false, 100);
			monitor.record("selector3", false, 100);
			monitor.record("selector3", false, 100);
			monitor.record("selector3", false, 100);
		});

		it("应该返回成功率低于默认阈值（70%）的选择器", () => {
			const unhealthy = monitor.reportUnhealthy();
			expect(unhealthy.length).toBe(2);
			expect(unhealthy.map((h) => h.selectorId)).toContain("selector2");
			expect(unhealthy.map((h) => h.selectorId)).toContain("selector3");
		});

		it("应该支持自定义阈值", () => {
			const unhealthy = monitor.reportUnhealthy(0.6); // 60%
			expect(unhealthy.length).toBe(2);

			const veryUnhealthy = monitor.reportUnhealthy(0.4); // 40%
			expect(veryUnhealthy.length).toBe(1);
			expect(veryUnhealthy[0].selectorId).toBe("selector3");
		});

		it("应该按成功率升序排序（最不健康的在前）", () => {
			const unhealthy = monitor.reportUnhealthy();
			expect(unhealthy.length).toBeGreaterThan(0);

			for (let i = 0; i < unhealthy.length - 1; i++) {
				expect(unhealthy[i].successRate).toBeLessThanOrEqual(unhealthy[i + 1].successRate);
			}
		});

		it("应该返回空数组当所有选择器都健康时", () => {
			const healthyMonitor = new SelectorHealthMonitor();
			healthyMonitor.record("healthy1", true, 100);
			healthyMonitor.record("healthy2", true, 100);

			const unhealthy = healthyMonitor.reportUnhealthy();
			expect(unhealthy).toEqual([]);
		});
	});

	describe("export() - 导出所有健康度数据", () => {
		it("应该导出所有选择器的健康度数据", () => {
			monitor.record("selector1", true, 100);
			monitor.record("selector2", false, 200);

			const exported = monitor.export();
			expect(Object.keys(exported).length).toBe(2);
			expect(exported["selector1"]).toBeDefined();
			expect(exported["selector2"]).toBeDefined();
		});

		it("应该返回空对象当没有数据时", () => {
			const exported = monitor.export();
			expect(exported).toEqual({});
		});

		it("导出的数据应该包含完整的健康度信息", () => {
			monitor.record("testSelector", true, 150);

			const exported = monitor.export();
			const health = exported["testSelector"];

			expect(health).toMatchObject({
				selectorId: "testSelector",
				totalCount: 1,
				successCount: 1,
				failureCount: 0,
				successRate: 1,
				avgDurationMs: 150,
				lastUsed: expect.any(Date),
			});
		});
	});

	describe("并发测试", () => {
		it("应该正确处理并发 record 调用", async () => {
			const promises = [];
			for (let i = 0; i < 100; i++) {
				promises.push(
					Promise.resolve().then(() => {
						monitor.record("concurrentSelector", i % 2 === 0, i);
					}),
				);
			}

			await Promise.all(promises);

			const health = monitor.getHealth("concurrentSelector");
			expect(health!.totalCount).toBe(100);
			expect(health!.successCount).toBe(50); // 偶数次成功
			expect(health!.failureCount).toBe(50); // 奇数次失败
			expect(health!.successRate).toBe(0.5);
		});
	});

	describe("辅助方法", () => {
		it("clear() 应该清空所有统计数据", () => {
			monitor.record("selector1", true, 100);
			monitor.record("selector2", false, 200);

			expect(monitor.size()).toBe(2);

			monitor.clear();

			expect(monitor.size()).toBe(0);
			expect(monitor.getHealth("selector1")).toBeUndefined();
			expect(monitor.getHealth("selector2")).toBeUndefined();
		});

		it("size() 应该返回当前跟踪的选择器数量", () => {
			expect(monitor.size()).toBe(0);

			monitor.record("selector1", true, 100);
			expect(monitor.size()).toBe(1);

			monitor.record("selector2", true, 100);
			expect(monitor.size()).toBe(2);

			monitor.record("selector1", false, 100); // 同一选择器
			expect(monitor.size()).toBe(2); // 不应增加
		});
	});
});

describe("全局 healthMonitor 实例", () => {
	it("应该导出全局单例实例", async () => {
		const { healthMonitor } = await import("../../../src/selectors/health.js");
		expect(healthMonitor).toBeDefined();
		expect(healthMonitor).toBeInstanceOf(SelectorHealthMonitor);
	});
});
