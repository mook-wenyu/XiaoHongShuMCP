# HushOps.Servers.XiaoHongShu · Node.js + TypeScript

## 2025-10-21 变更摘要（Breaking）
- 删除旧日志 shim：`src/logger.ts`（已移至 `src/deprecated/logger.REMOVED.md` 供迁移说明）
- 迁移方式：
  ```ts
  import { createLogger } from "./src/logging/index.js";
  const logger = createLogger();
  ```
- MCP 日志最佳实践：
  - MCP 服务器（stdio）禁止向 stdout 输出日志（避免污染协议通道）；
  - 调试：设置 `MCP_LOG_STDERR=true` 且 `LOG_PRETTY=false`，日志仅写入 stderr；
  - 生产：默认静默（容器 `loggerSilent: true`）。

## MCP 工具参数兼容说明
- 统一解析入口（getParams）：`arguments` → `params` → handler 入参本体 → `{}`；
- 影响文件：`src/mcp/server.ts`、`src/mcp/tools/roxyAdmin.ts`、`src/mcp/tools/actions.ts`；
- 示例（SDK 客户端）：
  ```ts
  // 规范形式（arguments）
  await client.callTool({ name: 'roxy.windows.list', arguments: { workspaceId: 28255 } })

  // 兼容形式（params，历史客户端）
  await client.callTool({ name: 'roxy.windows.list', arguments: {}, params: { workspaceId: 28255 } } as any)
  ```

## Roxy WorkspaceId 说明
- API 识别数字 id（如 `28255`）；桌面端前缀 `NJJ0******` 仅 UI 展示；
- 正确做法：对 API 一律传数字或字符串数字；不要传前缀格式。


> 多账号自动化：RoxyBrowser（CDP）+ Playwright + MCP TypeScript SDK。
> 模型：多窗口 = 多上下文（一个账号=一个 Roxy 窗口/Context），多页 = 同 Context 多 Page。

### 相关文档
- `docs/browserhost-design.md`（会话/复用/TTL/观测）
- `docs/WORKSPACE_ID_ANALYSIS.md`（Workspace ID 规范）
- `docs/mcp-tools.md`（MCP 工具清单与用例，含 `action.*` 与 `profile` 档位）
- `docs/examples/*.json`（发布示例）

## 快速开始
- Node ≥ 18，已安装 Chromium（`npm postinstall` 会自动安装）。
- 创建 `.env`：复制 `.env.example` 并填写下列变量：
  - `ROXY_API_TOKEN`（必填）
  - `ROXY_API_BASEURL`（可空；默认 `http://127.0.0.1:50000`；也可用 `ROXY_API_HOST/ROXY_API_PORT` 指定）
- 安装依赖：`npm install`
- 环境自检：`npm run check:env`（Zod 校验 + 示例文件检查，可选 `CHECK_ROXY_HEALTH=true` 探活）
- 测试环境：`npm run test:workspace-id`（验证 RoxyBrowser API 连接和 workspace ID 格式）
- 列出任务：`npm run dev -- --list-tasks`
- 示例运行：
  - 指定账号列表：`--dir-ids=dirA,dirB` 或多次 `--dirId=dirA --dirId=dirB`
  - 截图：`npm run dev -- --dir-ids=dirA --task=openAndScreenshot --payload={"url":"https://example.com"}`
  - 发布草稿（基于选择器直驱的 xhs.publish）：`npm run dev -- --dir-ids=dirA --task=xhs.publish --payload=@docs/examples/publish-payload.json`
  - 本地演示（无需 MCP）：`npm run demo:local -- --dir-ids=dirA --url=https://example.com`
  - 可选工作区：加 `--workspace-id=YOUR_WORKSPACE`

⚠️ **重要提示：Workspace ID 格式**
- RoxyBrowser API 返回的 workspace ID 是**数字类型**（例如 `28255`）
- 桌面应用显示的带前缀格式（例如 `NJJ0028255`）**仅用于前端展示**
- 调用 API 时应使用数字 ID，详见 [Workspace ID 分析](docs/WORKSPACE_ID_ANALYSIS.md)

