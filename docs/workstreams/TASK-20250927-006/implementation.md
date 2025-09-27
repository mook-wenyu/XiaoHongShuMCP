# 任务基础信息
- 任务 ID：TASK-20250927-006
- 来源：用户新增浏览器配置与拟人化需求
- 更新时间：2025-09-28
- 责任人：Codex
- 关联提交：待定
- 状态：进行中

## 变更摘要
- 统一浏览器 `profileKey` 流程，自动打开仅对 `user` 生效，冲突时抛出异常。
- 新增 `_openedProfiles` 缓存与自动拉起逻辑，返回 metadata 标记 `autoOpened`。
- 引入 `ProfileFingerprintManager`、`NetworkStrategyManager`、`PlaywrightSessionManager`，构建拟人化指纹与网络策略。
- 为 `BrowserTool`、`NoteCaptureTool`、`HumanizedActionTool` 及其请求/响应模型添加中英文 `Description` 注解。
- 修复 `Program.cs` 中 `CancellationToken` 命名空间问题，保证 Release 构建。
- 新增 `VerificationOptions`（`verification.statusUrl` / `verification.mockStatusCode`）配置绑定，在 `Program.cs` 注册，并在 `VerificationScenarioRunner` 中注入；当状态端点不可达时自动路由并返回指定状态码，避免 CLI 失败且仍可统计缓解次数。

## 关键文件
- `Services/Browser/BrowserAutomationService.cs`
- `Services/Browser/BrowserOpenModels.cs`
- `Services/Browser/Playwright/PlaywrightSessionManager.cs`
- `Services/Browser/Fingerprint/ProfileFingerprintManager.cs`
- `Services/Browser/Network/NetworkStrategyManager.cs`
- `Services/Humanization/HumanizedActionService.cs`
- `Tools/BrowserTool.cs`
- `Tools/NoteCaptureTool.cs`
- `Tools/HumanizedActionTool.cs`
- `Program.cs`

## 实施细节
- 自动打开：`EnsureProfileAsync` 检查 `_openedProfiles`，未命中且 profileKey=user 时创建用户配置并缓存，记录日志。
- 独立配置：`BrowserOpenRequest.UseIsolatedProfile` 固定目录 `storage/browser-profiles/<profileKey>`，禁止自定义路径。
- 指纹与网络：在 `OpenAsync` 中生成指纹与网络上下文，注入 Playwright 上下文的 user agent、viewport、代理、Canvas/WebGL 随机化及网络延迟。
- 人性化控制：动作执行前后调用 `DefaultBehaviorController`，拼装 metadata，供审计与后续调优。
- 双语注解：统一格式“中文说明 | English description”，确保工具接口清晰可读。
- CLI 修复：使用完全限定名引用 `System.Threading.CancellationToken.None`。

## 未完成项
- 未在真实浏览器环境验证 auto-open、指纹扰动、网络缓解计数。
- 需在后续任务补充更多反检测策略（字体、WebRTC、AudioContext 等）。
- `verification.statusUrl` 默认仍指向 https://httpbin.org/status/429，需在受限网络环境中提供替代端点并记录缓解指标。
