# 任务基础信息
- 任务 ID：TASK-20250927-006
- 来源：用户新增浏览器配置与拟人化需求
- 更新时间：2025-09-28
- 责任人：Codex
- 关联提交：待定
- 状态：进行中

## 设计目标
1. 统一浏览器会话键 `profileKey`，默认值 `user` 对应用户浏览器配置。
2. 当 `profileKey=user` 且未显式打开时自动尝试探测并拉起浏览器；其他键维持显式流程。
3. 对所有 MCP 工具方法与参数添加 `Description("中文说明 | English description")` 双语注解，固化审计要求。
4. 引入拟人化行为、指纹与网络策略控制，降低反检测风险。
5. 提供可选封装以在调用工具前确保 user 会话已就绪，同时保留手动控制能力。

## 方案概述
- **Profile 管理**：`BrowserAutomationService` 保持 `_openedProfiles` 字典缓存，`EnsureProfileAsync` 仅针对 `user` 自动探测。独立配置通过 `storage/browser-profiles/<profileKey>` 建立目录，禁止自定义路径。
- **冲突与错误处理**：当 `profileKey` 已存在但路径不同，抛出异常阻止覆盖；若会话已打开返回 `already_open` 状态。
- **工具参数**：`BrowserOpenToolRequest`、`NoteCaptureToolRequest`、`HumanizedActionToolRequest` 等统一字段名称和默认值，注解说明 user/isolated 行为差异。
- **拟人化控制**：`HumanizedActionService` 注入 `DefaultBehaviorController` 与网络策略队列，所有动作前后记录 `behavior.pre/post` 元数据。
- **自动拉起封装**：服务端在工具执行前若检测到 `profileKey=user` 缓存缺失则调用 `EnsureProfileAsync`，并在 metadata 标记 `autoOpened=true`。
- **反检测能力**：`ProfileFingerprintManager` 生成 UA、视口、触控等指纹参数；`NetworkStrategyManager` 配置代理、延迟与重试；`PlaywrightSessionManager` 应用额外脚本与网络节流。
- **验证配置**：提供 `VerificationOptions` 配置节，允许通过 `verification.statusUrl` 指定示例流程使用的状态端点。

## 设计决策
- **自动打开限制**：仅对 `user` 模式启用，防止误创建无效独立配置。失败时提示显式调用 `xhs_browser_open`。
- **目录结构**：独立配置固定父目录 `storage/browser-profiles/`，folderName 由 `profileKey` 决定。
- **审计字段**：所有工具返回 metadata 中包含指纹、网络、autoOpen 状态，配合日志记录审计路径。
- **注解规范**：保持“中文 | English”格式，关键术语采用一致翻译（例如 `Browser key`）。

## 风险与缓解
- **缺少真实验证**：记录在验证阶段，待获取测试账号后执行 `VerificationScenarioRunner`。临时缓解：通过日志检查与模拟。
- **浏览器探测失败**：自动打开失败时抛出清晰异常，提示提供 `profilePath`。
- **性能开销**：网络延迟与拟人化延迟可能影响效率，可在配置中调整范围，并记录指标供评估。
- **文档覆盖**：需同步更新 `docs/requirements|design|tasks|implementation|testing|coding-log|index|changelog`，避免遗漏。
- **外部端点不可达**：若默认的 httpbin 端点被阻断，可通过 `verification.statusUrl` 指向内网或本地服务，Runner 会在端点不可达时记录警告而非终止。

## 待评审事项
- 是否需要在自动封装中提供超时/重试选项。
- 需要确认 Playwright 环境部署方案与代理资源，便于执行验证脚本。