## 多账号并发与策略
- 并发单位为 `dirId`（账号上下文=窗口）。
- 并发/超时在 `.env` 配置：`MAX_CONCURRENCY`、`TIMEOUT_MS`。
- 限速/熔断：`POLICY_QPS`、`POLICY_FAILURES`、`POLICY_OPEN_SECONDS`（见下文）。

## 环境变量（.env）
- `ROXY_API_TOKEN`: Roxy 本地 API 鉴权（Header `token`）。
- `ROXY_API_BASEURL`（可空，默认 `http://127.0.0.1:50000`）或 `ROXY_API_HOST`+`ROXY_API_PORT`：Roxy 本地 API 地址。
- `ROXY_DIR_IDS`（可选后备）：未通过 CLI/MCP 显式传参时作为后备来源（逗号分隔）。
- `MAX_CONCURRENCY`：并发任务数（默认 2）。
- `TIMEOUT_MS`：单任务超时（默认 60000）。
- `POLICY_QPS`：近似每秒请求上限（默认 5）。
- `POLICY_FAILURES`：连续失败熔断阈值（默认 5）。
- `POLICY_OPEN_SECONDS`：熔断打开时长（秒，默认 15）。

策略档位建议：
- 低风险：`POLICY_QPS=2 POLICY_FAILURES=3 POLICY_OPEN_SECONDS=30`
- 平衡型：`POLICY_QPS=5 POLICY_FAILURES=5 POLICY_OPEN_SECONDS=15`
- 激进型（不推荐）：`POLICY_QPS=10 POLICY_FAILURES=8 POLICY_OPEN_SECONDS=8`

## 任务与步骤 DSL
- 注册中心：`src/tasks/registry.ts`（任务签名 `(ctx, dirId, args)`）。
- 内置任务：
  - `openAndScreenshot`：打开 URL 并截图。
  - `xhs.checkSession`：弱信号会话检查（Cookies）。
  - `xhs.noteCapture`：保存 HTML + 截图。
  - `xhs.publish`：按 `selectorMap` 上传/填写/提交草稿。
  - （已移除）`xhs.interact`：步骤 DSL 已下线；请直接使用 `action.*` 工具或 `xhsShortcuts`。
  - （已移除）`xhs.randomBrowse`：内部不再提供随机浏览编排，推荐由外部工作流以 `action.*`/`xhsShortcuts` 组合。
- 步骤 DSL 已移除；请直接使用 `action.*` 工具或 `xhsShortcuts`；选择器写法见 `docs/selectors-map.md` 与迁移指南 `docs/MIGRATION-SELECTOR-ONLY.md`

### 弹性选择器系统（Resilient Selector System）

本项目实现了企业级的弹性选择器系统，提供三层防护机制确保自动化任务的稳定性和可观测性。

#### 核心特性

**三层防护机制：**

1. **自动重试（Retry with Exponential Backoff）**
   - 选择器失败后自动重试，使用指数退避策略
   - 可配置重试次数、基础延迟、最大延迟
   - 每次重试添加随机 jitter 避免雷鸣群效应

2. **断路器保护（Circuit Breaker）**
   - 防止连续失败导致系统级联故障
   - QPS 限流、失败阈值、熔断时长可配置
   - 开路/半开路/闭路状态自动切换

3. **健康度监控（Health Monitoring）**
   - 实时跟踪每个选择器的成功率、平均耗时、失败次数
   - 自动生成健康度报告和优化建议
   - 支持定时报告和 JSON 导出

#### 快速使用

**基础用法：**

```typescript
import { resolveLocatorResilient } from "./src/selectors/index.js";
import type { TargetHints } from "./src/selectors/types.js";

// 定义选择器提示
const hints: TargetHints = {
  role: "button",
  name: "登录",
  alternatives: [
    { selector: "#login-btn" },
    { selector: ".login-button" },
  ],
};

// 使用弹性选择器解析
const locator = await resolveLocatorResilient(page, hints, {
  selectorId: "login-button",      // 用于健康度跟踪
  retryAttempts: 3,                 // 重试次数
  retryBaseMs: 200,                 // 重试基础延迟
  retryMaxMs: 2000,                 // 重试最大延迟
  verifyTimeoutMs: 1000,            // 验证超时
});

// 选择器已验证可见，直接使用
await locator.click();
```

