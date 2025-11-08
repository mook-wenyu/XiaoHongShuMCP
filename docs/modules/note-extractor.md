# 笔记内容提取器 (noteExtractor)

## 概述

`noteExtractor` 模块负责从小红书笔记页面提取完整的笔记内容，包括标题、正文、标签、作者信息和互动数据。

## 特性

- ✅ **API 拦截提取**：通过拦截小红书 API 响应获取数据，无需 DOM 解析
- ✅ **完整数据覆盖**：提取标题、正文、标签、作者、点赞/收藏/评论/分享数
- ✅ **错误处理完善**：详细的错误分类和错误信息
- ✅ **类型安全**：完整的 TypeScript 类型定义

## 设计原则

本模块遵循以下设计原则：

- **KISS**：简单直接的流程，避免过度复杂
- **SOLID**：单一职责，仅负责内容提取
- **高内聚**：所有提取逻辑集中在一个函数
- **低耦合**：通过接口与外部交互，不依赖具体实现

## 使用方法

### 基础用法

```typescript
import { extractNoteContent } from "./domain/xhs/noteExtractor.js";
import type { BrowserContext } from "playwright";

// 假设你已经有了 BrowserContext
const ctx: BrowserContext = /* ... */;
const noteUrl = "https://www.xiaohongshu.com/explore/12345678";

const result = await extractNoteContent(ctx, noteUrl);

if ("ok" in result && !result.ok) {
	// 提取失败
	console.error(`提取失败: ${result.code} - ${result.message}`);
} else {
	// 提取成功
	console.log("笔记标题:", result.title);
	console.log("笔记内容:", result.content);
	console.log("标签:", result.tags);
	console.log("作者:", result.author_nickname);
	console.log("点赞数:", result.interact_stats.likes);
}
```

### 在 MCP 工具中使用

```typescript
import { extractNoteContent } from "./domain/xhs/noteExtractor.js";

server.registerTool(
	"xhs_note_extract_content",
	{
		description: "提取小红书笔记的完整内容",
		inputSchema: {
			dirId: z.string(),
			noteUrl: z.string().url(),
		},
	},
	async (input: any) => {
		const { dirId, noteUrl } = input;
		const ctx = await manager.getContext(dirId);

		const result = await extractNoteContent(ctx, noteUrl);

		return {
			content: [{ type: "text", text: JSON.stringify(result) }],
		};
	}
);
```

## API 参考

### extractNoteContent()

提取单个笔记的完整内容。

**签名**

```typescript
async function extractNoteContent(
	ctx: BrowserContext,
	noteUrl: string
): Promise<NoteContentResult | ExtractError>
```

**参数**

- `ctx` (BrowserContext): Playwright 浏览器上下文，用于创建新页面
- `noteUrl` (string): 笔记完整 URL，例如 `https://www.xiaohongshu.com/explore/12345678`

**返回值**

成功时返回 `NoteContentResult`：

```typescript
{
	note_id: string;           // 笔记 ID
	url: string;               // 笔记 URL
	title: string;             // 笔记标题
	content: string;           // 笔记正文
	tags: string[];            // 标签列表
	author_nickname: string;   // 作者昵称
	interact_stats: {
		likes: number;         // 点赞数
		collects: number;      // 收藏数
		comments: number;      // 评论数
		shares: number;        // 分享数
	};
	extracted_at: string;      // 提取时间 (ISO 8601)
}
```

失败时返回 `ExtractError`：

```typescript
{
	ok: false;
	code: string;              // 错误代码
	message: string;           // 错误描述
}
```

**错误代码**

| 错误代码 | 说明 | 解决方案 |
|---------|------|---------|
| `API_FAILED` | 笔记详情 API 调用失败或无数据 | 检查网络连接，确认笔记 URL 有效 |
| `INVALID_DATA` | 笔记数据不完整（缺少必要字段） | 确认笔记是否正常可访问 |
| `NAVIGATION_FAILED` | 页面导航失败 | 检查 URL 格式，确认网络状态 |
| `TIMEOUT` | API 等待超时 | 增加超时时间或重试 |

## 工作原理

1. **创建新页面**：在提供的 BrowserContext 中创建新页面
2. **启动 API 监听**：在导航前启动 API 响应监听器
3. **导航到笔记**：访问笔记 URL，触发 API 请求
4. **等待 API 响应**：等待小红书笔记详情 API 响应
5. **验证数据**：检查必要字段是否存在
6. **构建结果**：标准化数据格式并返回
7. **清理资源**：自动关闭创建的页面

## 配置

模块使用 `XHS_CONF.feed.waitApiMs` 配置 API 等待超时时间（默认 10000ms）：

```typescript
// 在 src/config/xhs.ts 中配置
export const XHS_CONF = {
	feed: {
		waitApiMs: 10000, // API 等待超时时间（毫秒）
	},
};
```

## 依赖

- `playwright`：浏览器自动化
- `./netwatch.js`：API 监听器（`waitNoteDetail` 函数）
- `../../config/xhs.js`：配置管理

## 注意事项

1. **自动资源清理**：函数会自动关闭创建的页面，无需手动管理
2. **并发限制**：建议控制并发提取数量，避免触发反爬机制
3. **错误重试**：对于临时性错误（如网络超时），建议实现重试逻辑
4. **数据时效性**：提取的互动数据（点赞/收藏数）会随时间变化

## 示例：批量提取

```typescript
import { extractNoteContent } from "./domain/xhs/noteExtractor.js";

async function extractMultipleNotes(
	ctx: BrowserContext,
	noteUrls: string[]
): Promise<Array<NoteContentResult | ExtractError>> {
	const results = [];

	// 控制并发数量
	const concurrency = 3;
	for (let i = 0; i < noteUrls.length; i += concurrency) {
		const batch = noteUrls.slice(i, i + concurrency);
		const batchResults = await Promise.all(
			batch.map(url => extractNoteContent(ctx, url))
		);
		results.push(...batchResults);

		// 间隔延时，避免触发反爬
		await new Promise(resolve => setTimeout(resolve, 1000));
	}

	return results;
}
```

## 相关模块

- `netwatch.ts`：网络监听和 API 拦截
- `xhs.ts`：小红书 MCP 工具集成
- `noteActions.ts`：笔记互动操作（点赞、收藏等）

## 贡献指南

如需扩展或修改此模块：

1. 保持单一职责原则
2. 所有新增字段必须添加类型定义
3. 完善错误处理和错误码
4. 补充单元测试和集成测试
5. 更新本文档

## 更新日志

- **2025-01** - 初始版本，支持基础笔记内容提取
