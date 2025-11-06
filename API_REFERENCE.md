# API Reference - HushOps.Servers.XiaoHongShu

本文档提供 MCP 服务的完整 API 参考，包括所有工具的详细参数说明、返回值格式和使用示例。

---

## 目录

- [浏览器与页面管理](#浏览器与页面管理)
- [页面交互](#页面交互)
- [页面信息获取](#页面信息获取)
- [小红书专用工具](#小红书专用工具)
- [资源管理](#资源管理)
- [高权限管理工具](#高权限管理工具)
- [诊断与监控](#诊断与监控)
- [MCP 资源](#mcp-资源)
- [拟人化参数详解](#拟人化参数详解)
- [目标定位参数](#目标定位参数)
- [通用返回格式](#通用返回格式)

---

## 浏览器与页面管理

### browser.open

打开或复用浏览器窗口（dirId 映射到持久化 BrowserContext）。

**参数**：

```typescript
{
  dirId: string;          // 必填，账号窗口标识
  workspaceId?: string;   // 可选，工作区 ID（未提供时使用 ROXY_DEFAULT_WORKSPACE_ID）
}
```

**返回值**：

```typescript
{
  ok: true,
  value: {
    dirId: string;
    pages: number;        // 当前页面数量
  }
}
```

**示例**：

```json
{
	"dirId": "user123",
	"workspaceId": "28255"
}
```

---

### browser.close

关闭浏览器窗口。

**参数**：

```typescript
{
	dirId: string; // 必填，要关闭的窗口标识
}
```

**返回值**：

```typescript
{
  ok: true,
  value: {
    dirId: string;
  }
}
```

---

### page.create

在窗口中创建新页面。

**参数**：

```typescript
{
  dirId: string;          // 必填
  url?: string;           // 可选，创建后立即导航到此 URL
  workspaceId?: string;   // 可选
}
```

**返回值**：

```typescript
{
  ok: true,
  value: {
    index: number;        // 页面索引
    url: string;          // 当前 URL
  }
}
```

---

### page.list

列出窗口中的所有页面。

**参数**：

```typescript
{
  dirId: string;          // 必填
  workspaceId?: string;   // 可选
}
```

**返回值**：

```typescript
{
  ok: true,
  value: {
    pages: Array<{
      index: number;
      url: string;
    }>;
  }
}
```

---

### page.close

关闭指定页面。

**参数**：

```typescript
{
  dirId: string;          // 必填
  pageIndex?: number;     // 可选，默认关闭最后一个页面
  workspaceId?: string;   // 可选
}
```

**返回值**：

```typescript
{
  ok: true,
  value: {
    closed: boolean;
    closedIndex?: number;
  }
}
```

---

### page.navigate

导航到指定 URL。

**参数**：

```typescript
{
  dirId: string;          // 必填
  url: string;            // 必填，完整 URL
  pageIndex?: number;     // 可选，默认使用当前页面
  workspaceId?: string;   // 可选
}
```

**返回值**：

```typescript
{
  ok: true,
  value: {
    url: string;          // 最终 URL（可能重定向）
  }
}
```

---

## 页面交互

### page.click

点击页面元素（支持拟人化鼠标移动）。

**参数**：

```typescript
{
  dirId: string;                  // 必填
  target: TargetHints;            // 必填，元素定位参数
  pageIndex?: number;             // 可选
  workspaceId?: string;           // 可选
  human?: boolean | HumanOptions; // 可选，拟人化参数（默认 true）
}
```

**target 定位参数**：

```typescript
{
  // 语义定位（优先）
  role?: string;          // ARIA 角色：button/link/textbox/...
  name?: string;          // 可访问名称
  label?: string;         // 标签文本
  testId?: string;        // test-id 属性
  text?: string;          // 元素文本内容

  // CSS 兜底
  selector?: string;      // CSS 选择器

  // 其他
  id?: string;            // 元素 ID
}
```

**HumanOptions**：

```typescript
{
  enabled?: boolean;      // 是否启用拟人化（默认 true）
  profile?: string;       // 档位：default/cautious/rapid

  // 鼠标移动参数
  steps?: number;         // 曲线步数（默认 24）
  randomness?: number;    // 抖动幅度 0-1（默认 0.2）
  overshoot?: boolean;    // 是否过冲（默认 true）
  overshootAmount?: number; // 过冲像素（默认 12）
  microJitterPx?: number; // 终点微抖（默认 1.2）
  microJitterCount?: number; // 微抖次数（默认 2）
}
```

**返回值**：

```typescript
{
  ok: true,
  value: {
    selectorId: string;   // 使用的选择器标识
    clickedAt?: {         // 点击坐标
      x: number;
      y: number;
    };
  }
}
```

**示例**：

```json
{
	"dirId": "user123",
	"target": {
		"role": "button",
		"name": "登录"
	},
	"human": {
		"profile": "default",
		"steps": 24,
		"randomness": 0.2
	}
}
```

---

### page.hover

悬停元素（支持拟人化）。

**参数**：同 `page.click`

**返回值**：

```typescript
{
  ok: true,
  value: {
    selectorId: string;
    hoveredAt?: {
      x: number;
      y: number;
    };
  }
}
```

---

### page.scroll

滚动页面（支持拟人化分段滚动）。

**参数**：

```typescript
{
  dirId: string;                  // 必填
  pageIndex?: number;             // 可选
  workspaceId?: string;           // 可选

  // 滚动目标（三选一）
  deltaY?: number;                // 相对滚动（像素）
  absoluteY?: number;             // 绝对位置（像素）
  target?: TargetHints;           // 滚动到元素

  human?: boolean | {             // 拟人化参数
    enabled?: boolean;
    profile?: string;

    // 滚动参数
    segments?: number;            // 分段数（默认 6）
    jitterPx?: number;            // 每段抖动（默认 20）
    perSegmentMs?: number;        // 每段延时（默认 110）
    microPauseChance?: number;    // 微停顿概率（默认 0.25）
    microPauseMinMs?: number;     // 微停顿最小时长（默认 60）
    microPauseMaxMs?: number;     // 微停顿最大时长（默认 160）
    macroPauseEvery?: number;     // 宏停顿间隔段数（默认 4）
    macroPauseMinMs?: number;     // 宏停顿最小时长（默认 120）
    macroPauseMaxMs?: number;     // 宏停顿最大时长（默认 260）
  };
}
```

**返回值**：

```typescript
{
  ok: true,
  value: {
    scrolledY: number;            // 最终 Y 位置
    scrollHeight?: number;        // 页面总高度
  }
}
```

**示例**：

```json
{
	"dirId": "user123",
	"deltaY": 500,
	"human": {
		"segments": 6,
		"perSegmentMs": 120,
		"microPauseChance": 0.25
	}
}
```

---

### page.type

输入文本（支持逐字延时）。

**参数**：

```typescript
{
  dirId: string;                  // 必填
  target: TargetHints;            // 必填，输入框定位
  text: string;                   // 必填，输入文本
  pageIndex?: number;             // 可选
  workspaceId?: string;           // 可选
  human?: boolean | {
    enabled?: boolean;
    wpm?: number;                 // 每分钟字数（默认 110）
  };
}
```

**返回值**：

```typescript
{
  ok: true,
  value: {
    selectorId: string;
    typed: string;                // 输入的文本
  }
}
```

**示例**：

```json
{
	"dirId": "user123",
	"target": {
		"role": "textbox",
		"name": "标题"
	},
	"text": "今天天气真好",
	"human": {
		"wpm": 180
	}
}
```

---

### page.input.clear

清空输入框内容。

**参数**：

```typescript
{
  dirId: string;
  target: TargetHints;
  pageIndex?: number;
  workspaceId?: string;
}
```

**返回值**：

```typescript
{
  ok: true,
  value: {
    selectorId: string;
    cleared: boolean;
  }
}
```

---

## 页面信息获取

### page.screenshot

截取页面截图。

**参数**：

```typescript
{
  dirId: string;          // 必填
  pageIndex?: number;     // 可选
  workspaceId?: string;   // 可选
  fullPage?: boolean;     // 可选，全页截图（默认 false）
  target?: TargetHints;   // 可选，截取元素
}
```

**返回值**：

- 文本内容项：JSON 格式的元数据
- 图片内容项：image/png 格式的截图数据

```typescript
// 文本内容项
{
  ok: true,
  value: {
    path: string;         // 保存路径
    timestamp: number;    // 时间戳
    url: string;          // 页面 URL
  }
}

// 图片内容项
{
  type: "image",
  data: string,           // base64 编码
  mimeType: "image/png"
}
```

---

### page.snapshot

获取页面可访问性快照（a11y 树）。

**参数**：

```typescript
{
  dirId: string;          // 必填
  pageIndex?: number;     // 可选
  workspaceId?: string;   // 可选
  maxNodes?: number;      // 可选，最大节点数（默认 800）
}
```

**返回值**：

```typescript
{
  ok: true,
  value: {
    url: string;
    title: string;
    tree?: {              // a11y 树（截断到 maxNodes）
      role: string;
      name?: string;
      children: Array<any>;
    };
    stats?: {
      nodeCount: number;
      roleCounts: Record<string, number>;
      landmarks: string[];
    };
    clickableCount?: number;
  }
}
```

**示例**：

```json
{
	"dirId": "user123",
	"maxNodes": 500
}
```

---

## 小红书专用工具

### xhs.session.check

检查小红书会话状态。

**参数**：

```typescript
{
  dirId: string;
  workspaceId?: string;
}
```

**返回值**：

```typescript
{
  ok: true,
  value: {
    valid: boolean;       // 会话是否有效
    url: string;          // 当前 URL
    hasCookies: boolean;  // 是否有 cookies
  }
}
```

---

### xhs.navigate.home

导航到小红书首页并验证。

**参数**：

```typescript
{
  dirId: string;
  workspaceId?: string;
}
```

**返回值**：

```typescript
{
  ok: true,
  value: {
    url: string;
    verified: boolean;    // 是否成功到达 explore 页面
  }
}
```

---

## 资源管理

### resources.listArtifacts

列出指定 dirId 下的所有 artifacts 文件。

**参数**：

```typescript
{
	dirId: string;
}
```

**返回值**：

```typescript
{
  ok: true,
  value: {
    root: string;         // 根目录路径
    files: string[];      // 相对路径数组（已排序）
  }
}
```

---

### resources.readArtifact

读取 artifacts 文件内容。

**参数**：

```typescript
{
	dirId: string;
	path: string; // 相对于 artifacts/<dirId> 的路径
}
```

**返回值**：

- 文本文件：返回 text 内容项
- 图片文件（png/jpg）：返回 image 内容项

```typescript
// 文本文件
{
  type: "text",
  text: string
}

// 图片文件
{
  type: "image",
  data: string,           // base64 编码
  mimeType: "image/png" | "image/jpeg"
}
```

---

## 高权限管理工具

### roxy.workspaces.list

获取工作区列表。

**参数**：

```typescript
{
  page_index?: number;    // 页码（从 0 开始，默认 0）
  page_size?: number;     // 每页数量（默认 15，最大 200）
}
```

**返回值**：直接透传 Roxy API 响应。

---

### roxy.windows.list

获取浏览器窗口列表。

**参数**：

```typescript
{
  workspaceId: number | string;  // 必填
  dirIds?: string;               // 可选，逗号分隔
  windowName?: string;           // 可选，窗口名称筛选
  page_index?: number;           // 页码（从 1 开始，默认 1）
  page_size?: number;            // 每页数量（默认 15，最大 200）
  // ... 更多 Roxy API 支持的参数
}
```

**返回值**：直接透传 Roxy API 响应。

---

### roxy.window.create

创建新浏览器窗口。

**参数**：

```typescript
{
  workspaceId: number | string;  // 必填
  windowName?: string;           // 窗口名称
  os?: string;                   // 操作系统
  osVersion?: string;            // 系统版本
  coreVersion?: string;          // 浏览器版本
  projectId?: number | string;   // 项目 ID
  windowRemark?: string;         // 备注
  defaultOpenUrl?: string[];     // 默认打开的 URL 列表
  proxyInfo?: Record<string, any>; // 代理配置
  // ... 更多 Roxy API 支持的参数
}
```

**返回值**：直接透传 Roxy API 响应。

---

## 诊断与监控

### server.capabilities

查看服务器能力信息。

**参数**：无

**返回值**：

```typescript
{
  adapter: "official";
  roxyBridge: {
    loaded: boolean;
    package?: string;     // 包名
    version?: string;     // 版本号
  };
  adminTools: boolean;    // roxy.* 管理工具是否可用
}
```

---

### server.ping

连通性检查与心跳。

**参数**：无

**返回值**：

```typescript
{
	ok: true;
	ts: number; // 时间戳
}
```

---

## MCP 资源

### xhs://artifacts/{dirId}/index

列出指定 dirId 的所有 artifacts 文件。

**参数**：

- `dirId`: 路径参数，账号标识

**返回值**：

```typescript
{
  root: string;
  files: string[];        // 相对路径数组
}
```

---

### xhs://snapshot/{dirId}/{page}

获取指定页面的 a11y 快照。

**参数**：

- `dirId`: 路径参数，账号标识
- `page`: 路径参数，页面索引（数字）

**返回值**：同 `page.snapshot` 工具。

---

## 拟人化参数详解

### 档位（Profile）

使用 `profile` 参数可一键选择预设的行为节律：

- `default`: 平衡型（推荐）
  - mouseSteps: 24
  - mouseRandomness: 0.20
  - wpm: 110
  - scrollSegments: 6
  - scrollPerSegmentMs: 110

- `cautious`: 稳健型（更慢、更稳）
  - mouseSteps: 32
  - mouseRandomness: 0.23
  - wpm: 85
  - scrollSegments: 8
  - scrollPerSegmentMs: 150

- `rapid`: 高效型（更快、更紧凑）
  - mouseSteps: 18
  - mouseRandomness: 0.16
  - wpm: 140
  - scrollSegments: 4
  - scrollPerSegmentMs: 90

### 拟人化参数优先级

1. 显式传入的细化参数（如 `steps: 30`）
2. 档位预设值（如 `profile: "cautious"`）
3. 全局环境变量 `HUMAN_PROFILE`
4. 默认值（default 档位）

### 完全关闭拟人化

```json
{
  "human": false
}

// 或

{
  "human": {
    "enabled": false
  }
}
```

---

## 目标定位参数

### TargetHints

目标定位参数支持多种方式，按优先级排序：

1. **语义定位（推荐）**：
   - `role`: ARIA 角色（button/link/textbox/checkbox/...）
   - `name`: 可访问名称
   - `label`: 标签文本
   - `testId`: data-testid 属性

2. **文本匹配**：
   - `text`: 元素文本内容（支持部分匹配）

3. **CSS 兜底**：
   - `selector`: CSS 选择器
   - `id`: 元素 ID

### 选择器韧性

系统自动实现选择器韧性策略：

1. 优先尝试语义定位
2. 失败时自动回退到 CSS 选择器
3. 所有失败和耗时会记录到 `artifacts/selector-health.ndjson`
4. 可通过 `npm run report:selectors` 查看健康报告

---

## 通用返回格式

### 成功响应

```typescript
{
  ok: true,
  value: any              // 具体数据
}
```

### 错误响应

```typescript
{
  error: {
    code: string;         // 错误码
    message: string;      // 错误信息
    screenshotPath?: string; // 失败截图路径（如有）
  }
}
```

### 常见错误码

- `CONNECTION_FAILED`: 连接 Roxy 失败
- `NAVIGATE_FAILED`: 导航失败
- `SELECTOR_NOT_FOUND`: 元素定位失败
- `ACTION_FAILED`: 动作执行失败
- `INTERNAL_ERROR`: 内部错误
- `TIMEOUT`: 操作超时

---

## 最佳实践

### 1. 优先使用语义定位

```json
// ✅ 推荐
{
  "target": {
    "role": "button",
    "name": "提交"
  }
}

// ❌ 不推荐（除非必要）
{
  "target": {
    "selector": "#submit-btn"
  }
}
```

### 2. 合理使用拟人化

```json
// 默认已开启，无需传参
{
  "dirId": "user123",
  "target": { "role": "button", "name": "登录" }
}

// 需要更快的操作时
{
  "dirId": "user123",
  "target": { "role": "button", "name": "登录" },
  "human": { "profile": "rapid" }
}

// 调试时快速点击
{
  "dirId": "user123",
  "target": { "role": "button", "name": "登录" },
  "human": false
}
```

### 3. 等待与停留由外部编排

```typescript
// MCP 客户端侧
await tools.call("page.click", {...});
await sleep(random(3000, 6000));  // 外部等待
await tools.call("page.screenshot", {...});
```

### 4. 使用快照验证页面状态

```typescript
// 获取页面快照作为"到达证据"
const snapshot = await tools.call("page.snapshot", { dirId: "user123" });
console.log(snapshot.url, snapshot.clickableCount);
```

---

## 环境变量参考

完整的环境变量列表请查看 [.env.example](.env.example)。

关键配置：

- `ROXY_API_TOKEN`: 必填，Roxy API 令牌
- `HUMAN_PROFILE`: default/cautious/rapid，影响所有拟人化动作
- `SNAPSHOT_MAX_NODES`: 页面快照节点上限，默认 800
- `SELECTOR_RETRY_ATTEMPTS`: 选择器重试次数，默认 3
- `SELECTOR_BREAKER_QPS`: 断路器 QPS 限制，默认 10

---

## 更新记录

- 2025-01-06: 创建 API 参考文档
- 补充所有工具的完整参数说明
- 添加拟人化参数详解
- 添加目标定位参数说明
- 添加最佳实践指南
