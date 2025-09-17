# ADR: 收紧只读 Evaluate 白名单（outerHTML → html.sample，page.evaluate → page.eval.read）

日期：2025-09-15

## 背景
早期为便捷诊断，默认白名单包含 `page.evaluate` 与 `element.outerHTML`。随着反检测强化，需减少高基数/高敏感度的只读脚本：
- 通用 `page.evaluate` 标签无法区分用途；
- 完整 `outerHTML` 采集存在高基数与潜在敏感信息风险。

## 决策
1. 将默认白名单中的通用标签替换为细化标签：
   - `page.evaluate` → `page.eval.read`
2. 将 `element.outerHTML` 改为采样标签 `element.html.sample`，且默认白名单不再包含此标签，仅在审计场景通过环境与白名单显式启用：
   - `XHS__InteractionPolicy__EnableHtmlSampleAudit=true`
   - `XHS__InteractionPolicy__EvalAllowedPaths` 包含 `element.html.sample`
3. DOM 预检器默认不再获取 outerHTML；仅在开启审计开关时采样，并进行脱敏与 KB 级截断。

## 影响
- 破坏性变更：默认白名单减少；任何依赖通用标签/完整 outerHTML 的代码需迁移。
- Playwright 适配层已将 Page 级 Evaluate 标签切换为 `page.eval.read`；outerHTML 采集改为 `element.html.sample` 且默认关闭。
- 构建脚本与静态白名单测试已同步更新；允许在特定文件中使用审计专用标签。

## 测试
- 调整 `EvalGuardWhitelistTests` 与 `PlaywrightAntiDetectionSnapshotTests`；
- 新增 per-tool 差异与策略补丁生成相关测试。

## 配置示例
```
XHS:InteractionPolicy:EvalWhitelist:Paths=element.computedStyle,element.textProbe,element.probeVisibility,element.clickability,element.tagName,page.eval.read,antidetect.snapshot
# 如需启用 outerHTML 采样审计：
XHS__InteractionPolicy__EnableHtmlSampleAudit=true
XHS:InteractionPolicy:EvalWhitelist:Paths=...,element.html.sample
```

