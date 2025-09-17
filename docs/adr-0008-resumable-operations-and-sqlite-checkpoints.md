# ADR-0008：断流续写与检查点落盘（IResumableOperation + SQLite）

> 状态：Superseded by ADR-0011（2025-09-13 起以文件 JSON 仓库替代 SQLite）。

日期：2025-09-13

## 背景

长链路任务（搜索/详情/互动/发布）受到网络抖动、浏览器 crash、MCP 断流等影响，需要具备“无损恢复”能力。此前无统一抽象与仓库，恢复成本高，状态一致性不可控。

## 决策

1. 在 Core 引入通用抽象：`IResumableOperation<TCkpt>`、`ICheckpointRepository`、`OperationContext`、`CheckpointEnvelope`，以及序列化工具 `CheckpointSerializer`。
2. 新增工程 `XiaoHongShuMCP.Persistence`，提供 `SqliteCheckpointRepository`（文件级 SQLite，简单稳定）。
3. 检查点内容由业务自定义的强类型对象承载（如滚动偏移、已处理集合摘要、端点游标、重试计数等），以 JSON 落盘；Envelope 记录通用元信息（OperationId/Seq/Timestamp/Type）。
4. 序号 `Seq` 严格递增，便于快速读取最新检查点并进行顺序校验。
5. 样例操作：`IncrementalCursorOperation`（按步推进）用于端到端测试，验证“重启后继续”能力。

## 影响

- 新增引用：`Microsoft.Data.Sqlite (8.0.x)`（Persistence 工程）。
- 测试增加：`SqliteCheckpointRepositoryTests` 和 `IncrementalCursorOperationTests`，总测试数 +2。
- 未来业务链路将迁移到 `IResumableOperation` 协议下，实现统一的恢复点管理与回放验证。

## 取舍

- 选择 SQLite：满足小规模、高可靠、易部署的需求；避免引入重量级数据库依赖。
- 数据模式灵活：Envelope + JSON 强类型，使得各链路可独立扩展检查点字段，保持演进弹性。（破坏性演进时，通过 Type/版本字段兼容迁移）

## 回滚策略

- 若在灰度期间发现持久化异常（卡写/损坏），退回到 Memory-only（以环境变量禁用落盘），并保留日志以便排查。

## 已交付

- Core：`Core/Resumable/ResumableContracts.cs`，`Core/Resumable/Samples/IncrementalCursorOperation.cs`
- Persistence：`SqliteCheckpointRepository.cs`
- Tests：`SqliteCheckpointRepositoryTests.cs`、`IncrementalCursorOperationTests.cs`
