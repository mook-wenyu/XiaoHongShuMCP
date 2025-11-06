/* 中文注释：选择器健康度监控系统（记录成功率、耗时和使用情况） */

export interface SelectorHealth {
	selectorId: string;
	totalCount: number;
	successCount: number;
	failureCount: number;
	successRate: number; // 0-1
	avgDurationMs: number;
	p95DurationMs?: number; // 新增：P95
	lastUsed: Date;
}

interface SelectorStats {
	total: number;
	success: number;
	durations: number[]; // 最近 100 次
	lastUsed: Date;
}

/**
 * 选择器健康度监控器
 * 用于记录和分析选择器的性能和可靠性
 */
export class SelectorHealthMonitor {
	private stats = new Map<string, SelectorStats>();
	private readonly maxDurationsLength = 100;

	/**
	 * 记录选择器使用情况
	 * @param selectorId 选择器唯一标识（如 "searchInput"）
	 * @param success 是否成功
	 * @param durationMs 执行耗时（毫秒）
	 */
	record(selectorId: string, success: boolean, durationMs: number): void {
		const existing = this.stats.get(selectorId);

		if (existing) {
			existing.total++;
			if (success) existing.success++;
			existing.durations.push(durationMs);

			// 保持最近 100 次记录，超出则删除最旧的
			if (existing.durations.length > this.maxDurationsLength) {
				existing.durations.shift();
			}

			existing.lastUsed = new Date();
		} else {
			this.stats.set(selectorId, {
				total: 1,
				success: success ? 1 : 0,
				durations: [durationMs],
				lastUsed: new Date(),
			});
		}
	}

	/**
	 * 获取指定选择器的健康度数据
	 * @param selectorId 选择器唯一标识
	 * @returns 健康度数据，不存在则返回 undefined
	 */
	getHealth(selectorId: string): SelectorHealth | undefined {
		const stats = this.stats.get(selectorId);
		if (!stats) return undefined;

		const failureCount = stats.total - stats.success;
		const successRate = stats.total > 0 ? stats.success / stats.total : 0;

		// 计算平均耗时
		const avgDurationMs =
			stats.durations.length > 0
				? stats.durations.reduce((sum, d) => sum + d, 0) / stats.durations.length
				: 0;
		// 计算 P95（最近样本，升序取 ceil(0.95*n)-1）
		let p95: number | undefined = undefined;
		if (stats.durations.length > 0) {
			const sorted = [...stats.durations].sort((a, b) => a - b);
			const idx = Math.min(sorted.length - 1, Math.max(0, Math.ceil(sorted.length * 0.95) - 1));
			p95 = sorted[idx];
		}

		return {
			selectorId,
			totalCount: stats.total,
			successCount: stats.success,
			failureCount,
			successRate,
			avgDurationMs,
			p95DurationMs: p95,
			lastUsed: stats.lastUsed,
		};
	}

	/**
	 * 报告健康度低于阈值的选择器
	 * @param threshold 成功率阈值（默认 0.7，即 70%）
	 * @returns 不健康的选择器列表
	 */
	reportUnhealthy(threshold = 0.7): SelectorHealth[] {
		const unhealthy: SelectorHealth[] = [];

		for (const selectorId of this.stats.keys()) {
			const health = this.getHealth(selectorId);
			if (health && health.successRate < threshold) {
				unhealthy.push(health);
			}
		}

		// 按成功率升序排序（最不健康的排在前面）
		return unhealthy.sort((a, b) => a.successRate - b.successRate);
	}

	/**
	 * 导出所有选择器的健康度数据
	 * @returns 选择器 ID 到健康度数据的映射
	 */
	export(): Record<string, SelectorHealth> {
		const result: Record<string, SelectorHealth> = {};

		for (const selectorId of this.stats.keys()) {
			const health = this.getHealth(selectorId);
			if (health) {
				result[selectorId] = health;
			}
		}

		return result;
	}

	/**
	 * 获取所有选择器的健康度数据（数组形式）
	 * @returns 所有选择器的健康度数据数组
	 */
	getAll(): SelectorHealth[] {
		const result: SelectorHealth[] = [];

		for (const selectorId of this.stats.keys()) {
			const health = this.getHealth(selectorId);
			if (health) {
				result.push(health);
			}
		}

		return result;
	}

	/**
	 * 清空所有统计数据（主要用于测试）
	 */
	clear(): void {
		this.stats.clear();
	}

	/**
	 * 获取当前跟踪的选择器数量
	 */
	size(): number {
		return this.stats.size;
	}
}

// 全局单例实例
export const healthMonitor = new SelectorHealthMonitor();
