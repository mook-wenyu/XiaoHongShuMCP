/* 中文注释：三次贝塞尔曲线与过冲路径生成 */
export interface Point { x: number; y: number }
export interface CurveOptions { steps?: number; randomness?: number; overshoot?: boolean; overshootAmount?: number }

export function cubicBezier(p0: Point, p1: Point, p2: Point, p3: Point, t: number): Point {
  const u = 1 - t
  const x = u * u * u * p0.x + 3 * u * u * t * p1.x + 3 * u * t * t * p2.x + t * t * t * p3.x
  const y = u * u * u * p0.y + 3 * u * u * t * p1.y + 3 * u * t * t * p2.y + t * t * t * p3.y
  return { x, y }
}

function clamp(n: number, min: number, max: number) { return Math.max(min, Math.min(max, n)) }

export function makeCurvePath(from: Point, to: Point, opts: CurveOptions = {}): Point[] {
  if (from.x === to.x && from.y === to.y) return [from]
  const steps = clamp(Math.floor(opts.steps ?? 30), 2, 300)
  const rnd = clamp(opts.randomness ?? 0.2, 0, 0.8)

  const dx = to.x - from.x
  const dy = to.y - from.y
  const distance = Math.hypot(dx, dy) || 1

  // 控制点：靠近起终点并添加抖动
  const p1 = { x: from.x + dx * 0.3 + (Math.random() * 2 - 1) * distance * 0.1, y: from.y + dy * 0.2 + (Math.random() * 2 - 1) * distance * 0.15 }
  const p2 = { x: from.x + dx * 0.7 + (Math.random() * 2 - 1) * distance * 0.1, y: from.y + dy * 0.8 + (Math.random() * 2 - 1) * distance * 0.15 }

  const path: Point[] = []
  for (let i = 0; i < steps; i++) {
    const t = i / (steps - 1)
    const p = cubicBezier(from, p1, p2, to, t)
    // 简单随机抖动（与方向无关）
    const jitter = (Math.random() * 2 - 1) * rnd * 2
    path.push({ x: p.x + jitter, y: p.y + jitter })
  }

  if (opts.overshoot ?? true) {
    const amount = opts.overshootAmount ?? 10
    const over = { x: to.x + (dx / distance) * amount, y: to.y + (dy / distance) * amount }
    // 5 步返回目标
    for (let i = 1; i <= 5; i++) {
      const t = i / 5
      path.push({ x: over.x + (to.x - over.x) * t, y: over.y + (to.y - over.y) * t })
    }
  }
  return path
}
