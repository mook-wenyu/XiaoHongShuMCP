# 配置最简化指南（XHS 前缀）

本项目已启用“简化环境变量”模式：仅接受前缀 `XHS__` 的极少数关键键；其他配置项全部使用推荐默认值（内置类型默认或运行时推导），无需设置。

## 必要（可选）环境变量白名单

- `XHS__BrowserSettings__Headless`（默认 `false`）
- `XHS__BrowserSettings__UserDataDir`（默认项目根 `UserDataDir/`）
- `XHS__AntiDetection__Enabled`（默认 `true`）
- `XHS__AntiDetection__AuditDirectory`（默认 `.audit`）
- `XHS__AntiDetection__PatchNavigatorWebdriver`（默认 `false`）
- `XHS__Metrics__Enabled`（默认 `true`）
- `XHS__Metrics__MeterName`（默认 `XHS.Metrics`）
- `XHS__Metrics__AllowedLabels`（默认采用内置白名单，逗号分隔）
- `XHS__InteractionPolicy__EnableJsInjectionFallback`（默认 `false`）
- `XHS__InteractionPolicy__EnableJsReadEval`（默认 `false`，安全优先）
- `XHS__InteractionPolicy__EnableHtmlSampleAudit`（默认 `false`）
- `XHS__InteractionPolicy__EvalAllowedPaths`（默认空集合，逗号分隔标签）

> 说明：其余键不需要配置，统一使用代码默认与运行时推导。程序启动日志会打印“Using simplified env”提示。

## 示例（Linux/macOS）

```bash
export XHS__BrowserSettings__UserDataDir=profiles/dev
export XHS__AntiDetection__Enabled=true
export XHS__Metrics__Enabled=true
export XHS__Metrics__AllowedLabels=endpoint,status,hint
export XHS__InteractionPolicy__EnableJsReadEval=false
```

## 推荐默认值说明

- 浏览器：若未设置 `ExecutablePath/Channel`，优先自动探测系统 Chrome/Edge；否则使用 Playwright 内置内核。
- 反检测：默认启用轻量隐藏（`navigator.webdriver` 等）并写入审计快照到 `.audit/`。
- 指标：仅本地 `console` 导出；不包含 OTLP/Prometheus/Grafana 路线。
- 只读 Evaluate：默认禁用；如需排障请临时开启并监控 `ui_injection_total{type=eval}` 指标。
