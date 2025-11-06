/* 中文注释：选择器健康度报告生成 */
import { healthMonitor, type SelectorHealth } from "./health.js";
import { createLogger } from "../logging/index.js";

const log = createLogger();

export interface HealthReport {
	timestamp: string; // ISO 8601 时间戳
	totalSelectors: number; // 总选择器数量
	healthyCount: number; // 健康选择器数量（成功率 >= 阈值）
	unhealthyCount: number; // 不健康选择器数量
	averageSuccessRate: number; // 平均成功率
	selectors: SelectorHealth[]; // 所有选择器详情
	unhealthySelectors: SelectorHealth[]; // 不健康选择器详情
	recommendations: string[]; // 优化建议
}

export interface ReportOptions {
	unhealthyThreshold?: number; // 不健康阈值（默认 0.7）
	minSampleSize?: number; // 最小样本数（默认 5）
	includeHealthy?: boolean; // 是否包含健康选择器（默认 true）
}

/**
 * 生成选择器健康度报告
 * @param opts 报告选项
 * @returns 健康度报告对象
 */
export function generateHealthReport(opts: ReportOptions = {}): HealthReport {
	const unhealthyThreshold = opts.unhealthyThreshold ?? 0.7;
	const minSampleSize = opts.minSampleSize ?? 5;
	const includeHealthy = opts.includeHealthy ?? true;

	const allSelectors = healthMonitor.getAll();

	// 过滤掉样本数不足的选择器
	const validSelectors = allSelectors.filter((s: SelectorHealth) => s.totalCount >= minSampleSize);

	const unhealthySelectors = healthMonitor.reportUnhealthy(unhealthyThreshold);

	// 只保留样本数足够的不健康选择器
	const validUnhealthy = unhealthySelectors.filter((s) => s.totalCount >= minSampleSize);

	const healthyCount = validSelectors.length - validUnhealthy.length;
	const unhealthyCount = validUnhealthy.length;

	// 计算平均成功率
	const avgSuccessRate =
		validSelectors.length > 0
			? validSelectors.reduce((sum: number, s: SelectorHealth) => sum + s.successRate, 0) /
				validSelectors.length
			: 0;

	// 生成优化建议
	const recommendations = generateRecommendations(validUnhealthy);

	return {
		timestamp: new Date().toISOString(),
		totalSelectors: validSelectors.length,
		healthyCount,
		unhealthyCount,
		averageSuccessRate: avgSuccessRate,
		selectors: includeHealthy ? validSelectors : validUnhealthy,
		unhealthySelectors: validUnhealthy,
		recommendations,
	};
}

/**
 * 生成优化建议
 * @param unhealthy 不健康选择器列表
 * @returns 建议列表
 */
function generateRecommendations(unhealthy: SelectorHealth[]): string[] {
	const recommendations: string[] = [];

	if (unhealthy.length === 0) {
		recommendations.push("所有选择器运行正常，无需优化");
		return recommendations;
	}

	// 按成功率排序，找出最差的选择器
	const sorted = [...unhealthy].sort((a, b) => a.successRate - b.successRate);
	const worst = sorted[0];

	if (worst.successRate < 0.3) {
		recommendations.push(
			`选择器 "${worst.selectorId}" 成功率过低（${(worst.successRate * 100).toFixed(1)}%），建议立即检查选择器定义是否正确`,
		);
	}

	// 检查是否有平均耗时过长的选择器
	const slowSelectors = unhealthy.filter((s) => s.avgDurationMs > 2000);
	if (slowSelectors.length > 0) {
		recommendations.push(
			`${slowSelectors.length} 个选择器平均耗时超过 2 秒，建议优化选择器性能或增加超时配置`,
		);
	}

	// 检查是否有多个选择器都不健康
	if (unhealthy.length > 3) {
		recommendations.push(
			`共有 ${unhealthy.length} 个选择器不健康，建议全面审查选择器策略或检查页面结构是否发生变化`,
		);
	}

	// 针对每个不健康选择器生成具体建议
	for (const s of sorted.slice(0, 3)) {
		// 只显示前 3 个
		if (s.successRate < 0.5) {
			recommendations.push(
				`选择器 "${s.selectorId}" 需要优化：成功率 ${(s.successRate * 100).toFixed(1)}%，平均耗时 ${s.avgDurationMs.toFixed(0)}ms`,
			);
		}
	}

	return recommendations;
}

/**
 * 将健康度报告导出为 JSON 格式（用于监控系统集成）
 * @param report 健康度报告
 * @returns JSON 字符串
 */
export function exportReportAsJson(report: HealthReport): string {
	return JSON.stringify(report, null, 2);
}

/**
 * 记录健康度报告到日志
 * @param report 健康度报告
 */
export function logHealthReport(report: HealthReport): void {
	log.info(
		{
			totalSelectors: report.totalSelectors,
			healthyCount: report.healthyCount,
			unhealthyCount: report.unhealthyCount,
			avgSuccessRate: report.averageSuccessRate,
		},
		"选择器健康度报告",
	);

	if (report.unhealthySelectors.length > 0) {
		log.warn(
			{
				unhealthy: report.unhealthySelectors.map((s) => ({
					id: s.selectorId,
					successRate: s.successRate,
					avgDurationMs: s.avgDurationMs,
				})),
			},
			`发现 ${report.unhealthySelectors.length} 个不健康选择器`,
		);
	}

	if (report.recommendations.length > 0) {
		log.info({ recommendations: report.recommendations }, "优化建议");
	}
}

/**
 * 定期生成健康度报告（用于监控）
 * @param intervalMs 报告间隔（毫秒）
 * @param opts 报告选项
 * @returns 停止函数
 */
export function scheduleHealthReport(intervalMs: number, opts: ReportOptions = {}): () => void {
	const timer = setInterval(() => {
		const report = generateHealthReport(opts);
		logHealthReport(report);
	}, intervalMs);

	return () => clearInterval(timer);
}
