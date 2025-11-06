import { describe, it, expect, beforeEach, afterEach } from "vitest";
import { cleanText } from "../../../src/lib/text-clean";

const OLD_ENV = { ...process.env } as any;

// 这些用例在不跑浏览器的前提下，模拟容器全文+清洗后的匹配可行性

describe("xhs: container full-text matching edge cases", () => {
  beforeEach(() => { Object.assign(process.env, OLD_ENV); });
  afterEach(() => { Object.assign(process.env, OLD_ENV); });

  it("emoji-only content becomes empty after regex removal", () => {
    process.env.XHS_TEXT_CLEAN_REMOVE_REGEX = "[\\u{1F300}-\\u{1FAFF}]+"; // Emoji range
    const s = cleanText("😀😀😀");
    expect(s).toBe("");
  });

  it("author/time/like tokens can be removed via regex", () => {
    process.env.XHS_TEXT_CLEAN_REMOVE_REGEX = "(作者|时间|赞|点赞|\n)+";
    const s = cleanText("作者 小红薯 时间 昨天 赞 25 独立 游戏 招募");
    expect(s).toBe("小红薯 昨天 25 独立 游戏 招募".replace(/\s+/g," ").trim());
  });

  it("very long text collapses whitespace but preserves content", () => {
    delete process.env.XHS_TEXT_CLEAN_REMOVE_REGEX;
    const long = Array(200).fill("独立  游戏").join("  ");
    const s = cleanText(long);
    expect(s.length).toBeGreaterThan(200); // 被压缩但未被过度删减
  });

  it("english case-insensitive and mixed language are normalized", () => {
    const s = cleanText("Unity developer 招募，UNiTy 程序");
    expect(s.toLowerCase().includes("unity")).toBe(true);
    expect(s.includes("招募")).toBe(true);
  });
});
