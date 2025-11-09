# 可点击元素解析器 (click)

## 概述

`click` 模块提供卡片内"最可点击"元素的智能解析功能，用于小红书笔记卡片的精确点击定位。

## 特性

- ✅ **智能优先级**：noteId 精确匹配 → href 前缀优先 → 标题/封面兜底
- ✅ **多路径支持**：支持搜索结果、发现页、详情页等多种页面类型
- ✅ **可见性过滤**：仅定位可见元素，避免点击隐藏内容
- ✅ **类型安全**：完整的 TypeScript 类型定义

## 设计原则

- **最小惊讶原则**：优先选择用户最可能想点击的元素
- **健壮性**：多级降级策略，确保总能找到可点击元素
- **性能优先**：从最精确到最宽松，减少不必要的查询

## 使用方法

### 基础用法

```typescript
import { resolveClickableInCard } from "./domain/xhs/click.ts";
import type { Page, Locator } from "playwright";

// 假设你已经有了 page 和 card locator
const page: Page = /* ... */;
const card: Locator = page.locator(".note-item").first();

// 无 noteId：按 href 前缀优先级查找
const clickable1 = await resolveClickableInCard(page, card);
await clickable1.click();

// 有 noteId：精确匹配优先
const clickable2 = await resolveClickableInCard(page, card, {
	id: "6789abcdef"
});
await clickable2.click();
```

### 在笔记选择流程中使用

```typescript
import { resolveClickableInCard } from "./domain/xhs/click.ts";

async function selectNoteByKeyword(page: Page, keyword: string) {
	// 1. 收集所有卡片
	const cards = await page.locator(".note-item").all();

	// 2. 找到匹配关键词的卡片
	for (const card of cards) {
		const title = await card.locator(".title").textContent();
		if (title?.includes(keyword)) {
			// 3. 解析最可点击的元素
			const clickable = await resolveClickableInCard(page, card);

			// 4. 点击打开笔记
			await clickable.click();
			return;
		}
	}
}
```

### 在 MCP 工具中使用

```typescript
import { resolveClickableInCard } from "./domain/xhs/click.js";

// 在 xhs_select_note 工具中使用
async function openNoteCard(page: Page, card: Locator, noteId?: string) {
	const clickable = await resolveClickableInCard(page, card, { id: noteId });
	await clickable.click();

	// 等待导航或模态打开
	await page.waitForTimeout(1000);
}
```

## API 参考

### resolveClickableInCard()

在卡片容器内解析"最可点击"的元素。

**签名**

```typescript
async function resolveClickableInCard(
	page: Page,
	card: Locator,
	opts?: { id?: string },
): Promise<Locator>;
```

**参数**

- `page` (Page): Playwright Page 对象
- `card` (Locator): 卡片容器的 Locator
- `opts.id` (string, 可选): 笔记 ID，用于精确匹配

**返回值**

返回一个 `Locator` 对象，指向卡片内最可点击的元素。

## 工作原理

### 查找策略（三级降级）

```
1. noteId 精确匹配（如果提供了 id）
   ├─ 优先级 1: /search_result/{id}
   ├─ 优先级 2: /explore/{id}
   └─ 优先级 3: /discovery/item/{id}

2. href 前缀优先（按优先级降序）
   ├─ /search_result/
   ├─ /explore/
   └─ /discovery/item/

3. 兜底：标题或封面可见元素
   ├─ a.title:visible
   ├─ .footer a.title:visible
   ├─ a.cover:visible
   └─ a[class*="cover"]:visible
```

### 允许的 URL 前缀

| 前缀               | 说明           | 优先级 |
| ------------------ | -------------- | ------ |
| `/search_result/`  | 搜索结果页详情 | 最高   |
| `/explore/`        | 发现页详情     | 高     |
| `/discovery/item/` | 旧版详情页     | 中     |

### 可见性过滤

所有选择器都添加 `:visible` 伪类，确保只定位可见元素。

