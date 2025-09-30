# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### BREAKING CHANGES

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
