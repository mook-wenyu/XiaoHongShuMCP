# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### BREAKING CHANGES

#### 笔记采集服务简化 (TASK-20251001-003)

**影响范围**: 内部服务层（NoteCaptureContext, INoteRepository, NoteRepository）

**变更内容**:

1. **删除 NoteCaptureContext 的三个参数**
   - SortBy → 完全删除
   - NoteType → 完全删除
   - PublishTime → 完全删除

2. **简化 INoteRepository.QueryAsync 签名**
   - 从 5 个参数简化为 2 个参数
   - 只保留 keyword 和 targetCount

3. **简化 NoteRepository 查询逻辑**
   - 删除类型过滤（noteType）
   - 删除时间过滤（publishTime）
   - 删除排序选择（sortBy）
   - 固定使用 score 降序排序（comprehensive 默认行为）

4. **更新所有调用方**
   - NoteCaptureTool
   - NoteCaptureService
   - BrowserAutomationService
   - NoteEngagementService

**理由**:
- 极简主义设计：删除所有动态过滤和排序配置
- 简化封装层级：减少参数传递链路
- 固定最佳实践：始终使用综合评分排序
- 降低复杂度：从 5 参数简化为 2 参数

**测试覆盖**:
- 构建通过：0 warnings, 0 errors
- 测试结果：NoteCaptureToolTests 2/2 通过

**注意**: 此为内部服务层破坏性变更，不影响 MCP 工具层 API。

---

#### 笔记采集工具极简化 (TASK-20251001-002)

**影响范围**: `xhs_note_capture` MCP 工具

**变更内容**:

1. **删除 RunHumanizedNavigation 参数**
   - 强制始终执行人性化导航
   - 无法关闭该功能

2. **删除 NoteCaptureFilterSelections 类型**
   - 完全移除该类型定义
   - NoteCaptureToolResult 不再返回过滤条件信息

3. **极简化 NoteCaptureToolResult**
   - 从 13 个字段简化为 3 个核心字段
   - 删除的字段：
     * RawPath (IncludeRaw 固定 false)
     * Duration (性能调试信息)
     * RequestId (已在 Metadata 中)
     * BehaviorProfileId (调试信息)
     * FilterSelections (完整删除)
     * HumanizedActions (调试信息)
     * Planned (调试信息)
     * Executed (调试信息)
     * ConsistencyWarnings (调试信息)
     * SelectedKeyword (与 Keyword 冗余)

**迁移指南**:

```javascript
// 旧代码（不再可用）
await callTool("xhs_note_capture", {
  keywords: ["露营"],
  targetCount: 20,
  browserKey: "user",
  runHumanizedNavigation: false  // ❌ 删除，强制为 true
});

// 新代码（极简后）
await callTool("xhs_note_capture", {
  keywords: ["露营"],
  targetCount: 20,
  browserKey: "user"
});

// 返回值变更
// 旧代码（13 个字段）
const {
  keyword, csvPath, rawPath, collectedCount, duration,
  requestId, behaviorProfileId, filterSelections,
  humanizedActions, planned, executed, consistencyWarnings,
  selectedKeyword
} = result.data;

// 新代码（3 个核心字段）
const { keyword, csvPath, collectedCount } = result.data;

// requestId 从 Metadata 获取
const requestId = result.metadata.requestId;
```

**理由**:
- 极简主义设计到极致
- 强制执行最佳实践（始终人性化）
- 删除所有调试和冗余信息
- 客户端仅需要核心结果

**测试覆盖**:
- 更新 `NoteCaptureToolTests` 适配新结构
- 测试改名：`CaptureAsync_WhenNavigationFails_ShouldReturnError`
- 构建通过：0 warnings, 0 errors
- 测试结果：NoteCaptureToolTests 2/2 通过

**注意**: 此为极端破坏性变更，不向后兼容。所有客户端必须重写调用代码。

---

#### 笔记采集工具参数简化 (TASK-20251001-001)

**影响范围**: `xhs_note_capture` MCP 工具

**变更内容**:

从 `NoteCaptureToolRequest` 中删除 6 个参数，改为使用硬编码默认值：

1. **SortBy** (排序方式) → 硬编码为 `"comprehensive"`（综合排序）
2. **NoteType** (笔记类型) → 硬编码为 `"all"`（所有类型）
3. **PublishTime** (发布时间) → 硬编码为 `"all"`（所有时间）
4. **IncludeAnalytics** (分析字段) → 硬编码为 `false`（不包含）
5. **IncludeRaw** (原始 JSON) → 硬编码为 `false`（不生成）
6. **OutputDirectory** (输出目录) → 硬编码为 `"./logs/note-capture"`（默认路径）

