/* 中文注释：输入与等待的延迟分布工具 */
export interface DelayProfile {
	baseMs?: number;
	jitterMs?: number;
	minMs?: number;
	maxMs?: number;
}

export function jitter(ms: number, jitterMs = 30, minMs = 5, maxMs = 2000) {
	const val = Math.round(ms + (Math.random() * 2 - 1) * jitterMs);
	return Math.max(minMs, Math.min(maxMs, val));
}

export function charDelayByWPM(wpm = 180, jitterMs = 40) {
	// 近似：1 个英文字符 ~ 5 个/词；wpm → 每字符毫秒
	const cps = (wpm * 5) / 60;
	const msPerChar = Math.max(15, Math.round(1000 / cps));
	return (ch: string) => jitter(msPerChar + (/[.,!?:;]/.test(ch) ? 120 : 0), jitterMs, 15, 600);
}

export function sleep(ms: number) {
	return new Promise((r) => setTimeout(r, ms));
}
