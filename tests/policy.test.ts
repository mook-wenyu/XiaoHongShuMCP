import { describe, expect, it } from "vitest";
import { PolicyEnforcer } from "../src/services/policy.js";

describe("PolicyEnforcer", () => {
	it("opens after failures then half-open then closed on success", async () => {
		const p = new PolicyEnforcer({ qps: 10, failureThreshold: 2, openSeconds: 1 });
		await p.acquire("k");
		p.fail("k");
		await p.acquire("k");
		p.fail("k"); // open
		// next acquire should wait ~1s then half-open
		const t0 = Date.now();
		await p.acquire("k");
		const waited = Date.now() - t0;
		expect(waited).toBeGreaterThanOrEqual(900);
		p.success("k"); // close
		await p.acquire("k");
		// no long wait now
	});
});
