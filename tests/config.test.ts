import { describe, expect, it } from "vitest";
import { ConfigSchema } from "../src/config/schema.js";

describe("ConfigSchema", () => {
	it("parses baseURL or host+port with policy defaults", () => {
		const parsed = ConfigSchema.safeParse({
			ROXY_API_TOKEN: "t",
			ROXY_API_HOST: "127.0.0.1",
			ROXY_API_PORT: "50000",
		});
		expect(parsed.success).toBe(true);
		const env: any = parsed.success ? parsed.data : {};
		expect(env.POLICY_QPS).toBeGreaterThan(0);
		expect(env.POLICY_FAILURES).toBeGreaterThan(0);
		expect(env.POLICY_OPEN_SECONDS).toBeGreaterThan(0);
	});
});