**健康度报告：**

```typescript
import { generateHealthReport, logHealthReport } from "./src/selectors/report.js";

// 生成报告
const report = generateHealthReport({
  unhealthyThreshold: 0.7,    // 不健康阈值（成功率 < 70%）
  minSampleSize: 5,            // 最小样本数
  includeHealthy: true,        // 包含健康选择器
});

// 记录到日志
logHealthReport(report);

// 导出为 JSON
const json = exportReportAsJson(report);
```

**报告示例：**

```json
{
  "timestamp": "2025-10-23T06:00:00.000Z",
  "totalSelectors": 10,
  "healthyCount": 8,
  "unhealthyCount": 2,
  "averageSuccessRate": 0.85,
  "unhealthySelectors": [
    {
      "selectorId": "nav-discover",
      "totalCount": 100,
      "successCount": 60,
      "failureCount": 40,
      "successRate": 0.6,
      "avgDurationMs": 1500
    }
  ],
  "recommendations": [
    "选择器 \"nav-discover\" 成功率过低（60.0%），建议立即检查选择器定义是否正确",
    "选择器 \"nav-discover\" 需要优化：成功率 60.0%，平均耗时 1500ms"
  ]
}
```

#### 性能基准

- **成功的选择器解析**：< 500ms
- **带重试的选择器解析**：< 1000ms
- **健康选择器成功率**：> 90%
- **可接受选择器成功率**：> 70%

#### 选择器优先级策略

遵循 Playwright 推荐的语义优先级：

1. **role + name**（最稳定，推荐）- `{ role: "button", name: "登录" }`
2. **label**（表单字段首选）- `{ label: "用户名" }`
3. **placeholder**（输入框备选）- `{ placeholder: "搜索小红书" }`
4. **testId**（自有应用推荐）- `{ testId: "search-button" }`
5. **CSS selector**（最后选择）- `{ selector: "#search-input" }`

#### 迁移指南

已完成迁移的模块：
- ✅ `src/domain/xhs/navigation.ts` - 导航和模态窗口处理
- ✅ `src/domain/xhs/search.ts` - 搜索输入和提交

**详细文档：**

- **最佳实践指南**：[docs/selectors-best-practices.md](docs/selectors-best-practices.md)
  - 完整使用示例
  - 选择器命名规范
  - 重试配置建议
  - 故障排查指南
  - 性能优化技巧

- **迁移指南**：[docs/migration-guide.md](docs/migration-guide.md)
  - 从 `resolveLocatorAsync` 迁移到 `resolveLocatorResilient`
  - 代码对比示例
  - 迁移检查清单
  - FAQ 和常见问题

- **选择器映射规范**：[docs/selectors-map.md](docs/selectors-map.md)
  - 选择器字段说明
  - 容器和过滤写法

#### 配置参数

**全局配置（src/config/xhs.ts）：**

```typescript
export const XHS_CONF = {
  selector: {
    probeTimeoutMs: 250,           // 探测超时
    resolveTimeoutMs: 3000,        // 解析超时
    healthCheckIntervalMs: 60000,  // 健康检查间隔
  },
} as const;
```

**断路器配置（src/selectors/resilient.ts）：**

```typescript
const policyEnforcer = new PolicyEnforcer({
  qps: 10,                 // 每秒请求数限制
  failureThreshold: 3,     // 连续失败阈值
  openSeconds: 10,         // 熔断时间（秒）
});
```

#### 测试覆盖

- **单元测试**：44 个测试覆盖核心选择器逻辑
- **集成测试**：25 个测试覆盖真实场景
  - 网络延迟处理
  - 间歇性失败重试
  - 断路器触发验证
  - 并发场景处理
  - 健康度报告生成
  - 性能基准验证

**运行测试：**

