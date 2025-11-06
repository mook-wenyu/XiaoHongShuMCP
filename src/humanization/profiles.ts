/* 中文注释：拟人化行为档案（行为节律参数预设） */

export type BehaviorProfileKey = "default" | "cautious" | "rapid";

export interface BehaviorProfile {
	mouseSteps: number; // 鼠标曲线步进
	mouseRandomness: number; // 鼠标曲线抖动（0~1）
	wpm: number; // 键入速率（词/分钟），typeHuman 将换算为每字延时
	scrollSegments: number; // 滚动分段数
	scrollJitterPx: number; // 每段滚动的像素抖动
	scrollPerSegmentMs: number; // 每段滚动的基础延时
}

const profiles: Record<BehaviorProfileKey, BehaviorProfile> = {
	// 平衡：移动/滚动/输入节律相对自然，适合多数场景
	default: {
		mouseSteps: 24,
		mouseRandomness: 0.2,
		wpm: 110,
		scrollSegments: 6,
		scrollJitterPx: 20,
		scrollPerSegmentMs: 110,
	},
	// 稳健：更慢、更稳，适合对可读性/稳定性敏感的流程
	cautious: {
		mouseSteps: 32,
		mouseRandomness: 0.23,
		wpm: 85,
		scrollSegments: 8,
		scrollJitterPx: 24,
		scrollPerSegmentMs: 150,
	},
	// 高效：更快的移动与输入，节奏偏紧凑
	rapid: {
		mouseSteps: 18,
		mouseRandomness: 0.16,
		wpm: 140,
		scrollSegments: 4,
		scrollJitterPx: 16,
		scrollPerSegmentMs: 90,
	},
};

export function getProfile(key?: string | null): BehaviorProfile {
	const k = (key as BehaviorProfileKey) ?? "default";
	return profiles[k] ?? profiles.default;
}
