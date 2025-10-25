import { describe, it, expect } from "vitest";
import { resolveLocatorAsync } from "../src/selectors/selector.js";

describe("selector hints", () => {
  it("throws when no hints", async () => {
    // 仅验证函数健壮性（不实际创建 page），使用假对象触发错误分支
    const fake: any = { getByRole(){}, getByLabel(){}, getByPlaceholder(){}, getByTestId(){}, getByText(){}, locator(){} };
    await expect(resolveLocatorAsync(fake, {} as any)).rejects.toThrowError();
  });
});
