# ADR-0021：统一网络事件总线（HTTP + WebSocket + Worker）

日期：2025-09-14

## 决策

- 以破坏性不向后兼容方式，将网络监听从“仅 HTTP 响应”升级为“统一网络事件总线”。
- 在 Core 中引入 `INetworkEvent`、`NetworkEventKind`、`NetworkDirection`，并将 `INetworkMonitor.Response` 改为 `INetworkMonitor.Event`（事件负载为 `INetworkEvent`）。
- 将聚合器接口从 `OnResponse(INetworkResponse, string? body, IEndpointClassifier)` 改为 `OnEvent(INetworkEvent, IEndpointClassifier)`，支持 HTTP/WS/Worker 的统一分类、去重与窗口化聚合。
- `IEndpointClassifier` 扩展为 `Classify(NetworkEventKind kind, string url, int? status, string? payload, NetworkDirection? direction)`，要求输出低基数稳定端点键；未知返回 `null`（聚合记为 `unknown`）。
- Playwright 适配器扩展：在 `PlaywrightNetworkMonitor` 中同时转发 `IPage.Response` 与 `IPage.WebSocket` 的帧事件（收/发），分别映射为 `HttpResponse` 与 `WebSocketFrame`。

## 背景与动机

- 现状仅覆盖 HTTP，无法对现代前端以 WS/Worker 传输的数据实现“被动网络监听唯一权威”的目标。
- 多事件接口会带来长期维护成本与心智负担，不利于度量与治理统一。
- 统一事件总线可显著简化可观测与策略实现：同一聚合/分类/告警通道，标签低基数治理更可控。

## 方案

1. Core 抽象
   - 新增 `INetworkEvent`（Kind/Url/Status/Direction/Payload/Http/TimestampUtc）。
   - `INetworkMonitor` 改用 `event Action<INetworkEvent> Event`。
   - `IEndpointClassifier` 接受统一事件的关键特征。
   - `ResponseAggregator` 改为 `OnEvent`，去重键统一为 `url+payload` 哈希。
2. 适配器（Playwright）
   - 绑定 `IPage.Response` → 生成 `HttpResponse` 事件（不主动读正文）。
   - 绑定 `IPage.WebSocket`，订阅 `FrameReceived/FrameSent` → 生成 `WebSocketFrame`（文本帧优先，二进制转 Base64 截断）。
3. 应用层（UAM）
   - 仅对 `HttpResponse` 执行业务解析；非 HTTP 事件直接进入聚合以形成可观测。
   - 读取 HTTP 正文后，以 `FilledHttpEvent` 形式送入聚合，保证窗口内去重与分类一致。

## 兼容性

- 破坏性变更：删除/替换原有仅 HTTP 的事件与聚合接口；需要同步更新调用点与测试。
- Endpoint 分类器签名变化：应用层已提供默认实现（仅基于 URL 模式），WS/Worker 暂记为 `unknown`。

## 可观测与指标

- 继续使用 `net_aggregated_total{endpoint,status,kind}` 与 `net_window_items{endpoint}`，新增 `kind` 标签以区分 HTTP/WS。
- 方向可作为可选标签 `direction`（仅 WS/Worker）。

## 安全与反检测

- 统一事件总线“只读被动监听”，拒绝主动注入与干预；正文读取在 UAM 中进行且可观测。
- 对二进制帧进行截断与 Base64 转码，避免高基数或敏感风险。

## 回滚方案

- 如出现严重不兼容，可将 `INetworkMonitor.Event` 的负载临时仅承载 HTTP，并在应用层屏蔽 WS 分支；聚合器仍可正常工作。