**迁移指南**:

```javascript
// 旧代码（不再可用）
await callTool("xhs_note_capture", {
  keywords: ["露营"],
  targetCount: 20,
  sortBy: "comprehensive",        // ❌ 删除
  noteType: "all",                // ❌ 删除
  publishTime: "all",             // ❌ 删除
  includeAnalytics: false,        // ❌ 删除
  includeRaw: false,              // ❌ 删除
  outputDirectory: "./output",    // ❌ 删除
  browserKey: "user",
  runHumanizedNavigation: true
});

// 新代码（简化后）
await callTool("xhs_note_capture", {
  keywords: ["露营"],
  targetCount: 20,
  browserKey: "user",
  runHumanizedNavigation: true
});
```

**理由**:
- 极简主义设计：遵循 "Convention over Configuration" 原则
- 减少 MCP 工具接口复杂度
- 大多数用户使用默认值即可满足需求
- 与之前的 Metadata 简化方向保持一致

**测试覆盖**:
- 更新 `NoteCaptureToolTests` 适配新参数结构
- 构建通过：0 warnings, 0 errors
- 测试结果：NoteCaptureToolTests 2/2 通过

**内部实现**:
- `NoteCaptureContext` 保持不变（内部使用）
- `NoteCaptureFilterSelections` 保持不变（返回给客户端展示固定值）
- 默认值在 `NoteCaptureTool.ExecuteAsync` 中硬编码

**注意**: 此变更为破坏性更改，不向后兼容。所有客户端必须更新调用代码。

---

#### 数据结构序列化支持与元数据简化 (TASK-20250202-001)

**影响范围**: 所有 MCP 工具返回值

**变更内容**:

1. **数据结构 JSON 序列化支持**
   - `OperationResult<T>`: 从 class 转换为 record 类型，确保可 JSON 序列化
   - `HumanizedActionScript`: 从 class 转换为 record 类型，添加 `[JsonConstructor]` 支持
   - `NetworkSessionContext.ExitIp`: 从 `IPAddress?` 类型改为 `string?` 类型

2. **工具返回元数据简化**
   - `BrowserTool`: `Metadata` 字段从 20+ 字段简化为仅保留 `requestId`
   - `NoteCaptureTool`: `Metadata` 字段从 15+ 字段简化为仅保留 `requestId`
   - 所有业务数据已完整保留在 `Data.SessionMetadata` 中

**迁移指南**:

如果您的客户端代码访问了 Metadata 字段，需要按以下方式迁移：

```javascript
// 旧代码（不再可用）
const fingerprint = result.metadata.fingerprintHash;
const proxyId = result.metadata.networkProxyId;
const keyword = result.metadata.selectedKeyword;

// 新代码（使用 Data 字段）
const fingerprint = result.data.sessionMetadata?.fingerprintHash;
const proxyId = result.data.sessionMetadata?.proxyId;
const keyword = result.data.selectedKeyword;  // NotCaptureTool

// requestId 仍可从 Metadata 获取
const requestId = result.metadata.requestId;
```

**理由**:
- 确保所有数据结构符合 MCP stdio 协议的 JSON 序列化要求
- 消除 Metadata 与 Data.SessionMetadata 之间的冗余信息
- 遵循 MCP 最佳实践：Metadata 用于请求追踪，Data 用于业务数据

**测试覆盖**:
- 添加 `SerializationTests` 验证所有数据结构可正确 JSON 序列化
- 更新 `NoteCaptureToolTests` 适配简化后的 Metadata
- 构建通过：0 warnings, 0 errors
- 测试结果：52/56 通过（4个失败为转换前就存在的问题）

**运行时验证**:
- ✅ 验证场景 (`--verification-run`) 成功执行
- ✅ 浏览器自动化正常工作（打开用户配置、页面导航）
- ✅ 无 JSON 序列化异常
- ✅ 无 Metadata 字段访问错误
- ✅ 网络策略正常触发（429 缓解机制）
- 验证日期：2025-10-01

**文档参考**:
- 详细设计文档：`docs/workstreams/TASK-20250202-001/design.md`
- 研究分析：`docs/workstreams/TASK-20250202-001/research.md`

---

## [Previous Releases]

### [Initial Release]
- 初始项目发布
