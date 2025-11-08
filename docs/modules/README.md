# 新模块文档索引

本目录包含 HushOps.Servers.XiaoHongShu 项目中新增模块的详细使用文档。

## 📚 模块列表

### 1. [笔记内容提取器 (noteExtractor)](./note-extractor.md)

**位置**: `src/domain/xhs/noteExtractor.ts`

**功能**: 通过 API 拦截提取小红书笔记的完整内容。

**核心能力**:
- ✅ API 拦截提取（无需 DOM 解析）
- ✅ 完整数据覆盖（标题、正文、标签、互动数据）
- ✅ 错误处理完善

**适用场景**:
- 数据分析和质量评分
- 内容批量提取
- MCP 工具集成

[📖 查看完整文档 →](./note-extractor.md)

---

### 2. [可点击元素解析器 (click)](./clickable-resolver.md)

**位置**: `src/domain/xhs/click.ts`

**功能**: 在卡片容器内智能解析"最可点击"的元素。

**核心能力**:
- ✅ 智能优先级策略（noteId 精确匹配 → href 前缀 → 兜底）
- ✅ 多路径支持（搜索结果、发现页、详情页等）
- ✅ 可见性过滤

**适用场景**:
- 笔记卡片点击
- 批量打开笔记
- 自动化导航

[📖 查看完整文档 →](./clickable-resolver.md)

---

### 3. [详情 URL 识别器 (detail-url)](./detail-url.md)

**位置**: `src/domain/xhs/detail-url.ts`

**功能**: 统一识别小红书笔记详情页 URL。

**核心能力**:
- ✅ 多路径支持（6 种详情页格式）
- ✅ 正则匹配高效识别
- ✅ 错误容错

**适用场景**:
- 页面类型检测
- 导航后验证
- URL 过滤

[📖 查看完整文档 →](./detail-url.md)

---

## 🎯 快速导航

### 按使用场景

| 场景 | 推荐模块 | 说明 |
|------|---------|------|
| 提取笔记内容 | [noteExtractor](./note-extractor.md) | 获取标题、正文、标签、互动数据 |
| 点击打开笔记 | [click](./clickable-resolver.md) | 智能定位最可点击的元素 |
| 判断页面类型 | [detail-url](./detail-url.md) | 识别是否为详情页 |

### 按开发流程

1. **数据收集阶段** → [noteExtractor](./note-extractor.md)
2. **自动化交互阶段** → [click](./clickable-resolver.md)
3. **状态判断阶段** → [detail-url](./detail-url.md)

---

## 🔗 模块关系

```
detail-url (URL识别)
    ↓
click (元素定位)
    ↓
noteExtractor (内容提取)
```

**工作流示例**:
1. 使用 `click` 定位并点击卡片
2. 使用 `detail-url` 判断是否成功打开详情页
3. 使用 `noteExtractor` 提取笔记完整内容

---

## 📖 文档规范

所有模块文档遵循统一结构：

1. **概述** - 模块用途和核心特性
2. **设计原则** - 遵循的设计理念
3. **使用方法** - 基础用法和示例
4. **API 参考** - 完整的函数签名和参数说明
5. **工作原理** - 内部实现逻辑
6. **使用场景** - 常见场景和代码示例
7. **配置** - 可选的配置项
8. **注意事项** - 重要提醒和限制
9. **相关模块** - 关联模块链接
10. **更新日志** - 版本变更记录

---

## 🤝 贡献指南

### 添加新模块文档

1. 在 `docs/modules/` 目录创建新文档
2. 遵循统一文档结构
3. 添加到本索引文件
4. 包含完整的示例代码
5. 添加相关模块链接

### 更新现有文档

1. 保持文档与代码同步
2. 更新变更到"更新日志"章节
3. 补充新的使用场景
4. 优化示例代码

---

## 📞 获取帮助

- 查看完整项目说明：`../../README.md`
- 相关用例参考：`tests/integration/selectors/` 目录

---

## ⚡ 快速开始

```typescript
// 1. 导入模块
import { extractNoteContent } from "./domain/xhs/noteExtractor.js";
import { resolveClickableInCard } from "./domain/xhs/click.js";
import { isDetailUrl } from "./domain/xhs/detail-url.js";

// 2. 使用流程示例
async function completeWorkflow(page: Page, card: Locator) {
	// 点击卡片
	const clickable = await resolveClickableInCard(page, card);
	await clickable.click();

	// 判断是否打开详情页
	await page.waitForTimeout(500);
	if (isDetailUrl(page.url())) {
		// 提取笔记内容
		const content = await extractNoteContent(ctx, page.url());
		console.log("提取成功:", content);
	}
}
```

---

最后更新: 2025-11-09
