import { describe, it, expect, beforeEach } from "vitest";
import { appendHealth } from "../../../src/selectors/health-sink.js";
import { readFile } from "node:fs/promises";

const OUT = process.env.SELECTOR_HEALTH_PATH || "artifacts/selector-health.ndjson";

describe("health-sink batching (smoke)", () => {
	beforeEach(async () => {
		// no cleanup to avoid interfering other tests; just append
	});

	it("appends multiple records without throwing (buffered)", async () => {
		// write a few lines
		for (let i = 0; i < 5; i++) {
			await appendHealth({
				ts: Date.now(),
				selectorId: "batch.test",
				ok: true,
				durationMs: 10 + i,
			});
		}
		// small delay to allow flush timers
		await new Promise((r) => setTimeout(r, 600));
		const raw = await readFile(OUT, "utf-8").catch(() => "");
		expect(raw.includes("batch.test")).toBe(true);
	});
});