```bash
npm test                          # 运行所有测试
npm test -- selectors             # 运行选择器相关测试
npm test -- integration           # 运行集成测试
```

## MCP（stdio）
- 启动：`npm run mcp`
- 工具清单与示例：见 `docs/mcp-tools.md`
- 连接与状态：本服务内建连接管理器，按 `dirId` 复用已打开的浏览器与页面；跨多次工具调用保持页面状态，直至调用 `roxy.closeDir` 或进程退出（收到 SIGINT/SIGTERM 时会尝试优雅清理）。
- 工具：
  - `roxy.openDir(dirId, workspaceId?)`
  - `roxy.closeDir(dirId)`
  - `roxy.workspaces.list(page_index?, page_size?)`
  - `roxy.windows.list(workspaceId?, dirIds?, windowName?, sortNums?, os?, projectIds?, windowRemark?, page_index?, page_size?, ...)`
  - `roxy.window.create(workspaceId, windowName?, os?, osVersion?, coreVersion?, projectId?, windowRemark?, defaultOpenUrl?, proxyInfo?, ...)`
  - `xhs.session.check(dirId, workspaceId?)`
  - `page.new(dirId, url?, workspaceId?)` / `page.list(dirId, workspaceId?)` / `page.close(dirId, pageIndex?, workspaceId?)`
  - `action.navigate(dirId, url, pageIndex?, wait?, workspaceId?)`
  - `action.click(dirId, target, pageIndex?, workspaceId?)`
  - `action.type(dirId, target, text, wpm?, pageIndex?, workspaceId?)`
  - `action.hover(dirId, target, pageIndex?, workspaceId?)`
  - `action.scroll(dirId, deltaY?, pageIndex?, workspaceId?)`
  - `action.waitFor(dirId, target?, state?, timeoutMs?, pageIndex?, workspaceId?)`
  - `action.upload(dirId, target, files[], pageIndex?, workspaceId?)`
  - `action.extract(dirId, target, prop?, pageIndex?, workspaceId?)`
  - `action.evaluate(dirId, expression, arg?, pageIndex?, workspaceId?)`
  - `action.screenshot(dirId, pageIndex?, fullPage?, workspaceId?)`

## 人性化与稳定性
- `src/humanization/*`：
  - 鼠标曲线：`moveMouseTo()`
  - 段落滚动：`scrollHuman()`
  - 拟人打字：`typeHuman()`
  - 安全点击：在点击前 `scrollIntoViewIfNeeded() + waitFor({state:'visible'})`，避免元素脱离视口。
- `action.*`：原子动作失败自动截图（由上层调用决定），并统一返回 `ActionResult`，包含 `error.code/message/screenshotPath`。

## 产物与指标
- 每账号清单：`artifacts/<dirId>/manifest-*.json`
- 全局指标：`artifacts/metrics-*.json`
- 选择器健康度（NDJSON）：`artifacts/selector-health.ndjson`（可通过 `SELECTOR_HEALTH_PATH` 自定义；`SELECTOR_HEALTH_DISABLED=true` 可关闭）

## 类型安全与运行时验证

### 类型系统架构

本项目采用完整的端到端类型安全设计，确保编译时和运行时的双重保护：

**架构分层：**
```
src/types/          - TypeScript 类型定义（编译时检查）
src/schemas/        - Zod Schema 定义（运行时验证）
src/clients/        - API 客户端实现（类型安全 + 验证）
src/contracts/      - 接口契约（依赖倒置）
```

**核心原则：**
1. **单一事实来源** - 类型从 Zod Schema 推断（`z.infer<typeof Schema>`）
2. **运行时验证** - 所有 API 响应使用 Zod 验证
3. **空值安全** - 正确处理 nullable 响应
4. **类型推断** - 利用 TypeScript 类型推断减少重复

### 使用示例

#### 1. 类型安全的 API 调用

