# ADR-0007（已归档）：可观测解耦（IMetrics）与三分架构扩展（Observability 工程）

日期：2025-09-13

## 背景

历史上 `UniversalApiMonitor` 直接使用 `System.Diagnostics.Metrics`（OTel 绑定），在 Host 内部配置导出器。这导致：

- 业务层对 OTel 概念有隐式耦合（`Meter`/`TagList`），抽象边界不清晰；
- 指标拨盘分散（`XHS:UniversalApiMonitor:*` 与 `XHS:Metrics:*` 并存），运维心智负担重；
- 难以对白名单标签、基数治理和导出器切换实施统一策略。

## 决策（更新：2025-09-17 起改为 InProcess 聚合，彻底移除 Prometheus/Otel 依赖）

1. 在 Core 新增 `IMetrics` 抽象（`ICounter`/`IHistogram`/`LabelSet`）。
2. 提供 `InProcessMetrics` 实现与 `AddObservability` 扩展，仅依赖 .NET 内置 `Meter`，无任何外部导出器；历史上的 `otlp/prom` 路线保留为档案，不再参与构建。
3. `UniversalApiMonitor` 构造函数新增依赖 `IMetrics?`，不再直接引用 `Meter`/`TagList`；指标写入统一经由 `IMetrics`。
4. 破坏性删除旧键位：不再读取 `XHS:UniversalApiMonitor:*` 中的指标开关与导出设置，统一改为 `XHS:Metrics:*`。
5. 标签治理：在 `InProcessMetrics` 内实现标签白名单（默认：`endpoint,status,hint,accounttier,region`）。
6. 架构守卫扩展：Observability 不得依赖 `Services` 或 `Adapters`。

## 影响

- 配置迁移（破坏性）：
  - 删除：`XHS:UniversalApiMonitor:EnableMetrics`、`XHS:UniversalApiMonitor:MetricsExporter`、`XHS:UniversalApiMonitor:OtlpEndpoint`、`XHS:UniversalApiMonitor:OtlpProtocol`、`XHS:UniversalApiMonitor:PrometheusPort`、`XHS:Metrics:Exporter`、`XHS:Metrics:Otlp:*`
  - 新增：`XHS:Metrics:Enabled`、`XHS:Metrics:MeterName`、`XHS:Metrics:AllowedLabels`、`XHS:Metrics:UnknownRatioThreshold`
- 代码迁移：业务层改为注入 `IMetrics`（可空）；未注册时为 `NoopMetrics`，无需判空。
- 测试：更新 `UniversalApiMonitorMetricsTests` 以校验有/无 `IMetrics` 注入的行为差异；新增架构守卫覆盖 Observability 依赖边界。

## 取舍与理由

- 选择在 Core 定义抽象、在 Observability 提供实现，保持业务对实现透明，支持后续替换为自研或其他指标后端。
- （历史）Prometheus HttpListener 导出器说明；现阶段不再提供 Collector/Prometheus 相关脚本与指引。

## 回滚策略

- 若 24 小时内指标聚合导致性能问题，可暂时设置 `XHS:Metrics:Enabled=false` 并回退到上一个稳定标签。

## 执行

- 已提交：Core.IMetrics、Observability.OtelMetrics/ServiceCollectionExtensions、UniversalApiMonitor 重构、测试与守卫更新、默认配置与文档迁移。
