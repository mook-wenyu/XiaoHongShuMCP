# 指标字典（网络事件与交互）

此文档列举与本次“统一网络事件总线”相关的核心指标及标签约束，生产环境通过 OTLP 导出，开发可启用 Prom HttpListener。

## 网络事件聚合（低基数）

- 指标：`net_aggregated_total{endpoint,status,kind}`
  - 含义：被动网络事件聚合计数，窗口内去重后新增条目的计数。
  - 标签：
    - `endpoint`（低基数枚举，如 `Homefeed`/`Feed`/`unknown`）
    - `status`（HTTP 状态码，WS/Worker 取 0）
    - `kind`（`HttpResponse`/`WebSocketFrame`/`WorkerMessage`）
- 指标：`net_window_items{endpoint}`
  - 含义：每次读取窗口快照时的条目数（直方图观测值）。
  - 标签：
    - `endpoint`

说明：方向标签 `direction`（`Inbound`/`Outbound`）可在后续按需加入；为控制基数，本批未默认启用。

## 互动与节律（节选）

- `human_click_attempts_total{path}`：交互点击尝试次数（由 HumanizedInteractionService 记录）。
- `preflight_ready_total{role}`/`preflight_busy_total{role}`/`preflight_disabled_total{role}`：点击前 DOM 预检结果计数。
- `rate_limit_acquired_total{endpoint}`/`rate_limit_wait_ms{endpoint}`：官方限流适配器计数/等待。
- `circuit_open_total{status}`：熔断打开计数（Polly v8 适配器）。

## 告警建议（示例）

- HTTP 风控：`sum by () (net_aggregated_total{kind="HttpResponse",status="429"}) / sum by () (net_aggregated_total{kind="HttpResponse"}) > 3%` 连续 5 分钟。
- WS 噪声：按端点 `unknown` 的 `WebSocketFrame` 比例持续上升（需要追加 `direction` 标签后区分）。
- 处理耗时：UAM 处理时延直方图 p95 > 阈值（参考 `uam_process_duration_ms`）。