```typescript
import type { IRoxyClient } from "./src/contracts/IRoxyClient.js";
import type { WorkspaceListResponse, WindowListResponse } from "./src/types/roxy/index.js";

// 完整类型推断
const client: IRoxyClient = container.createRoxyClient();

// 类型安全的方法调用
const workspaces: WorkspaceListResponse = await client.workspaces({
  page_index: 1,
  page_size: 20
});

// 类型安全的数据访问
if (workspaces.data?.rows) {
  workspaces.data.rows.forEach(workspace => {
    console.log(`工作区: ${workspace.workspaceName} (ID: ${workspace.id})`);
  });
}

// 处理 nullable 响应
const windows: WindowListResponse = await client.listWindows({
  workspaceId: 28255
});

if (windows.data !== null) {
  // TypeScript 知道 data 不为 null
  const count = windows.data.rows.length;
  console.log(`找到 ${count} 个窗口`);
} else {
  console.log("没有窗口数据");
}
```

#### 2. Zod 运行时验证

```typescript
import { WorkspaceListResponseSchema } from "./src/schemas/roxy/index.js";

// 方式 1: 使用 safeParse（推荐）
const result = WorkspaceListResponseSchema.safeParse(apiResponse);

if (result.success) {
  // 验证通过，data 已完全类型化
  const data = result.data;
  console.log(data.data?.rows);
} else {
  // 验证失败，查看详细错误
  console.error("验证失败:", result.error.format());
}

// 方式 2: 使用 parse（抛出异常）
try {
  const validated = WorkspaceListResponseSchema.parse(apiResponse);
  // validated 已完全类型化
} catch (error) {
  if (error instanceof z.ZodError) {
    console.error("Schema 验证错误:", error.format());
  }
}
```

#### 3. 自定义 Schema 验证

```typescript
import { z } from "zod";
import { ApiResponseSchema } from "./src/schemas/roxy/common.js";

// 定义自定义数据 Schema
const CustomDataSchema = z.object({
  id: z.string(),
  name: z.string().min(1),
  tags: z.array(z.string()).optional(),
});

// 包装为 API 响应格式
const CustomResponseSchema = ApiResponseSchema(CustomDataSchema);

// 推断类型
type CustomResponse = z.infer<typeof CustomResponseSchema>;

// 验证响应
const response = await fetch("/api/custom");
const parsed = CustomResponseSchema.safeParse(await response.json());
```

#### 4. 处理联合类型

```typescript
// windowSortNum 可以是 string 或 number
import type { Window } from "./src/types/roxy/window.js";

function processWindow(window: Window) {
  // TypeScript 知道 windowSortNum 是 string | number | undefined
  const sortNum = window.windowSortNum;

  if (typeof sortNum === "number") {
    console.log(`序号: ${sortNum.toFixed(0)}`);
  } else if (typeof sortNum === "string") {
    console.log(`序号: ${sortNum}`);
  }
}
```

### 迁移指南

如果你正在从旧版本迁移，请注意以下变更：

#### Breaking Changes

1. **响应类型变更** - 所有 API 方法现在返回具体类型而非 `unknown`：
   ```typescript
   // ❌ 旧版本
   const workspaces: any = await client.workspaces();

   // ✅ 新版本
   const workspaces: WorkspaceListResponse = await client.workspaces();
   ```

2. **Nullable 响应** - 某些 API 响应的 data 字段现在可能为 null：
   ```typescript
   // ❌ 旧版本（假设 data 总是存在）
   const windows = await client.listWindows({ workspaceId: 28255 });
   windows.data.rows.forEach(...); // 可能崩溃

   // ✅ 新版本（安全检查）
   const windows = await client.listWindows({ workspaceId: 28255 });
   if (windows.data !== null) {
     windows.data.rows.forEach(...);
   }
   ```

3. **字段名变更** - 窗口列表使用 `rows` 而非 `list`：
   ```typescript
   // ❌ 旧版本
   const windows = await client.listWindows({ workspaceId: 28255 });
   const list = windows.data.list; // 不再存在

   // ✅ 新版本
   const windows = await client.listWindows({ workspaceId: 28255 });
   const list = windows.data?.rows; // 统一使用 rows
   ```

4. **新增 API 方法** - `randomFingerprint` 方法：
   ```typescript
   // 为窗口生成随机指纹
   await client.randomFingerprint(28255, "dirId123");
   ```

