# 设计摘要

| 字段 | 内容 |
| --- | --- |
| 更新时间 | 2025-09-28 |
| 责任人 | Codex |
| 关联提交 | 待提交 |
| 状态 | 进行中 |

## 设计决策

### TASK-20250927-003
1. **浏览器键唯一性**：`BrowserOpenRequest/Result` 扩展键名字段，独立模式使用 `folderName` 作为默认键；同名键指向不同路径时报错，相同路径返回警告并复用缓存。
2. **固定目录策略**：独立模式不再接受 `profilePath`，统一使用 `storage/browser-profiles/<folderName>`。
3. **会话缓存**：`BrowserAutomationService` 使用线程安全字典维护已打开会话，提供查询接口供后续操作使用。

### TASK-20250927-002
1. **浏览器打开模型**：引入 `BrowserProfileKind`、`BrowserOpenRequest/Result`，改为显式请求时打开浏览器。
2. **路径解析策略**：用户模式自动探测常见目录或使用显式路径；独立模式可新建 `storage/browser-profiles/<folderName>`。
3. **工具扩展**：新增 `BrowserTool` 暴露 `xhs_browser_open`，默认使用用户模式。
4. **日志与审计**：所有打开操作记录模式、路径和是否新建，便于审计。

### TASK-20250927-004
1. **键名合并**：`BrowserOpenRequest` 合并 `folderName` 与 `browserKey` 为单一 `profileKey`，独立模式目录固定为 `storage/browser-profiles/<profileKey>`。
2. **用户自动打开**：`IBrowserAutomationService` 暴露 `EnsureProfileAsync`，用户模式缺失时自动尝试打开，结果新增 `AutoOpened` 标记并写入元数据。
3. **工具联动**：`BrowserTool`、`NoteCaptureTool`、`HumanizedActionTool` 在执行前检测/拉起用户浏览器；独立模式保持显式调用约束。
4. **双语描述**：全部 MCP 工具方法、参数以及请求/结果记录添加 `Description("中文 | English")`，保持 Schema 可读性。

### TASK-20250927-005
1. **拟人化策略**：引入 `HumanBehaviorOptions`，通过 `DefaultBehaviorController` 实现动作前后节奏控制，产生日志与元数据。
2. **指纹伪装**：`ProfileFingerprintManager` 生成 UA、视口、触控、语言、时区及额外请求头，并在 Playwright 上下文中应用 Canvas/WebGL 扰动脚本。
3. **网络策略**：`NetworkStrategyManager` 负责代理选择、延迟区间与错误缓解计数，Playwright 路由层注入延迟与 429/403 监控。
4. **会话元数据**：`BrowserSessionMetadata` 记录指纹与网络摘要，方便后续分析与审计。

### TASK-20250927-006
1. **自动拉起封装**：`EnsureProfileAsync` 逻辑仅对 `profileKey=user` 自动探测，调用方依赖 metadata 识别 `autoOpened` 状态；独立模式保持显式要求。
2. **工具参数规范**：所有工具请求体默认携带 `browserKey`，元数据输出包含 `behavior.*`、`fingerprint*`、`network*` 字段及错误信息。
3. **封装入口**：可选地在客户端/服务端暴露“调用前确保 user 浏览器开启”的封装，避免遗漏 `xhs_browser_open`。
4. **审计日志**：日志记录键名、路径、指纹哈希与缓解次数，确保可追溯性，并在文档中说明真实环境验证的缺口与后续计划。
5. **验证配置**：通过 `VerificationOptions` (`verification.statusUrl` / `verification.mockStatusCode`) 允许替换或模拟示例流程使用的状态端点，端点不可达时记录警告而非终止流程。

## 风险与缓解

- **键名冲突**：通过字典检测阻止覆盖，并在文档中说明命名规范。
- **目录探测失败**：保持抛出明确异常，引导用户提供显式路径或目录名。
- **真实验证缺失**：在验证文档中记录缺口，并安排获取测试账号/代理后执行 `--verification-run`。
- **外部端点不可达**：提供可配置的状态端点与可选模拟状态码，若网络受限可指向内网或本地接口，并在运行时捕获异常或直接返回本地模拟响应。
