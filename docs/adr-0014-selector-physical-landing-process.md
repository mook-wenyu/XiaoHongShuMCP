# ADR-0014：选择器“物理落地”流程（基于计划的源码重排/清理）

日期：2025-09-14

## 背景
- 运行期已通过 SelectorTelemetry+WeakSelectorGovernor 支持“重排到末位”的弱选择器治理。
- 为减少长期维护面，需要将稳定的治理结果“物理落地”到 DomElementManager.cs，并以 ADR 留痕。

## 决策
- 引入 MCP 工具链与 CI 工作流：导出计划 → 生成 ADR-0013（变更清单）→ 生成最小源码补丁（reorder/prune）→ 人工审阅后合入。
- 默认采用“reorder”模式；对确认无效的选择器在下一轮采用“prune”模式物理删除。

## 流程
1. 收集至少一周遥测数据；
2. 触发工作流“Selector Maintenance”（或本地脚本）：
   - export_plan.sh（阈值/样本数可配置）
   - generate_adr.sh（生成 ADR-0013）
   - generate_patch.sh（生成 docs/selector-plans/diff-YYYYMMDD.patch）
3. 审阅 ADR 与补丁；按需调整阈值与选择器，确保 A11y/语义优先；
4. 合入补丁，完成“物理落地”。

## 影响
- 通过治理减少低命中/高耗时的选择器，提高定位鲁棒性与性能；
- 采用“先运行期、后物理”的两段式，降低错误清理的风险。

## 回滚
- 可通过 plan JSON 逆序恢复原顺序；或从版本控制回退。

