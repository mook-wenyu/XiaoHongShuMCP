/* 中文注释：拟人化行为档案（行为节律参数预设） */

export type BehaviorProfileKey = 'default' | 'cautious' | 'rapid'

export interface BehaviorProfile {
  mouseSteps: number         // 鼠标曲线步进
  mouseRandomness: number    // 鼠标曲线抖动（0~1）
  wpm: number                // 键入速率（词/分钟），typeHuman 将换算为每字延时
  scrollSegments: number     // 滚动分段数
  scrollJitterPx: number     // 每段滚动的像素抖动
  scrollPerSegmentMs: number // 每段滚动的基础延时
}

const profiles: Record<BehaviorProfileKey, BehaviorProfile> = {
  default:  { mouseSteps: 25, mouseRandomness: 0.2, wpm: 180, scrollSegments: 6,  scrollJitterPx: 20, scrollPerSegmentMs: 120 },
  cautious: { mouseSteps: 35, mouseRandomness: 0.25, wpm: 140, scrollSegments: 8,  scrollJitterPx: 24, scrollPerSegmentMs: 180 },
  rapid:    { mouseSteps: 18, mouseRandomness: 0.15, wpm: 220, scrollSegments: 4,  scrollJitterPx: 16, scrollPerSegmentMs: 80  }
}

export function getProfile(key?: string | null): BehaviorProfile {
  const k = (key as BehaviorProfileKey) ?? 'default'
  return profiles[k] ?? profiles.default
}
