/* 单元测试：HTML 抓取质量验证 */
import { describe, it, expect } from "vitest";
import { validateCaptureQuality, progressiveWait } from "../../../../src/services/scraper/quality.js";
import type { Page } from "playwright";

// Mock Page 对象
function createMockPage(options: {
  htmlLength?: number;
  contentLength?: number;
  hasTitle?: boolean;
  hasImages?: boolean;
}): Page {
  const {
    htmlLength = 15000,
    contentLength = 500,
    hasTitle = true,
    hasImages = true,
  } = options;

  return {
    content: async () => "x".repeat(htmlLength),
    evaluate: async (_fn: any) => {
      // 模拟浏览器环境执行
      return {
        contentLength,
        hasTitle,
        hasImages,
      };
    },
    waitForLoadState: async () => Promise.resolve(),
    waitForFunction: async () => Promise.resolve(),
  } as any;
}

describe("validateCaptureQuality", () => {
  it("应该正确计算高质量内容的分数", async () => {
    const mockPage = createMockPage({
      htmlLength: 15000, // 25 分
      contentLength: 500, // 35 分（> 200 * 2）
      hasTitle: true, // 20 分
      hasImages: true, // 20 分
    });

    const quality = await validateCaptureQuality(mockPage);

    expect(quality.htmlLength).toBe(15000);
    expect(quality.contentLength).toBe(500);
    expect(quality.hasTitle).toBe(true);
    expect(quality.hasImages).toBe(true);
    expect(quality.score).toBe(100); // 满分
  });

  it("应该正确计算中等质量内容的分数", async () => {
    const mockPage = createMockPage({
      htmlLength: 6000, // 15 分（5000-10000）
      contentLength: 250, // 25 分（> 200）
      hasTitle: true, // 20 分
      hasImages: false, // 0 分
    });

    const quality = await validateCaptureQuality(mockPage);

    expect(quality.score).toBe(60); // 15 + 25 + 20 + 0
  });

  it("应该正确计算低质量内容的分数", async () => {
    const mockPage = createMockPage({
      htmlLength: 800, // 0 分（< 1000）
      contentLength: 50, // 0 分（< 100）
      hasTitle: false, // 0 分
      hasImages: false, // 0 分
    });

    const quality = await validateCaptureQuality(mockPage);

    expect(quality.score).toBe(0); // 最低分
  });

  it("应该正确计算部分质量内容的分数", async () => {
    const mockPage = createMockPage({
      htmlLength: 2000, // 5 分（1000-5000）
      contentLength: 120, // 10 分（> 100，< 200）
      hasTitle: true, // 20 分
      hasImages: true, // 20 分
    });

    const quality = await validateCaptureQuality(mockPage);

    expect(quality.score).toBe(55); // 5 + 10 + 20 + 20
  });

  it("应该处理空内容", async () => {
    const mockPage = createMockPage({
      htmlLength: 0,
      contentLength: 0,
      hasTitle: false,
      hasImages: false,
    });

    const quality = await validateCaptureQuality(mockPage);

    expect(quality.score).toBe(0);
    expect(quality.htmlLength).toBe(0);
    expect(quality.contentLength).toBe(0);
  });
});

describe("progressiveWait", () => {
  it("应该在网络空闲等待超时后继续执行", async () => {
    let waitForLoadStateCalled = false;
    const mockPage = {
      waitForLoadState: async () => {
        waitForLoadStateCalled = true;
        throw new Error("Timeout");
      },
      waitForFunction: async () => Promise.resolve(),
    } as any;

    // 不应抛出错误
    await expect(progressiveWait(mockPage)).resolves.toBeUndefined();
    expect(waitForLoadStateCalled).toBe(true);
  });

  it("应该在内容长度等待超时后继续执行", async () => {
    let waitForFunctionCalled = false;
    const mockPage = {
      waitForLoadState: async () => Promise.resolve(),
      waitForFunction: async () => {
        waitForFunctionCalled = true;
        throw new Error("Timeout");
      },
    } as any;

    // 不应抛出错误
    await expect(progressiveWait(mockPage)).resolves.toBeUndefined();
    expect(waitForFunctionCalled).toBe(true);
  });

  it("应该正常完成所有等待阶段", async () => {
    let loadStateResolved = false;
    let functionResolved = false;

    const mockPage = {
      waitForLoadState: async () => {
        loadStateResolved = true;
        return Promise.resolve();
      },
      waitForFunction: async () => {
        functionResolved = true;
        return Promise.resolve();
      },
    } as any;

    await progressiveWait(mockPage);

    expect(loadStateResolved).toBe(true);
    expect(functionResolved).toBe(true);
  });

  it("应该在所有等待都超时时正常完成", async () => {
    const mockPage = {
      waitForLoadState: async () => {
        throw new Error("networkidle timeout");
      },
      waitForFunction: async () => {
        throw new Error("content timeout");
      },
    } as any;

    // 所有超时都应被捕获，不抛出错误
    await expect(progressiveWait(mockPage)).resolves.toBeUndefined();
  });
});

describe("质量分数边界条件", () => {
  it("应该在 HTML 长度恰好 10000 时给予满分", async () => {
    const mockPage = createMockPage({
      htmlLength: 10001,
      contentLength: 500,
      hasTitle: true,
      hasImages: true,
    });

    const quality = await validateCaptureQuality(mockPage);
    expect(quality.score).toBe(100);
  });

  it("应该在 HTML 长度恰好 5000 时给予部分分数", async () => {
    const mockPage = createMockPage({
      htmlLength: 5001,
      contentLength: 500,
      hasTitle: true,
      hasImages: true,
    });

    const quality = await validateCaptureQuality(mockPage);
    expect(quality.score).toBe(90); // 15 (html 5000-10000) + 35 + 20 + 20
  });

  it("应该在内容长度恰好达标时给予满分", async () => {
    const mockPage = createMockPage({
      htmlLength: 15000,
      contentLength: 401, // > 200 * 2
      hasTitle: true,
      hasImages: true,
    });

    const quality = await validateCaptureQuality(mockPage);
    expect(quality.score).toBe(100);
  });
});
