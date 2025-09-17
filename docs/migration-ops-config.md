# 配置迁移说明（XHS → Ops）

发布时间：2025-09-12

## 变更概述
- 根节从 `XHS` 改为 `Ops`，不向后兼容；发布当期提供一次性迁移提示与日志告警，下一版本移除兼容。

## 环境变量映射示例
- `XHS__Logging__Overrides__HushOps.Services.UniversalApiMonitor=Debug`
  → `Ops__Logging__Overrides__HushOps.Services.UniversalApiMonitor=Debug`
- `XHS__BrowserSettings__Headless=true`
  → `Ops__Automation__Browser__Headless=true`
- `XHS__InteractionCache__TtlMinutes=5`
  → `Ops__Humanize__InteractionCache__TtlMinutes=5`
- `XHS__EndpointRetry__MaxRetries=2`
  → `Ops__Automation__Retry__MaxRetries=2`
- 平台专属：`XHS__SearchTimeouts__NoteList=8000`
  → `Ops__Platforms__XHS__SearchTimeouts__NoteList=8000`

## 回滚指引
- 将环境变量前缀恢复为 `XHS__` 并回退到重构前版本 tag。
