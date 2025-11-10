/* 中文注释：XhsSelectors 映射定义（纯单元测试）
 * 目标：覆盖 xhs.ts 中的语义映射定义，提升行覆盖率。
 */
import { describe, it, expect } from "vitest";
import { XhsSelectors } from "../../../src/selectors/xhs.js";

describe("XhsSelectors 语义映射定义（纯形态校验）", () => {
  it("searchInput/searchSubmit/navDiscover/noteAnchor/noteModal* 均返回 alternatives", () => {
    const s1 = XhsSelectors.searchInput();
    const s2 = XhsSelectors.searchSubmit();
    const s3 = XhsSelectors.navDiscover();
    const s4 = XhsSelectors.noteAnchor();
    const s5 = XhsSelectors.noteModalMask();
    const s6 = XhsSelectors.noteModalClose();
    for (const s of [s1, s2, s3, s4, s5, s6]) {
      expect(Array.isArray((s as any).alternatives)).toBe(true);
      expect((s as any).alternatives.length).toBeGreaterThan(0);
    }
  });
});

