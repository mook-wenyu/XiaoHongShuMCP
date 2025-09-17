# 反检测 MCP 工具使用说明（GetAntiDetectionSnapshot）

本工具用于采集浏览器脚本环境的指纹快照，并形成“审计+白名单校验”的闭环：

- 采集内容：UA、语言/时区、平台、硬件并发、WebGL Vendor/Renderer、Canvas 指纹片段等。
- 审计写盘：默认写入到 `.audit/antidetect-snapshot-*.json`（可通过 `XHS:AntiDetection:AuditDirectory` 或工具参数覆盖）。
- 白名单校验：传入一个 JSON 规则文件，对快照进行校验并给出违规项列表；用于灰度放量与告警治理。

> 注意：反检测“策略注入”在浏览器上下文创建时由 `IPlaywrightAntiDetectionPipeline.ApplyAsync` 执行；本工具不负责修改策略，仅负责采集和校验。

## 工具签名

- 名称：`GetAntiDetectionSnapshot`
- 参数：
  - `writeAudit: bool`（默认 `true`）是否写入审计目录
  - `auditDirectory: string?`（默认读取 `XHS:AntiDetection:AuditDirectory` 或 `.audit`）审计目录覆盖
  - `whitelistPath: string?`（可选）白名单 JSON 相对路径；提供则进行校验

## 白名单 JSON 结构（简化版）

```json
{
  "AllowedPlatforms": ["Win32", "MacIntel"],
  "AllowedTimeZones": ["Asia/Shanghai", "America/Los_Angeles"],
  "AllowedWebdrivers": [false],
  "UserAgentMustContain": ["Mozilla/5.0"],
  "UserAgentMustNotContain": ["HeadlessChrome"],
  "WebglVendors": ["Google Inc."],
  "WebglRendererRegex": ["ANGLE\\s*\\(.*\\)"],
  "LanguagesPrefixAny": ["zh-CN", "en-US"]
}
```

含义说明：
- `AllowedWebdrivers`：默认要求 `false`，除非白名单显式允许 `true`。
- `UserAgentMustContain` / `UserAgentMustNotContain`：UA 必须包含/不得包含的子串。
- `WebglRendererRegex`：允许的 Renderer 正则表达式（任意一个匹配即可）。
- `LanguagesPrefixAny`：`languages[0]` 需以其中任意前缀开头。

## 返回结果 AntiDetectionSnapshotMcp

```jsonc
{
  "snapshot": { /* AntiDetectionSnapshot */ },
  "success": true,      // 白名单校验通过或未启用校验
  "message": "✅ 指纹快照采集成功",
  "auditPath": ".audit/antidetect-snapshot-20250913....json",  // 若 writeAudit=true
  "violations": []      // 违规项列表（如 UA_MUST_NOT_CONTAIN:HeadlessChrome）
}
```

## 推荐用法

1. 在接入或升级反检测策略前后，执行 `GetAntiDetectionSnapshot`，对比审计快照并校验白名单。
2. 若 `violations` 非空，建议降低放量或切换画像/代理，直至通过。
3. 将 `.audit/*` 作为只读留存，便于回溯策略变更与站点对抗迭代。

## 安全与合规

- 审计快照中不记录 Cookie/Token/个人信息。
- 建议配合 `.gitignore` 忽略 `.audit/` 输出，避免误入版本库。

