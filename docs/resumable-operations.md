# 断流续写（Resumable Operations）使用说明

本文介绍如何使用“可恢复操作 + 检查点仓库”在中断后无损续写长链路任务。

## 设计概览

- 抽象接口（Core）
  - `IResumableOperation<TCkpt>`：定义可恢复操作（运行/续写）
- `ICheckpointRepository`：检查点持久化（默认 文件 JSON，破坏性替代 SQLite）
  - `OperationContext`：运行上下文（operationId/仓库/取消令牌）
  - `CheckpointEnvelope`：检查点信封（OperationId/Seq/Timestamp/Type/DataJSON）
  - `CheckpointSerializer`：强类型检查点序列化工具

- 实现
  - `XiaoHongShuMCP.Persistence/FileJsonCheckpointRepository`（默认目录 .data/checkpoints/）
- 搜索链路示例：`ResumableSearchOperation`（多步：监听+输入+滚动+聚合；已删除服务回退路径，统一监听为唯一权威）

## 配置

默认已内置（Program.cs）：

```
XHS:Resumable:Dir = .data/checkpoints
```

可通过环境变量覆盖：

```
export XHS__Resumable__Dir=/data/xhs/checkpoints
```

## MCP 工具

1) 可恢复搜索：

```
mcp.call("GetSearchNotesResumable", {
  keyword: "咖啡",
  maxResults: 30,
  sortBy: "latest",
  noteType: "all",
  publishTime: "week",
  operationId: "search:coffee:acc001" // 幂等键；留空自动生成
})
```

返回：`success/operationId/seq/checkpoint`。

2) 检查点读取：

```
mcp.call("GetCheckpoint", { operationId: "search:coffee:acc001" })
```

3) 检查点删除：

```
mcp.call("ClearCheckpoint", { operationId: "search:coffee:acc001" })
```

## 多步化规划

- 已落地的阶段划分：EnsureContext → Input（首轮） → AwaitAPI → Aggregate → ScrollNext（未完成时） → Finalize。
- 检查点字段：Stage、Cursor、ScrollOffset、Attempt/MaxAttempts、Aggregated、LastBatch、ProcessedDigest[]、Completed、LastError。
- 禁服务回退：已移除任何“服务直取”回退路径，监听 + 拟人交互 为唯一权威；若阶段失败，记录失败检查点并结束，由上层重试。

### 阶段指标（低基数）

- 统一采用 endpoint=SearchNotes 标签；拒绝高基数标签。
- 直方图（毫秒）：
  - uam_stage_ensure_duration_ms
  - uam_stage_input_duration_ms
  - uam_stage_await_duration_ms
  - uam_stage_aggregate_duration_ms
  - uam_stage_scroll_duration_ms
- 计数器：
  - uam_stage_failures_total

> 说明：默认 IMetrics 基于白名单标签；当前仅开放 endpoint 标签。如需按 stage 维度拆分，请在 Observability 中加入 stage 白名单后再开启。

## 安全与合规

- 禁止在检查点中持久化敏感字段（Authorization/Cookie/xsec_token 等）。
- 检查点仅包含参数快照与摘要信息，且支持脱敏与审计。
