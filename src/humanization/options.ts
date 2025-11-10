/* 中文注释：人类化动作参数构建工具（提高内聚，便于复用与测试）
 * 职责：
 * - 依据给定 human 参数与全局拟人化档位（container.getHumanizationProfileKey）构造鼠标移动与滚动选项
 * - 提供默认值与边界约束，避免业务处重复拼装
 */
import type { ServiceContainer } from "../core/container.js";
import { getProfile } from "./profiles.js";

export function buildMouseMoveOptions(human: any | undefined, container: ServiceContainer) {
	const p = getProfile(human?.profile ?? container.getHumanizationProfileKey());
	return {
		steps: human?.steps ?? p.mouseSteps,
		randomness: human?.randomness ?? p.mouseRandomness,
		overshoot: human?.overshoot ?? true,
		overshootAmount: human?.overshootAmount ?? 12,
		microJitterPx: human?.microJitterPx ?? 1.2,
		microJitterCount: human?.microJitterCount ?? 2,
	};
}

export function buildScrollOptions(human: any | undefined, container: ServiceContainer) {
	const p = getProfile(human?.profile ?? container.getHumanizationProfileKey());
	return {
		segments: human?.segments ?? human?.steps ?? p.scrollSegments,
		jitterPx: human?.jitterPx ?? p.scrollJitterPx,
		perSegmentMs: human?.perSegmentMs ?? p.scrollPerSegmentMs,
		microPauseChance: human?.microPauseChance ?? 0.25,
		microPauseMinMs: human?.microPauseMinMs ?? 60,
		microPauseMaxMs: human?.microPauseMaxMs ?? 160,
		macroPauseEvery: human?.macroPauseEvery ?? 4,
		macroPauseMinMs: human?.macroPauseMinMs ?? 120,
		macroPauseMaxMs: human?.macroPauseMaxMs ?? 260,
	};
}

