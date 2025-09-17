# 评估使用率面板集成（ui_injection_total{type="eval"}）

## 目的
- 可视化只读 Evaluate 的使用趋势与路径分布（应尽可能接近 0）。
- 辅助收紧 EnableJsReadEval 门控策略（分场景白名单）与及时发现异常。

## 面板片段
- 见 `docs/grafana/panels-eval-usage.json`，包含：
  - 时间序列：`sum by (path) (rate(ui_injection_total{type="eval"}[5m]))`
  - 统计总量：`sum(increase(ui_injection_total{type="eval"}[1h]))`

## 集成步骤
1. 在 Grafana 的仪表盘编辑器中，导入上述 JSON 片段（或手动创建查询）。
2. 与 SelectorTelemetry 相关面板放置在“定位与交互”分组下。
3. 配合告警规则：当 `increase(ui_injection_total[15m]) > 0` 触发 Warning；持续异常时提升等级。

## 最佳实践
- 默认生产将 `XHS:InteractionPolicy:EnableJsReadEval=false`；
- 在预发环境按路径白名单局部放开并观察 path 级别曲线；
- 逐步移除不必要探针，保留必要只读探针（如 clickability/visibility）。

