/* 中文注释：humanization 核心微基准（曲线/缓动/随机化/键入延迟） */
import { Bench } from "tinybench";
import { makeCurvePath } from "../src/humanization/curves.js";
import { easeInOutCubic } from "../src/humanization/core/timing.js";
import { charDelayByWPM, jitter } from "../src/humanization/delays.js";
import { writeFile, mkdir } from "node:fs/promises";

async function main() {
	const bench = new Bench({ time: 100, iterations: 100 });

	bench.add("curve-100px-steps30", () => {
		makeCurvePath({ x: 0, y: 0 }, { x: 100, y: 0 }, { steps: 30, randomness: 0.2 });
	});

	bench.add("easing-cubic-1k", () => {
		let acc = 0;
		for (let i = 0; i < 1000; i++) acc += easeInOutCubic(i / 999);
		return acc;
	});

	bench.add("typing-delay-240wpm-1k", () => {
		const d = charDelayByWPM(240, 0);
		for (let i = 0; i < 1000; i++) d("a");
	});

	bench.add("jitter-1k", () => {
		for (let i = 0; i < 1000; i++) jitter(100, 20, 10, 200);
	});

	await bench.run();

	const results = bench.tasks.map((t) => ({
		name: t.name,
		hz: t.result?.hz,
		mean: t.result?.mean,
		min: t.result?.min,
		max: t.result?.max,
		variance: t.result?.variance,
		samples: t.result?.samples.length,
	}));

	await mkdir("artifacts", { recursive: true });
	const path = `artifacts/metrics-${Date.now()}.json`;
	await writeFile(
		path,
		JSON.stringify({ date: new Date().toISOString(), results }, null, 2),
		"utf-8",
	);
	// eslint-disable-next-line no-console
	console.log(JSON.stringify({ ok: true, path }, null, 2));
}

main().catch((err) => {
	console.error(err);
	process.exit(1);
});
