/* 中文注释：随机化工具（抖动、百分比方差、键盘邻接） */
import { randomNormal, randomUniform } from "./distributions.js";

export function jitter(base: number, variance: number, min = 0, max = Infinity): number {
	const result = base + randomNormal(0, variance);
	return Math.max(min, Math.min(max, result));
}

export function randomVariance(base: number, variancePercent: number): number {
	const factor = 1 + randomUniform(-variancePercent, variancePercent);
	return base * factor;
}

// 生成微抖动路径点（绕中心点的小幅随机游走，逐步衰减）
export interface Point {
	x: number;
	y: number;
}
export function microJitterPoints(
	center: Point,
	amplitudePx = 0.8,
	count = 6,
	decay = 0.85,
): Point[] {
	const pts: Point[] = [];
	let amp = Math.max(0, amplitudePx);
	for (let i = 0; i < Math.max(0, count); i++) {
		const theta = randomUniform(0, Math.PI * 2);
		const r = randomNormal(0, amp);
		pts.push({ x: center.x + Math.cos(theta) * r, y: center.y + Math.sin(theta) * r });
		amp *= decay;
	}
	return pts;
}

export const QWERTY_NEIGHBORS: Record<string, string[]> = {
	a: ["q", "w", "s", "z"],
	b: ["v", "g", "h", "n"],
	c: ["x", "d", "f", "v"],
	d: ["s", "e", "r", "f", "c", "x"],
	e: ["w", "s", "d", "r"],
	f: ["d", "r", "t", "g", "v", "c"],
	g: ["f", "t", "y", "h", "b", "v"],
	h: ["g", "y", "u", "j", "n", "b"],
	i: ["u", "j", "k", "o"],
	j: ["h", "u", "i", "k", "m", "n"],
	k: ["j", "i", "o", "l", "m"],
	l: ["k", "o", "p"],
	m: ["n", "j", "k"],
	n: ["b", "h", "j", "m"],
	o: ["i", "k", "l", "p"],
	p: ["o", "l"],
	q: ["w", "a"],
	r: ["e", "d", "f", "t"],
	s: ["a", "w", "e", "d", "x", "z"],
	t: ["r", "f", "g", "y"],
	u: ["y", "h", "j", "i"],
	v: ["c", "f", "g", "b"],
	w: ["q", "a", "s", "e"],
	x: ["z", "s", "d", "c"],
	y: ["t", "g", "h", "u"],
	z: ["a", "s", "x"],
};

export function getNeighborKey(key: string): string | undefined {
	const list = QWERTY_NEIGHBORS[key.toLowerCase()];
	if (!list || list.length === 0) return undefined;
	const index = Math.floor(randomUniform(0, list.length));
	return list[index];
}
