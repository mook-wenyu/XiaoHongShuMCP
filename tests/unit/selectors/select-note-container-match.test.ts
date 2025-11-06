import { describe, it, expect } from "vitest";

const norm = (s: string) => (s || "").trim().replace(/\s+/g, " ").toLowerCase();
const hitAnyContainer = (containerText: string, keywords: string[]) => {
  const tn = norm(containerText);
  const ks = keywords.map(k => norm(k)).filter(Boolean);
  return ks.findIndex(k => tn.includes(k)) >= 0;
};

describe("xhs: container full-text matching (ANY-of)", () => {
  it("matches against normalized container text regardless of title presence", () => {
    const container = "封面\n\n独立游戏招募一名unity程序\n作者 时间 点赞";
    expect(hitAnyContainer(container, ["unity", "设计"])).toBe(true);
    expect(hitAnyContainer(container, ["穿搭", "美食"])).toBe(false);
  });
});
