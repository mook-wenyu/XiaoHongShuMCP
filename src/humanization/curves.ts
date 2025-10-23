/* 中文注释：生成鼠标移动路径（线性/贝塞尔近似），用于 mouse.move 的 steps */
export interface Point { x: number; y: number }

function clamp(n: number, min: number, max: number) { return Math.max(min, Math.min(max, n)); }

export function lerp(a: number, b: number, t: number) { return a + (b - a) * t; }

export function quadraticBezier(p0: Point, p1: Point, p2: Point, t: number): Point {
	const u = 1 - t;
	return { x: u * u * p0.x + 2 * u * t * p1.x + t * t * p2.x, y: u * u * p0.y + 2 * u * t * p1.y + t * t * p2.y };
}

export interface PathOptions { steps?: number; randomness?: number }

export function makeCurvePath(start: Point, end: Point, opts: PathOptions = {}): Point[] {
	const steps = clamp(Math.floor(opts.steps ?? 25), 5, 200);
	const rnd = clamp(opts.randomness ?? 0.15, 0, 0.8);
	const mid: Point = { x: (start.x + end.x) / 2, y: (start.y + end.y) / 2 };
	const dx = end.x - start.x;
	const dy = end.y - start.y;
	const ortho: Point = { x: -dy, y: dx };
	const mag = Math.sqrt(ortho.x * ortho.x + ortho.y * ortho.y) || 1;
	const unit: Point = { x: ortho.x / mag, y: ortho.y / mag };
	const bend = (Math.hypot(dx, dy) || 1) * rnd * (Math.random() * 0.6 + 0.4);
	const ctrl: Point = { x: mid.x + unit.x * bend * (Math.random() * 2 - 1), y: mid.y + unit.y * bend * (Math.random() * 2 - 1) };
	const pts: Point[] = [];
	for (let i = 0; i <= steps; i++) pts.push(quadraticBezier(start, ctrl, end, i / steps));
	return pts;
}

export function makeLinearPath(start: Point, end: Point, steps = 20): Point[] {
	const pts: Point[] = [];
	for (let i = 0; i <= steps; i++) pts.push({ x: lerp(start.x, end.x, i / steps), y: lerp(start.y, end.y, i / steps) });
	return pts;
}
