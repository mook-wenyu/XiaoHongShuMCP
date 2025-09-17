## 选择器治理（Selector Governance）设计说明（中文）

### 目标
- 在前端频繁改版下保持定位鲁棒性；
- 以“弱选择器计划”驱动有控回放与回滚；
- 全链路可观测与审计可追溯。

### 流程闭环
- 遥测采集：`SelectorTelemetryService` 统计定位成功率、耗时、退避次数；
- 计划生成：`WeakSelectorGovernor` 基于遥测生成候选弱选择器计划（含影响面评估）；
- 运行时回放：`SelectorPlanHostedService` 支持 Dry-Run 与 Apply 两种模式；
- 落地与回滚：`SelectorMaintenanceTools` 提供 Compare/Build/Apply/Rollback/ADR 工具链；
- 审计：`.audit/selector-plan-*.json` 与 ADR 文档记录每次变更。

### 关键指标（OTel）
- `selectors_plan_proposed_total{mode}`：生成计划次数；
- `selectors_plan_applied_total{mode}`：应用次数；
- `selectors_plan_reverted_total{mode}`：回滚次数；
- `selectors_plan_impact{mode}`：影响面评估（比例/样本数）。

### 使用建议
- MCP 层仅编排工具，不直接改写源码；
- 默认 Dry-Run，人工核验后 Apply；
- 变更必须生成 ADR 与审计凭证。
