# Grafana 面板补充：选择器重排与失败率观测

> 本文提供将 SelectorTelemetry 与定位阶段指标可视化的查询示例，便于评估“重排收益”和识别低效选择器。

## 查询示例

- 重排收益（平均耗时对比）

  - 面板：折线（5m rate）
  - 查询：
    - 成功平均耗时（ms）：
      ```promql
      sum by (strategy) (rate(locate_stage_duration_ms_sum[5m]))
      /
      clamp_min(sum by (strategy) (rate(locate_stage_duration_ms_count[5m])), 0.0001)
      ```

- 失败率（按策略）

  - 面板：柱状
  - 查询：
    ```promql
    sum by (strategy) (rate(locate_failures_total[10m]))
    /
    clamp_min(sum by (strategy) (rate(locate_attempts_total[10m])), 0.0001)
    ```

- 尝试计数（按策略）

  - 面板：表格
  - 查询：
    ```promql
    sum by (strategy) (increase(locate_attempts_total[1h]))
    ```

## 使用说明

- 将本文件中的查询添加到 `docs/grafana/xhs-observability-dashboard.json` 或在 Grafana 中手动创建面板。
- 配合 `docs/alerts/xhs-alert-rules-extra.yaml` 的 `SelectorFailureRateHigh` 告警一起使用。
- 低基数纪律：仅使用已白名单的标签（strategy/role/name）。

