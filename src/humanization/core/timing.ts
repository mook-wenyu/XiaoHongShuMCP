/* 中文注释：缓动函数（easing）集合 */
export type EasingFunction = (t: number) => number

export const linear: EasingFunction = (t) => t
export const easeIn: EasingFunction = (t) => t * t
export const easeOut: EasingFunction = (t) => t * (2 - t)
export const easeInQuad: EasingFunction = (t) => t * t
export const easeOutQuad: EasingFunction = (t) => t * (2 - t)
export const easeInOutQuad: EasingFunction = (t) => (t < 0.5 ? 2 * t * t : 1 - Math.pow(-2 * t + 2, 2) / 2)
export const easeInOutCubic: EasingFunction = (t) => {
  if (t < 0.5) return 4 * t * t * t
  const f = 2 * t - 2
  return 1 + (f * f * f) / 2
}
