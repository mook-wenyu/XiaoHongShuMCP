import { describe, it, expect, beforeEach, afterEach } from "vitest";
import { cleanText } from "../../../src/lib/text-clean";

const OLD_ENV = { ...process.env } as any;

describe("text-clean", () => {
  beforeEach(() => { Object.assign(process.env, OLD_ENV); });
  afterEach(() => { Object.assign(process.env, OLD_ENV); });

  it("removes default punctuation and collapses whitespace", () => {
    delete process.env.XHS_TEXT_CLEAN_REMOVE_REGEX;
    delete process.env.XHS_TEXT_CLEAN_REMOVE_CHARS;
    const s = cleanText(" 独立  游戏，招募！  Unity 程序\n");
    expect(s).toBe("独立 游戏招募 Unity 程序");
  });

  it("respects custom remove chars", () => {
    process.env.XHS_TEXT_CLEAN_REMOVE_CHARS = "独";
    const s = cleanText("独立 游戏");
    expect(s).toBe("立 游戏");
  });

  it("respects custom regex", () => {
    process.env.XHS_TEXT_CLEAN_REMOVE_REGEX = "[0-9]+";
    const s = cleanText("abc123def");
    expect(s).toBe("abcdef");
  });
});
