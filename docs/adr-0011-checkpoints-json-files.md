# ADR-0011：检查点持久化改为文件 JSON（弃用 SQLite）

状态：Accepted（2025-09-13）

## 背景与问题

- 既有方案使用 SQLite（`Microsoft.Data.Sqlite`）存储检查点，虽然稳定，但引入了额外二进制依赖、跨平台行为差异（JSON1/ICU）、以及包版本冲突风险。
- 项目已执行“装配合流”（Adapters/Observability/Persistence → Core），为降低维护面并执行“绝不向后兼容”的演进策略，决定移除 SQLite 依赖。

## 决策

- 采用“文件系统 + JSON 文档”的检查点仓库：`FileJsonCheckpointRepository`。
- 存储模型：目录内每个 operationId 一个文件，保存最新 `CheckpointEnvelope`（如需历史可后续扩展）。
- 文件名安全：`sha256(operationId).hex16.json`，避免非法字符与路径遍历。
- 原子写入：`tmp` → `Move(overwrite: true)`；读取失败（并发覆盖/半写）自动忽略。
- 过滤行为：`ListLatestAsync(prefix)` 通过解析文件内容按真实 `OperationId` 前缀过滤，保证语义一致。
- 配置键破坏性变更：
  - 旧：`XHS:Resumable:DbPath`（废弃）
  - 新：`XHS:Resumable:Dir`（默认值：`.data/checkpoints`）

## 方案对比

1) SQLite（旧）
- 优点：查询灵活、并发控制成熟、单文件易于备份。
- 缺点：额外依赖、跨平台差异、包版本协调成本、对我们“仅需最新记录”的场景偏重。

2) 文件 JSON（采纳）
- 优点：零外部依赖、实现简单、可读可审计、与“最新即真”的模型匹配。
- 缺点：大规模 operationId 时目录枚举 O(N)；需自行处理原子写与文件名安全。
- 适用：当前规模与访问模式（最新读/写占优、ListLatest topN≤50）。

## 影响范围（破坏性）

- 删除 `SqliteJsonCheckpointRepository` 及 `Microsoft.Data.Sqlite` 依赖。
- `Program.cs` 切换为 `FileJsonCheckpointRepository` 注入；配置键改为 `XHS:Resumable:Dir`。
- 文档与测试全部更新；新增架构守卫测试仍生效（以命名空间约束）。

## 迁移策略

- 不提供自动迁移（遵循“绝不向后兼容”）：请手工导出旧 SQLite 中最新信封为 JSON，并以 `sha256(op).json` 文件名写入新目录。
- 若无需历史，直接清空旧 DB 并启用新配置键即可。

## 验证

- 单测：文件仓库读写/覆盖/过滤；Resumable 三链路注入文件仓库通过；总计 125/125 通过。
- 性能：小规模下 ListLatest O(N) 可接受；未来如需可引入索引文件或亚目录分片。

## 结论

文件 JSON 仓库满足功能、可靠性与可维护性目标，并显著降低依赖与冲突面，符合“标准化 + 生态复用、删除自研与过时内容”的原则。

