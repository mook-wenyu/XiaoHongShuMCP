# ADR-0018：停止使用 GitHub Actions（改为本地/服务器脚本+计划任务）

日期：2025-09-14

## 背景
为降低供应链与平台耦合风险，并统一“标准化+生态复用、最小外部依赖”的运维策略，项目不再使用 GitHub Actions。所有治理任务（选择器计划导出/落地、CI 守卫、覆盖率统计等）统一通过仓库内脚本在本地或受控服务器执行，并由计划任务（cron）调度。

## 决策
- 删除 `.github/workflows/*` 中所有工作流；
- 保留并完善 `scripts/` 下的本地化脚本：
  - `scripts/ci-guards/scan_for_injection.sh`（禁注入/禁业务层 Evaluate）；
  - `scripts/ci-test-coverage.sh`（覆盖率阈值检查：Core≥90%，其余≥80%）；
  - `scripts/selector-maintenance/*`（导出→ADR→补丁→可选物理落地）；
- 文档中提供 cron 示例与人工审阅流程说明。

## 影响
- 不再支持基于 PR 的云端自动检查；需在本地/服务器执行脚本后再推送；
- 选择器治理“物理落地”继续采用 ADR+补丁+审阅机制，增强可追溯。

## 备选方案
- 迁移至其他 CI/CD 服务：暂不采纳；现阶段以最小可控运维为主。

