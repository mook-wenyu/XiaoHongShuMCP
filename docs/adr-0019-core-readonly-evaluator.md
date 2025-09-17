# ADR-0019：在 Core 引入只读 Evaluate 抽象并接线 Humanization（2025-09-14）

## 背景
- 反检测策略要求：业务层零注入，读写分离，且“只读 Evaluate”需集中门控与计量，生产默认禁用、白名单极小化。
- Humanization/Selectors 等通用模块需要在不依赖具体适配器或应用门控实现的前提下，执行少量只读 DOM 探针（如 role/aria/可聚焦）。

## 决策
- 在 `HushOps.Core` 新增抽象接口 `HushOps.Core.Humanization.IReadonlyJsEvaluator`，仅暴露只读 Evaluate：
  - `Task<T?> PageEvalAsync(IAutoPage, script, path, ct)`
  - `Task<T?> ElementEvalAsync(IAutoElement, script, path, ct)`
- 在应用层新增适配器 `ReadonlyJsEvaluatorAdapter`，将现有 `Utilities.IEvalGuard` 桥接为 `IReadonlyJsEvaluator`，继承其门控与计量能力。
- 修改 `DomPreflightInspector` 依赖为 `IReadonlyJsEvaluator`（仍位于应用层，后续迁移到 Core），保持“点击前语义预检”只读且可观测。
- 依赖注入：在 `Program.cs` 注册 `IReadonlyJsEvaluator` 指向适配器，默认禁用只读 Evaluate（`XHS:InteractionPolicy:EnableJsReadEval=false`）。

## 影响
- 破坏性：面向后续迁移，Humanization 相关实现将逐步迁出应用层，依赖 Core 抽象；
- 安全性：继续执行“只读 Evaluate 清零”目标，生产默认禁用，白名单路径要求低基数；
- 可测试性：新增适配器层与适配器门控单测（见 `AdapterTelemetryGuardTests`）。

## 备选方案
1. 直接在 Core 内部实现门控与白名单（放弃应用层门控）：否，违背“应用层策略可配置”的原则；
2. 继续在应用中直接依赖 `IEvalGuard`：否，不利于 Humanization/Selectors 下沉与复用。

## 后续
- P1 后续批次：将 `DomPreflightInspector`、`ClickabilityDetector`、`DelayManager/PacingAdvisor` 下沉到 `HushOps.Core.Humanization`；
- 引入低基数指标白名单守卫到更多策略路径，完善告警规则。

