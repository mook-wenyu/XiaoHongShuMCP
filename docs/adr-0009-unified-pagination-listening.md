# ADR-0009（已归档）：统一分页监听为唯一权威，移除服务回退路径（ResumableSearchOperation）

- 日期：2025-09-13
- 状态：Accepted（已合入）

## 背景

历史上 `ResumableSearchOperation` 在具备多步化依赖（浏览器/页面守卫/拟人交互/UAM）失败时，会回退调用服务层的 `SearchNotesAsync` 完成“一次性获取”。该回退增强韧性，但同时引入了两套并存的路径：

- 统一监听策略（UniversalApiMonitor + 拟人输入/滚动 + 去重聚合）
- 服务调用回退（SearchNotesAsync）

双路径导致：
- 行为不一致（监听策略可记录阶段指标/检查点游标；服务回退缺少这些上下文）
- 维护成本增大（两套异常语义/超时策略）
- 架构复杂度升高（测试需要覆盖双分支）

## 决策

- 破坏性更改：删除 `ResumableSearchOperation` 中全部服务回退调用，统一以“监听+输入+滚动+聚合”为唯一权威策略。
- 构造函数取消 `IXiaoHongShuService` 依赖，强制注入以下组件：
  - `IBrowserManager` / `IPageStateGuard` / `IHumanizedInteractionService` / `IPageLoadWaitService` / `IUniversalApiMonitor`
- 当监听路径失败时：
  - 写入失败检查点（`Stage=aggregate`, `LastError` 记录异常消息，`Completed` 仅在 `Attempt>=MaxAttempts` 为 true）
  - 不再进行任何服务层回退调用，由上层以“重试/续写”对待

## 后果

- 优点：
  - 策略唯一，行为稳定；阶段指标（`uam_stage_*`）与检查点游标（`SearchId/PageToken`）持续可用
  - 测试面更聚焦：只需覆盖监听循环、聚合去重与失败检查点
  - 可审计性更强：所有成功/失败均在统一阶段模型中体现
- 代价：
  - 在多步化依赖缺失的环境中无法运行，需要 Host 保证依赖装配
  - 极端场景下需要一次额外的“重试运行”，由可恢复机制承担（非回退）

## 迁移指引

- 若外部代码显式传入 `IXiaoHongShuService` 创建 `ResumableSearchOperation`，请移除该参数，改为注入上文五个强制依赖。
- 工具层（MCP）：`GetSearchNotesResumable` 已更新为 `GetRequiredService` 获取上述依赖，确保运行环境一致。
- 文档：`docs/resumable-operations.md` 已更新为“多步唯一权威”；同时新增本 ADR。

## 验证

- 单元测试：
  - `ResumableSearchMonitorLoopTests`：两轮监听 a,b → b,c，聚合到 3 完成
  - `ResumableSearchOperationTests`：
    - 构造缺失依赖抛出 `ArgumentNullException`
    - UAM 设置失败路径写入失败检查点

## 关联

- ADR-0007：可观测解耦（IMetrics）
- ADR-0008：断流续写与检查点落盘（SQLite）
- （历史）后续 ADR-0010：Prometheus 稳定包切换与 /metrics 暴露（已在 ADR‑0020 删除）。

## 扩展（互动链路纯策略化，M2）

- 移除 `ResumableInteractOperation` 中的 `IXiaoHongShuService` 依赖与 `InteractNoteAsync`/`SearchNotesAsync` 回退调用；
- 新阶段：`verify`（DOM 状态 + UAM API 双重确认）；
- 新指标：互动链路补齐 `ensure/locate/bind/click/await` 直方图与阶段失败计数；
- MCP 工具 `InteractNoteResumable` 随之调整注入依赖。
