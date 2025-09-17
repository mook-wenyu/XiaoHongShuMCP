# 架构与边界（方案B：零业务注入 + 画像一致性 + 新 Headless）

- 业务层零注入：任何 JS 注入仅允许在 Core 的 `IPlaywrightAntiDetectionPipeline` 中出于反检测目的执行，且默认关闭仅读 Read‑Eval；
- 画像一致性：Locale/Timezone/UserAgent 等在 `BrowserContext` 层集中设定；
- 被动网络监听：HTTP + WS + Worker 统一聚合，`EndpointClassifier` 提供 URL→正文体征→WS 语义 三段回退；
- 拟人化交互：最小 jerk 轨迹 + 节律倍率（Pacing）；
- 可观测：InProcessMetrics 本地聚合 + 低基数标签；Serilog 结构化日志；`.audit` 落盘审计；
- 破坏式不兼容：仅接受 `XHS__*` 环境变量白名单，删除冗余配置与旧指标命名。

目录：

- `HushOps.Core`：反检测、浏览器适配、网络聚合、轨迹、人机节律、配置、可观测、弹性策略；
- `XiaoHongShuMCP`：MCP（集成 CLI 入口）；
- （无 UI）MCP 不包含桌面界面；如需可视化，请外接独立面板或使用 OTLP + 可观测平台。

关键指标（InProcess）：

- `human.trajectory.duration.ms` Histogram；`human.trajectory.steps`，`human.hotspot.pauses`；
- `anti_detect.snapshot.count`，`ui.injection.count{type}`；
- `net.aggregate.count{endpoint,kind,status_class}`，`net.window.items{endpoint}`；
- `net.classify.unknown.ratio` ObservableGauge（滑动窗口）。

安全与合规：

- CSP 与最小权限：默认禁用注入；启用仅在管线内并记录审计；
- 日志脱敏：令牌/会话信息不可落盘；
- 新 Headless 与 UA‑CH 一致性治理。