#### 非破坏性改进

- **ensureOpen** - 现在正确处理 null 响应，自动降级到 open
- **TypeScript 严格模式** - 项目现在启用完整的严格类型检查
- **错误处理改进** - ValidationError 包含详细的 Zod 错误信息

### API 参考

#### IRoxyClient 接口

完整的类型安全 API 客户端：

```typescript
interface IRoxyClient {
  // 健康检查
  health(): Promise<{ code?: number; msg?: unknown } | string>;

  // 窗口操作
  open(dirId: string, args?: string[], workspaceId?: string | number):
    Promise<ConnectionInfo>;
  close(dirId: string): Promise<void>;
  ensureOpen(dirId: string, workspaceId?: string | number, args?: string[]):
    Promise<ConnectionInfo>;

  // 连接信息
  connectionInfo(dirIds: string[]): Promise<ConnectionInfoResponse>;

  // 工作空间管理
  workspaces(params?: WorkspaceListParams): Promise<WorkspaceListResponse>;

  // 窗口管理
  listWindows(params: WindowListParams): Promise<WindowListResponse>;
  createWindow(body: WindowCreateRequest): Promise<WindowCreateResponse>;
  detailWindow(params: WindowDetailParams): Promise<WindowDetailResponse>;
  randomFingerprint(workspaceId: number | string, dirId: string):
    Promise<ApiResponse<null>>;
}
```

**参数类型：**

```typescript
// 工作空间列表参数
interface WorkspaceListParams {
  page_index?: number; // >= 1
  page_size?: number;  // >= 1
}

// 窗口列表参数
interface WindowListParams extends PaginatedParams {
  workspaceId: number | string; // 必需
  dirIds?: string;               // 逗号分隔
  windowName?: string;
  sortNums?: string;
  os?: string;
  // ... 更多过滤参数
}
```

**响应类型：**

```typescript
// 统一响应格式
interface ApiResponse<T> {
  code: number;    // 0 表示成功
  msg: string;     // 响应消息
  data: T;         // 响应数据（可能为 null）
}

// 分页响应
interface PaginatedResponse<T> {
  total: number;   // 总记录数
  rows: T[];       // 当前页数据
}

// 连接信息
interface ConnectionInfo {
  id: string;      // 窗口 ID
  ws: string;      // WebSocket 端点
  http?: string;   // HTTP 端点（可选）
}
```

### 最佳实践

1. **始终检查 nullable 字段**
   ```typescript
   const windows = await client.listWindows({ workspaceId: 28255 });
   if (windows.data?.rows) {
     // 安全访问
   }
   ```

2. **使用类型守卫**
   ```typescript
   function isValidConnection(conn: ConnectionInfo | null): conn is ConnectionInfo {
     return conn !== null && !!conn.ws;
   }
   ```

3. **利用 Zod transform**
   ```typescript
   const schema = z.string().transform(s => parseInt(s, 10));
   ```

4. **错误处理**
   ```typescript
   try {
     const result = await client.open(dirId);
   } catch (error) {
     if (error instanceof ValidationError) {
       console.error("验证失败:", error.context?.zodError);
     }
   }
   ```

## 架构分层
- `src/config/`（Zod 校验）→ `src/lib/http.ts`（重试） → `src/clients/roxyClient.ts` → `src/services/`（playwrightConnector/policy/artifacts/metrics）→ `src/runner/` → `src/steps/` → `src/tasks/` → `src/cli.ts` / `src/mcp/server.ts`

## 已知限制
- 通过 CDP 连接远程浏览器，个别特性（如完整 tracing/video）受限于通道能力。
- 本项目不提供登录绕过等敏感逻辑，`xhs.checkSession` 仅为弱信号判断。

## C# 对照
- 见 `docs/csharp-parity.md`（更新于 2025-10-21）。
- 旧版 .NET 文档：`docs/legacy/README-dotnet.md`。

## 开发脚本
- Lint：`npm run lint`
- 格式化：`npm run format` / `npm run format:check`
- 测试：`npm test`（Vitest）
