# 详情 URL 识别器 (detail-url)

## 概述

`detail-url` 模块提供统一的小红书笔记详情 URL 识别规则，用于判断当前 URL 是否为笔记详情页。

## 特性

- ✅ **多路径支持**：覆盖搜索结果、发现页、历史路径、问答、视频等形态
- ✅ **正则匹配**：基于正则表达式的高效匹配
- ✅ **错误容错**：异常输入自动返回 false，不会抛出错误
- ✅ **类型安全**：完整的 TypeScript 类型定义

## 设计原则

- **集中管理**：所有 URL 识别规则集中在一个模块
- **易于维护**：新增路径只需修改正则表达式
- **性能优先**：简单高效的正则匹配，无副作用

## 使用方法

### 基础用法

```typescript
import { isDetailUrl } from "./domain/xhs/detail-url.ts";

// 检查是否为详情页 URL
const url1 = "https://www.xiaohongshu.com/explore/6789abcdef";
console.log(isDetailUrl(url1)); // true

const url2 = "https://www.xiaohongshu.com/";
console.log(isDetailUrl(url2)); // false

const url3 = "https://www.xiaohongshu.com/search_result/6789abcdef";
console.log(isDetailUrl(url3)); // true
```

### 在导航判断中使用

```typescript
import { isDetailUrl } from "./domain/xhs/detail-url.ts";

async function handlePageNavigation(page: Page) {
	const currentUrl = page.url();

	if (isDetailUrl(currentUrl)) {
		console.log("当前在笔记详情页");
		// 执行详情页相关操作
		await extractNoteContent(page);
	} else {
		console.log("当前在列表页或其他页面");
		// 执行列表页相关操作
		await collectNoteCards(page);
	}
}
```

### 在模态检测中使用

```typescript
import { isDetailUrl } from "./domain/xhs/detail-url.ts";

async function detectOpenMethod(page: Page, initialUrl: string) {
	await page.waitForTimeout(500);

	const currentUrl = page.url();
	const urlChanged = currentUrl !== initialUrl;

	if (urlChanged && isDetailUrl(currentUrl)) {
		return "url"; // URL 打开方式
	} else {
		// 检查是否有模态出现
		const hasModal = (await page.locator(".modal, [role='dialog']").count()) > 0;
		return hasModal ? "modal" : "none";
	}
}
```

### 在卡片点击后判断

```typescript
import { isDetailUrl } from "./domain/xhs/detail-url.js";

async function clickAndDetectOpen(page: Page, cardLocator: Locator) {
	const urlBefore = page.url();
	await cardLocator.click();
	await page.waitForTimeout(500);

	const urlAfter = page.url();

	if (urlBefore !== urlAfter && isDetailUrl(urlAfter)) {
		console.log("通过 URL 打开了笔记详情");
		return { method: "url", url: urlAfter };
	} else {
		console.log("可能通过模态打开了笔记详情");
		return { method: "modal", url: urlBefore };
	}
}
```

## API 参考

### DETAIL_URL_RE

详情页 URL 匹配正则表达式。

**定义**

```typescript
const DETAIL_URL_RE =
	/(\/explore\/|\/search_result\/|\/discovery\/item\/|\/question\/|\/p\/|\/zvideo\/)/i;
```

**支持的路径前缀**

| 路径前缀           | 说明             | 示例                         |
| ------------------ | ---------------- | ---------------------------- |
| `/explore/`        | 发现页笔记详情   | `/explore/6789abcdef`        |
| `/search_result/`  | 搜索结果笔记详情 | `/search_result/6789abcdef`  |
| `/discovery/item/` | 旧版详情页路径   | `/discovery/item/6789abcdef` |
| `/question/`       | 问答详情页       | `/question/6789abcdef`       |
| `/p/`              | 短链接详情页     | `/p/6789abcdef`              |
| `/zvideo/`         | 视频详情页       | `/zvideo/6789abcdef`         |

### isDetailUrl()

判断给定 URL 是否为笔记详情页。

**签名**

```typescript
function isDetailUrl(url: string): boolean;
```

**参数**

- `url` (string): 要检查的 URL 字符串

**返回值**

- `boolean`: 如果是详情页返回 `true`，否则返回 `false`

**特性**

- ✅ 大小写不敏感（使用 `/i` 标志）
- ✅ 异常安全（捕获所有错误并返回 false）
- ✅ 空值安全（`null` 或 `undefined` 返回 false）

## 工作原理

### 匹配逻辑

```typescript
export function isDetailUrl(url: string): boolean {
	try {
		return DETAIL_URL_RE.test(String(url || ""));
	} catch {
		return false;
	}
}
```

1. **空值处理**：`url || ""` 确保非空字符串
2. **类型转换**：`String()` 确保是字符串类型
3. **正则匹配**：`.test()` 检查是否匹配任一路径前缀
4. **异常捕获**：捕获所有异常并返回 false

### 正则表达式分析

```regex
/(\/explore\/|\/search_result\/|\/discovery\/item\/|\/question\/|\/p\/|\/zvideo\/)/i
```

- `( ... )`: 捕获组（实际未使用捕获结果）
- `|`: 或操作符，匹配任意一个分支
- `\/`: 转义的正斜杠
- `/i`: 不区分大小写标志

## 使用场景

### 场景 1：页面类型检测

```typescript
import { isDetailUrl } from "./domain/xhs/detail-url.js";

async function detectPageType(page: Page): Promise<"list" | "detail" | "other"> {
	const url = page.url();

	if (isDetailUrl(url)) {
		return "detail";
	} else if (url.includes("/explore") || url.includes("/search")) {
		return "list";
	} else {
		return "other";
	}
}
```