## 选择器说明

### 1. noteId 精确选择器

```typescript
`a[href*="${prefix}${id}"]:visible, a[class*="cover" i][href*="${prefix}${id}"]:visible`;
```

- 匹配包含指定 noteId 的链接
- 优先匹配封面链接（class 包含 "cover"）

### 2. 通用 href 选择器

```typescript
`a[href*="${prefix}"]:visible, a[class*="cover" i][href*="${prefix}"]:visible`;
```

- 匹配包含允许前缀的任意链接
- 优先匹配封面链接

### 3. 兜底选择器

```typescript
`a.title:visible, .footer a.title:visible, a.cover:visible, a[class*="cover" i]:visible`;
```

- 匹配标题或封面元素
- 适用于非标准结构的卡片

## 使用场景

### 场景 1：精确点击指定笔记

```typescript
// 当你知道 noteId 时，使用精确匹配
const clickable = await resolveClickableInCard(page, card, {
	id: "6789abcdef",
});
await clickable.click();
```

### 场景 2：点击第一个可点击元素

```typescript
// 当不确定 noteId 时，使用默认策略
const clickable = await resolveClickableInCard(page, card);
await clickable.click();
```

### 场景 3：批量处理卡片

```typescript
const cards = await page.locator(".note-item").all();
for (const card of cards) {
	try {
		const clickable = await resolveClickableInCard(page, card);
		const href = await clickable.getAttribute("href");
		console.log("可点击链接:", href);
	} catch (error) {
		console.error("解析失败:", error);
	}
}
```

## 配置

模块没有外部配置项，所有策略硬编码在函数内部。如需自定义：

1. 修改 `allow` 数组以调整 URL 前缀优先级
2. 修改选择器字符串以适配不同的 DOM 结构

```typescript
// 当前配置
const allow = [
	"/search_result/", // 优先级 1
	"/explore/", // 优先级 2
	"/discovery/item/", // 优先级 3
];
```

## 注意事项

1. **可见性依赖**：只定位 `:visible` 元素，确保浏览器正确渲染
2. **DOM 结构变化**：如果小红书更新 DOM 结构，可能需要更新选择器
3. **性能考虑**：多次调用 `.count()` 可能影响性能，建议批量处理时添加缓存
4. **错误处理**：如果所有策略都失败，返回的 Locator 可能指向不存在的元素

## 故障排查

### 问题：找不到可点击元素

**可能原因**：

- 卡片 DOM 结构变化
- 元素未完全加载
- CSS 类名或 href 格式变化

**解决方案**：

1. 检查页面 HTML 结构
2. 更新选择器字符串
3. 添加等待逻辑确保元素可见

### 问题：点击了错误的元素

**可能原因**：

- 优先级策略不符合预期
- 多个元素匹配同一选择器

**解决方案**：

1. 提供 `id` 参数使用精确匹配
2. 调整 `allow` 数组优先级
3. 检查返回的 Locator 是否正确

## 相关模块

- `detail-url.ts`：笔记详情 URL 识别
- `navigation.ts`：笔记选择和导航
- `search.ts`：搜索与页面元素准备

## 扩展建议

如需支持更多场景：

1. **添加新 URL 前缀**：

   ```typescript
   const allow = [
   	"/search_result/",
   	"/explore/",
   	"/discovery/item/",
   	"/question/", // 新增：问答页
   	"/zvideo/", // 新增：视频页
   ];
   ```

2. **自定义选择器策略**：

   ```typescript
   function resolveClickableInCard(
   	page: Page,
   	card: Locator,
   	opts?: {
   		id?: string;
   		strategy?: "cover-first" | "title-first";
   	},
   ): Promise<Locator>;
   ```

3. **添加日志追踪**：
   ```typescript
   console.log(`解析策略: ${strategyUsed}`);
   console.log(`匹配元素: ${elementSelector}`);
   ```

## 更新日志

- **2025-01** - 初始版本，支持三级降级策略
