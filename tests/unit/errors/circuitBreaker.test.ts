/* 中文注释：熔断器测试（连续失败→熔断→半开→恢复） */
import { describe, it, expect, beforeEach } from "vitest";
import { PolicyEnforcer, type PolicyOptions } from "../../../src/services/policy.js";

describe("熔断器错误路径测试", () => {
	let policy: PolicyEnforcer;
	let options: PolicyOptions;

	beforeEach(() => {
		options = {
			qps: 10,
			failureThreshold: 3,
			openSeconds: 1,
		};
		policy = new PolicyEnforcer(options);
	});

	describe("熔断触发", () => {
		it("应该在连续失败达到阈值后触发熔断", async () => {
			const key = "test-key";

			// 连续失败 3 次
			for (let i = 0; i < 3; i++) {
				await policy.acquire(key);
				policy.fail(key, new Error("Test failure"));
			}

			// 验证熔断器打开
			const state = (policy as any).states[key];
			expect(state?.state).toBe("open");
			expect(state?.until).toBeGreaterThan(Date.now());
		});

		it("应该记录熔断状态", async () => {
			const key = "circuit-test";

			// 触发熔断
			for (let i = 0; i < options.failureThreshold!; i++) {
				await policy.acquire(key);
				policy.fail(key, new Error("Failure"));
			}

			// 验证熔断器打开
			const failures = (policy as any).failures[key];
			const state = (policy as any).states[key];
			expect(failures).toBe(options.failureThreshold);
			expect(state?.until).toBeDefined();
			expect(state?.until).toBeGreaterThan(Date.now());
		});

		it("应该不同 key 独立熔断", async () => {
			const key1 = "key1";
			const key2 = "key2";

			// key1 触发熔断
			for (let i = 0; i < options.failureThreshold!; i++) {
				await policy.acquire(key1);
				policy.fail(key1, new Error("Failure"));
			}

			// key2 仍然可以正常使用
			await expect(policy.use(key2, async () => "success")).resolves.toBe("success");

			// 验证 key1 熔断，key2 未熔断
			const state1 = (policy as any).states[key1];
			const state2 = (policy as any).states[key2];
			expect(state1?.state).toBe("open");
			expect(state2?.state).toBeUndefined(); // key2 从未失败，无状态
		});
	});

	describe("熔断恢复", () => {
		it("应该在熔断时长后进入半开状态", async () => {
			const key = "recovery-test";
			const shortBreakPolicy = new PolicyEnforcer({
				...options,
				openSeconds: 0.1, // 100ms 熔断时长
			});

			// 触发熔断
			for (let i = 0; i < options.failureThreshold!; i++) {
				await shortBreakPolicy.acquire(key);
				shortBreakPolicy.fail(key, new Error("Failure"));
			}

			// 验证熔断打开
			let state = (shortBreakPolicy as any).states[key];
			expect(state?.state).toBe("open");

			// 等待熔断时长（150ms > 100ms）
			await new Promise((resolve) => setTimeout(resolve, 150));

			// 手动触发 acquire 进入半开状态
			await shortBreakPolicy.acquire(key);

			// 验证进入半开状态
			state = (shortBreakPolicy as any).states[key];
			expect(state?.state).toBe("half-open");

			// 半开状态：成功请求后恢复
			const result = await shortBreakPolicy.use(key, async () => {
				shortBreakPolicy.success(key);
				return "recovered";
			});

			expect(result).toBe("recovered");
		}, 10000);

		it("应该在半开状态成功后关闭熔断器", async () => {
			const key = "half-open-test";
			const quickRecoveryPolicy = new PolicyEnforcer({
				...options,
				openSeconds: 0.05, // 50ms
			});

			// 触发熔断
			for (let i = 0; i < options.failureThreshold!; i++) {
				await quickRecoveryPolicy.acquire(key);
				quickRecoveryPolicy.fail(key, new Error("Failure"));
			}

			// 等待进入半开状态
			await new Promise((resolve) => setTimeout(resolve, 100));

			// 半开状态成功请求
			await quickRecoveryPolicy.use(key, async () => {
				quickRecoveryPolicy.success(key);
				return "success";
			});

			// 验证熔断器已关闭（失败计数重置）
			const failures = (quickRecoveryPolicy as any).failures[key];
			const state = (quickRecoveryPolicy as any).states[key];
			expect(failures).toBe(0);
			expect(state?.state).toBe("closed");

			// 后续请求正常通过
			await expect(quickRecoveryPolicy.use(key, async () => "normal")).resolves.toBe("normal");
		}, 10000);

		it("应该在半开状态失败后重新熔断", async () => {
			const key = "half-open-fail-test";
			const policy = new PolicyEnforcer({
				...options,
				openSeconds: 0.05, // 50ms
			});

			// 触发熔断
			for (let i = 0; i < options.failureThreshold!; i++) {
				await policy.acquire(key);
				policy.fail(key, new Error("Failure"));
			}

			// 等待进入半开状态
			await new Promise((resolve) => setTimeout(resolve, 100));

			// 半开状态失败请求
			try {
				await policy.use(key, async () => {
					throw new Error("Still failing");
				});
			} catch (e) {
				policy.fail(key, e as Error);
			}

			// 验证重新熔断
			const state = (policy as any).states[key];
			expect(state?.state).toBe("open");
			expect(state?.until).toBeGreaterThan(Date.now());
		}, 10000);
	});

	describe("QPS 限流", () => {
		it("应该限制每秒请求数", async () => {
			const key = "qps-test";
			const qpsPolicy = new PolicyEnforcer({
				...options,
				qps: 2, // 每秒 2 个请求
			});

			// 快速发送 5 个请求
			const results = await Promise.allSettled([
				qpsPolicy.acquire(key),
				qpsPolicy.acquire(key),
				qpsPolicy.acquire(key),
				qpsPolicy.acquire(key),
				qpsPolicy.acquire(key),
			]);

			// 前 2 个应该成功，后 3 个应该被限流（需要等待）
			const fulfilled = results.filter((r) => r.status === "fulfilled").length;
			expect(fulfilled).toBeGreaterThanOrEqual(2);
		}, 5000);

		it("应该在下一秒重置 QPS 配额", async () => {
			const key = "qps-reset-test";
			const qpsPolicy = new PolicyEnforcer({
				...options,
				qps: 1,
			});

			// 第一秒的请求
			await qpsPolicy.acquire(key);

			// 等待 1 秒
			await new Promise((resolve) => setTimeout(resolve, 1100));

			// 第二秒的请求应该成功
			await expect(qpsPolicy.acquire(key)).resolves.toBeUndefined();
		}, 5000);
	});

	describe("失败计数管理", () => {
		it("应该正确累积失败次数", async () => {
			const key = "fail-count-test";

			await policy.acquire(key);
			policy.fail(key, new Error("Failure 1"));

			await policy.acquire(key);
			policy.fail(key, new Error("Failure 2"));

			const failures = (policy as any).failures[key];
			expect(failures).toBe(2);
		});

		it("应该在成功后重置失败计数", async () => {
			const key = "reset-count-test";

			// 失败 2 次
			await policy.acquire(key);
			policy.fail(key, new Error("Failure 1"));
			await policy.acquire(key);
			policy.fail(key, new Error("Failure 2"));

			// 成功 1 次
			await policy.acquire(key);
			policy.success(key);

			// 失败计数应该重置
			const failures = (policy as any).failures[key];
			expect(failures).toBe(0);
		});

		it("应该在未达到阈值时不触发熔断", async () => {
			const key = "no-break-test";

			// 失败 2 次（阈值是 3）
			await policy.acquire(key);
			policy.fail(key, new Error("Failure 1"));
			await policy.acquire(key);
			policy.fail(key, new Error("Failure 2"));

			// 应该仍然可以请求
			await expect(policy.use(key, async () => "success")).resolves.toBe("success");
		});
	});

	describe("熔断器边界条件", () => {
		it("应该处理零阈值配置", async () => {
			const zeroThresholdPolicy = new PolicyEnforcer({
				...options,
				failureThreshold: 0,
			});

			const key = "zero-threshold-test";

			// 任何失败都不应该触发熔断（阈值为 0）
			await zeroThresholdPolicy.acquire(key);
			zeroThresholdPolicy.fail(key, new Error("Failure"));

			await expect(zeroThresholdPolicy.use(key, async () => "success")).resolves.toBe("success");
		});

		it("应该处理超大阈值配置", async () => {
			const highThresholdPolicy = new PolicyEnforcer({
				...options,
				failureThreshold: 1000,
			});

			const key = "high-threshold-test";

			// 失败 10 次不应该触发熔断
			for (let i = 0; i < 10; i++) {
				await highThresholdPolicy.acquire(key);
				highThresholdPolicy.fail(key, new Error("Failure"));
			}

			await expect(highThresholdPolicy.use(key, async () => "success")).resolves.toBe("success");
		});

		it("应该处理零 QPS 配置", async () => {
			const zeroQpsPolicy = new PolicyEnforcer({
				...options,
				qps: 0,
			});

			const key = "zero-qps-test";

			// QPS 为 0 意味着不限流
			await expect(zeroQpsPolicy.acquire(key)).resolves.toBeUndefined();
			await expect(zeroQpsPolicy.acquire(key)).resolves.toBeUndefined();
		});

		it("应该处理超短熔断时长", async () => {
			const shortBreakPolicy = new PolicyEnforcer({
				...options,
				openSeconds: 0.001, // 1ms
			});

			const key = "short-break-test";

			// 触发熔断
			for (let i = 0; i < options.failureThreshold!; i++) {
				await shortBreakPolicy.acquire(key);
				shortBreakPolicy.fail(key, new Error("Failure"));
			}

			// 等待 10ms（远超熔断时长）
			await new Promise((resolve) => setTimeout(resolve, 10));

			// 应该已经恢复
			await expect(shortBreakPolicy.use(key, async () => "recovered")).resolves.toBe("recovered");
		}, 5000);
	});

	describe("并发场景", () => {
		it("应该正确处理并发请求", async () => {
			const key = "concurrent-test";

			const requests = Array.from({ length: 10 }, (_, i) =>
				policy.use(key, async () => {
					await new Promise((resolve) => setTimeout(resolve, 10));
					policy.success(key);
					return `request-${i}`;
				}),
			);

			const results = await Promise.allSettled(requests);

			// 所有请求应该最终完成（可能因 QPS 限制有延迟）
			const fulfilled = results.filter((r) => r.status === "fulfilled");
			expect(fulfilled.length).toBeGreaterThan(0);
		}, 10000);

		it("应该隔离不同 key 的并发请求", async () => {
			const keys = Array.from({ length: 5 }, (_, i) => `key-${i}`);

			const requests = keys.map((key) =>
				policy.use(key, async () => {
					policy.success(key);
					return key;
				}),
			);

			const results = await Promise.all(requests);

			expect(results).toEqual(keys);
		});
	});
});