### 场景 2：导航后验证

```typescript
import { isDetailUrl } from "./domain/xhs/detail-url.js";

async function navigateToNote(page: Page, noteId: string) {
	const targetUrl = `https://www.xiaohongshu.com/explore/${noteId}`;
	await page.goto(targetUrl);

	// 验证是否成功导航到详情页
	const currentUrl = page.url();
	if (!isDetailUrl(currentUrl)) {
		throw new Error("导航失败：未到达详情页");
	}
}
```

### 场景 3：URL 过滤

```typescript
import { isDetailUrl } from "./domain/xhs/detail-url.js";

function filterDetailUrls(urls: string[]): string[] {
	return urls.filter((url) => isDetailUrl(url));
}

// 使用示例
const mixedUrls = [
	"https://www.xiaohongshu.com/explore/abc", // ✓ 详情页
	"https://www.xiaohongshu.com/", // ✗ 首页
	"https://www.xiaohongshu.com/search_result/def", // ✓ 详情页
	"https://www.xiaohongshu.com/user/123", // ✗ 用户页
];

const detailUrls = filterDetailUrls(mixedUrls);
console.log(detailUrls);
// 输出: [
//   "https://www.xiaohongshu.com/explore/abc",
//   "https://www.xiaohongshu.com/search_result/def"
// ]
```

### 场景 4：条件分支

```typescript
import { isDetailUrl } from "./domain/xhs/detail-url.js";

async function handleDifferentPages(page: Page) {
	const url = page.url();

	if (isDetailUrl(url)) {
		// 详情页逻辑
		await extractNoteData(page);
	} else {
		// 列表页逻辑
		await scrollAndCollect(page);
	}
}
```

## 扩展建议

### 1. 添加新路径前缀

如果小红书新增了详情页路径格式：

```typescript
// 修改正则表达式
const DETAIL_URL_RE = /(
	\/explore\/|
	\/search_result\/|
	\/discovery\/item\/|
	\/question\/|
	\/p\/|
	\/zvideo\/|
	\/new_path\/      // 新增路径
)/ix;
```

### 2. 导出更多工具函数

```typescript
// 提取笔记 ID
export function extractNoteId(url: string): string | null {
	if (!isDetailUrl(url)) return null;
	const match = url.match(/\/(explore|search_result|discovery\/item|question|p|zvideo)\/(\w+)/);
	return match ? match[2] : null;
}

// 获取详情页类型
export function getDetailType(url: string): string | null {
	if (!isDetailUrl(url)) return null;
	if (url.includes("/explore/")) return "explore";
	if (url.includes("/search_result/")) return "search";
	if (url.includes("/question/")) return "question";
	if (url.includes("/zvideo/")) return "video";
	return "other";
}
```

### 3. 添加完整 URL 验证

```typescript
export function isValidDetailUrl(url: string): boolean {
	try {
		const urlObj = new URL(url);
		return urlObj.hostname.includes("xiaohongshu.com") && isDetailUrl(url);
	} catch {
		return false;
	}
}
```

## 测试示例

```typescript
import { describe, it, expect } from "vitest";
import { isDetailUrl, DETAIL_URL_RE } from "./detail-url.js";

describe("isDetailUrl", () => {
	it("应识别发现页详情", () => {
		expect(isDetailUrl("https://www.xiaohongshu.com/explore/abc123")).toBe(true);
	});

	it("应识别搜索结果详情", () => {
		expect(isDetailUrl("https://www.xiaohongshu.com/search_result/def456")).toBe(true);
	});

	it("应识别问答详情", () => {
		expect(isDetailUrl("https://www.xiaohongshu.com/question/ghi789")).toBe(true);
	});

	it("应拒绝首页", () => {
		expect(isDetailUrl("https://www.xiaohongshu.com/")).toBe(false);
	});

	it("应拒绝用户页", () => {
		expect(isDetailUrl("https://www.xiaohongshu.com/user/123")).toBe(false);
	});

	it("应处理大小写", () => {
		expect(isDetailUrl("https://www.xiaohongshu.com/EXPLORE/ABC")).toBe(true);
	});

	it("应处理空值", () => {
		expect(isDetailUrl("")).toBe(false);
		expect(isDetailUrl(null as any)).toBe(false);
		expect(isDetailUrl(undefined as any)).toBe(false);
	});

	it("应处理异常输入", () => {
		expect(isDetailUrl({} as any)).toBe(false);
		expect(isDetailUrl(123 as any)).toBe(false);
	});
});
```

## 性能考虑

- **正则匹配**：`.test()` 方法非常高效，时间复杂度 O(n)
- **无副作用**：纯函数，可以安全缓存结果
- **内存占用**：正则表达式对象仅创建一次，共享使用

```typescript
// 如需频繁调用，可以添加缓存
const urlCache = new Map<string, boolean>();

function isDetailUrlCached(url: string): boolean {
	if (urlCache.has(url)) {
		return urlCache.get(url)!;
	}
	const result = isDetailUrl(url);
	urlCache.set(url, result);
	return result;
}
```

## 注意事项

1. **URL 格式变化**：如果小红书更新 URL 结构，需要更新正则表达式
2. **国际化路径**：目前仅支持中国区路径，国际版可能不同
3. **重定向处理**：重定向后的 URL 可能与原始 URL 不同，需注意判断时机

## 相关模块

- `click.ts`：可点击元素解析，使用相同的路径前缀
- `navigation.ts`：笔记导航和选择，依赖 URL 判断
- `noteExtractor.ts`：笔记内容提取，需要先判断是否在详情页

## 更新日志

- **2025-01** - 初始版本，支持 6 种详情页路径格式
