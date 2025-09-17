# 指标（InProcess 聚合方案）

本项目通过 Core 层的 `IMetrics` 抽象统一记录关键指标，默认实现为 `InProcessMetrics`：

- **无外部依赖**：仅依赖 .NET 自带的 `System.Diagnostics.Metrics`，彻底移除 Prometheus/Otel。
- **标签白名单**：默认允许 `endpoint/status/hint/accounttier/region/stage/type/path/role/name/personaid/strategy`，可通过 `XHS:Metrics:AllowedLabels` 覆盖。
- **最低安全基线**：指标仅在进程内聚合，必要时可配合 JSON 审计快照持久化，无需额外安全栈。

## 配置项

| 键 | 说明 | 默认值 |
| --- | --- | --- |
| `XHS:Metrics:Enabled` | 是否启用指标聚合 | `true` |
| `XHS:Metrics:MeterName` | Meter 名称（便于区分环境） | `XHS.Metrics` |
| `XHS:Metrics:AllowedLabels` | 允许的标签集合（逗号分隔，小写） | 内置白名单 |

环境变量示例：

```bash
export XHS__Metrics__Enabled=true
export XHS__Metrics__MeterName=XHS.Metrics.Dev
export XHS__Metrics__AllowedLabels=endpoint,status,hint
```

> 建议保持低基数标签，避免影响内存占用与审计可读性。

## 核心度量

| 度量 | 类型 | 标签 | 描述 |
| --- | --- | --- | --- |
| `uam_total_responses` | Counter | endpoint,status | UniversalApiMonitor 监听到的响应数量 |
| `uam_success_2xx` | Counter | endpoint,status | 2xx 成功响应计数 |
| `uam_http_429` | Counter | endpoint | 429 命中次数（反检测失败信号） |
| `uam_http_403` | Counter | endpoint | 403 命中次数（账号/指纹风险） |
| `uam_process_duration_ms` | Histogram | endpoint | API 处理耗时（毫秒） |
| `human.delay.ms` | Histogram | strategy | 拟人化动作延迟分布 |
| `mcp_tool_rate_wait.ms` | Histogram | name | MCP 工具限流等待耗时 |
| `mcp_tool_circuit_open.total` | Counter | name | 熔断打开次数 |

## 与 JSON 审计联动

- 建议结合 `JsonLocalStore` 将关键指标快照写入 `storage/metrics/`，形成日级可审计记录。
- 可在定时任务中读取 `InProcessMetrics` 累积数据，序列化为 JSON（含校验和）并归档到 `evidence/`。

## 关闭指标

若需排查极端性能问题，可通过以下方式暂时关闭：

```bash
export XHS__Metrics__Enabled=false
```

关闭后，系统会自动注册 `NoopMetrics`，业务逻辑无需修改也不会抛出异常。
