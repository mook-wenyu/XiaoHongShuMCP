import { describe, it, expect } from "vitest";

const norm = (s: string) => (s || "").toLowerCase();
const hitAny = (text: string, keywords: string[]) => {
	const tn = norm(text);
	const ks = keywords.map(norm).filter(Boolean);
	return ks.findIndex((k) => tn.includes(k)) >= 0;
};

describe("xhs: keyword matching uses ANY-of semantics", () => {
	it("matches when any keyword is included (case-insensitive)", () => {
		expect(hitAny("美食攻略合集", ["穿搭", "美食"])).toBe(true);
		expect(hitAny("城市夜跑打卡", ["穿搭", "美食"])).toBe(false);
		expect(hitAny("FASHION穿搭灵感", ["穿搭", "美食"])).toBe(true);
	});
});
