/* 中文注释：人性化动作的统一类型定义（计划层与执行层解耦） */
export interface Point { x: number; y: number }

// 鼠标移动选项（计划层）
export interface MouseMoveOptions {
  steps?: number;            // 路径离散步数
  randomness?: number;       // 路径弯曲随机度（0..0.95）
  overshoot?: boolean;       // 是否允许末端轻微过冲
  overshootAmount?: number;  // 过冲像素幅度
  microJitterPx?: number;    // 目标附近微抖动幅度（默认≈0.6）
  microJitterCount?: number; // 微抖动次数（默认≈4）
}

export interface MouseClickOptions extends MouseMoveOptions {
  button?: "left" | "right" | "middle";
  clickCount?: number;
  delay?: number;            // Playwright click delay(ms)
}

// 滚动计划的每一段（delta 为像素增量，waitMs 为段间等待）
export interface ScrollSegment { delta: number; waitMs: number }

export interface ScrollPlanOptions {
  segments?: number;         // 分段数
  jitterPx?: number;         // 每段增量的抖动像素
  perSegmentMs?: number;     // 基础等待（未给则使用经验随机）
  easingName?: "linear"|"easeIn"|"easeOut"|"easeInOutQuad"|"easeInOutCubic";
  microPauseChance?: number; // 微停顿几率（0..1）
  microPauseMinMs?: number;  // 微停顿最小
  microPauseMaxMs?: number;  // 微停顿最大
  macroPauseEvery?: number;  // 每 N 段宏停顿（=0 关闭）
  macroPauseMinMs?: number;  // 宏停顿最小
  macroPauseMaxMs?: number;  // 宏停顿最大
}
