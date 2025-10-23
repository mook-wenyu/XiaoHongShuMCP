/* 中文注释：action.* 工具的通用入参 Schema */
import { z } from "zod"

export const DirId = z.string().default("user")
export const WorkspaceId = z.string().optional()
export const PageIndex = z.number().int().nonnegative().optional()
// 兼容 Steps TargetHints 与直接选择器（字符串）
export const Target = z.union([z.string(), z.record(z.any())])

// 可选高级参数 Schema（保持默认行为不变）
export const EasingName = z.enum([
  "linear",
  "easeIn",
  "easeOut",
  "easeInOutQuad",
  "easeInOutCubic"
]);

export const MoveOptionsSchema = z.object({
  steps: z.number().int().positive().max(500).optional(),
  randomness: z.number().min(0).max(0.95).optional(),
  overshoot: z.boolean().optional(),
  overshootAmount: z.number().min(0).max(50).optional(),
  microJitterPx: z.number().min(0).max(3).optional(),
  microJitterCount: z.number().int().min(0).max(20).optional(),
}).optional();

export const ClickOptionsSchema = z.object({
  button: z.enum(["left","right","middle"]).optional(),
  clickCount: z.number().int().positive().max(3).optional(),
  delay: z.number().int().nonnegative().max(1000).optional(),
  // 鼠标移动轨迹控制（传递给 moveMouseTo）
  steps: z.number().int().positive().max(500).optional(),
  randomness: z.number().min(0).max(0.95).optional(),
  overshoot: z.boolean().optional(),
  overshootAmount: z.number().min(0).max(50).optional(),
  microJitterPx: z.number().min(0).max(3).optional(),
  microJitterCount: z.number().int().min(0).max(20).optional(),
}).optional();

export const ScrollOptionsSchema = z.object({
  segments: z.number().int().positive().max(60).optional(),
  jitterPx: z.number().int().nonnegative().max(200).optional(),
  perSegmentMs: z.number().int().positive().max(2000).optional(),
  easing: EasingName.optional(),
  // 停顿式滚动（默认开启，可显式关闭）
  microPauseChance: z.number().min(0).max(1).optional(),
  microPauseMinMs: z.number().int().min(0).max(2000).optional(),
  microPauseMaxMs: z.number().int().min(0).max(4000).optional(),
  macroPauseEvery: z.number().int().min(0).max(20).optional(),
  macroPauseMinMs: z.number().int().min(0).max(5000).optional(),
  macroPauseMaxMs: z.number().int().min(0).max(10000).optional(),
}).optional();

export type EasingName = z.infer<typeof EasingName>
export type MoveOptionsInput = z.infer<typeof MoveOptionsSchema>
export type ClickOptionsInput = z.infer<typeof ClickOptionsSchema>
export type ScrollOptionsInput = z.infer<typeof ScrollOptionsSchema>
