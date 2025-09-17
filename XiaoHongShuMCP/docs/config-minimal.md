# 配置极简白名单（仅 XHS__ 前缀）

系统仅接受以下环境变量；其余键忽略并在 `.audit` 记录：

- `XHS__BrowserSettings__Headless`（bool，默认 true）
- `XHS__BrowserSettings__UserDataDir`（string）
- `XHS__BrowserSettings__Locale`（string，默认 zh-CN）
- `XHS__BrowserSettings__TimezoneId`（string，默认 Asia/Shanghai）
- `XHS__AntiDetection__Enabled`（bool，默认 true）
- `XHS__AntiDetection__AuditDirectory`（string，默认 .audit）
- `XHS__AntiDetection__EnableJsInjectionFallback`（bool，默认 false）
- `XHS__AntiDetection__EnableJsReadEval`（bool，默认 false）
- `XHS__AntiDetection__PatchNavigatorWebdriver`（bool，默认 false）
- `XHS__Metrics__Enabled`（bool，默认 true）
- `XHS__Metrics__MeterName`（string，默认 XHS.Metrics）
- `XHS__Metrics__AllowedLabels`（string，逗号分隔，默认内置白名单）
- `XHS__Metrics__UnknownRatioThreshold`（double，默认 0.30）
- `XHS__InteractionPolicy__EnableJsInjectionFallback`（bool，默认 false）
- `XHS__InteractionPolicy__EnableJsReadEval`（bool，默认 false）
- `XHS__InteractionPolicy__EnableHtmlSampleAudit`（bool，默认 false）
- `XHS__InteractionPolicy__EvalAllowedPaths`（string，逗号分隔白名单标签）
- `XHS__InteractionPolicy__PacingMultiplier`（double，默认 1.0）
说明：自 M2 起，任何“UI 注入兜底”（如 dispatchEvent('click')）均需同时满足以下条件才会生效：
- 业务交互策略允许：`XHS__InteractionPolicy__EnableJsInjectionFallback=true`
- 反检测管线放行：`XHS__AntiDetection__EnableJsInjectionFallback=true`
并且所有注入均通过 `IPlaywrightAntiDetectionPipeline.TryUiInjectionAsync` 受控执行并产生日志审计（.audit 目录）。
