# ADR-0013：基于遥测的选择器批量清理/降权计划

日期：2025-09-14 UTC

## 背景与目标
- 依据 SelectorTelemetry 的成功率/耗时统计，对弱选择器进行降权（移至末尾）或后续清理；
- 维持低基数纪律，不记录高基数字段；清理以可回溯计划落地。

## 判定阈值（本次）
- 成功率阈值：70 %
- 最小样本数：5

## 变更项（示例）
## 落地方式
1. 运行工具导出计划：`ExportWeakSelectorPlan` → 生成 JSON；
2. 启动参数或配置 `XHS:Selectors:PlanPath` 指向该 JSON；
3. 服务 `SelectorPlanHostedService` 启动时自动应用；
4. 后续如需永久化，可在 DomElementManager 中物理重排/删除并提交新 ADR。
