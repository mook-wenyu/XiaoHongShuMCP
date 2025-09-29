# 设计摘要

| 字段 | 内容 |
| --- | --- |
| 更新时间 | 2025-09-28 |
| 责任人 | Codex |
| 关联提交 | 待提交 |
| 状态 | 进行中 |

## 设计决策

### TASK-20250928-001
1. 收集所有笔记指标列并按首次出现的顺序去重，写入 CSV 表头。
2. 写入每条记录时按上述表头顺序查找字典，缺失项输出空字符串，保证列数一致。
3. 保持 `includeAnalytics` 现状不变，仅修复顺序并在交付中提示潜在列顺序变化。

### TASK-20250928-002
1. 继续使用 `AddSimpleConsole`，通过配置 `ConsoleLoggerOptions.LogToStandardErrorThreshold=Trace` 将日志全部重定向到 stderr。
2. 指导原则：stdout 专用于 MCP JSON；所有日志打印必须走 Logger，以便遵守 stderr 要求。
3. 若未来新增日志提供器，需评审其输出目标，防止回归。

### TASK-20250928-003
1. 删除账号配置模型，服务器启动流程仅依赖浏览器 profile，运行日志提示用户手工登录。
2. 默认关键词策略改为仅依赖配置或请求参数；无账号回退逻辑，缺失时抛出明确异常。
3. Playwright 会话禁用 `storage-state.json` 复用，实现“服务层不持久化 Cookie”；保留浏览器配置目录复用以平衡体验。
4. MCP 工具描述、README 与日志同步指引手工登录操作与安全注意事项。

### TASK-20250928-004
1. 采用“自定义 `ILoggerProvider` + `LoggingCapability`”方案构建 MCP 日志桥接，统一管理级别、敏感字段过滤与节流策略。citeturn5search0turn6search0
2. 在 `Program.cs` 中声明 `ServerCapabilities.Logging`、配置支持级别（debug/info/warn/error）并保持默认静默，等待客户端 `logging/setLevel` 才开启推送。citeturn4search0turn4search1
3. `logging/setLevel` 更新自定义状态后驱动 `McpLoggingDispatcher`，Channel 堵塞时回退至 stderr 并输出降级告警。citeturn4search2turn6search0
4. 验证阶段需通过端到端脚本模拟级别切换，确保通知 payload 与节流策略满足规范。citeturn4search2turn6search0
5. 通过 `IMcpLoggingNotificationSender` 适配层隔离 MCP 通知发送逻辑，便于在测试环境注入替身并扩展到多传输场景。

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

### TASK-20250928-005
1. 采用内存传输集成测试构建 MCP 日志端到端验证，覆盖初始化能力声明、`logging/setLevel` 请求及通知推送。citeturn0search1turn0search6
2. 确保服务器初始化响应默认包含 `ServerCapabilities.Logging`，并在测试中断言该字段存在。citeturn0search1
3. 测试中复用现有脱敏与阈值逻辑，校验敏感信息掩码和级别生效路径。

### TASK-20250928-006
1. 默认在运行时检测浏览器是否存在，缺失时自动调用 `Microsoft.Playwright.Program.Main(new[]{"install"})` 下载，并输出执行日志；默认不附带离线浏览器包。citeturn1search0
2. 保留跨平台安装脚本封装 `playwright.ps1 install`，供 CI/CD 显式预装并配置共享缓存；脚本默认不触发构建，需维护者在具备 .NET SDK 的环境下显式加开关才能生成 `playwright.ps1`。citeturn1search0turn1search7
3. 若自动安装失败，返回含排查指引的异常，并指向脚本执行或代理配置作为回退策略；脱机安装由使用方自备浏览器镜像或在联网环境预装。

### TASK-20250928-007
1. 构建公共 `HumanizedInteraction` 模块，统一动作模型（hover/click/double click/context click/drag/wheel/scroll/input/hotkey/idle 等）与定位结构（role、text、label、placeholder、altText、title、testId 单值），供所有工具复用。citehttps://playwright.dev/docs/best-practices
2. 引入随机化策略：曲线鼠标路径、点击落点抖动、滚动步长变化、键入延迟及错字修正、Idle 停顿，以及可配置行为档案（速度区间、误操作概率、随机种子）。citehttps://docs.agentql.com/avoiding-bot-detection/user-like-behaviorhttps://medium.com/@domadiyamanan/preventing-playwright-bot-detection-with-random-mouse-movements-10ab7c710d2a
3. 重构 HumanizedActionTool、NoteCaptureTool 等，使其仅负责生成动作脚本，执行由公共服务处理，并输出详尽日志与回退机制；未来其它工具可逐步接入。
4. 通过 `DefaultHumanizedActionScriptBuilder` 生成脚本，`HumanizedInteractionExecutor` 负责顺序执行；服务层统一在获取 Playwright 页面后执行脚本并回写元数据。
5. 扩展 `SessionConsistencyInspector` 采集代理、GPU、硬件并发、网络连通性与自动化指示器；`HumanBehaviorProfileOptions` 增加视窗容差、代理前缀、GPU 信息与自动化容忍配置，并输出结构化一致性报告供日志与指标使用。
6. 工具层重构：`HumanizedActionTool` 采用“准备脚本 + 执行”模式返回脚本摘要；`NoteCaptureTool` 在采集前触发拟人化导航，产生筛选摘要与行为档案输出，并允许跳过导航作为回退。

## 风险与缓解

- **键名冲突**：通过字典检测阻止覆盖，并在文档中说明命名规范。
- **目录探测失败**：保持抛出明确异常，引导用户提供显式路径或目录名。
- **真实验证缺失**：在验证文档中记录缺口，并安排获取测试账号/代理后执行 `--verification-run`。
- **外部端点不可达**：提供可配置的状态端点与可选模拟状态码，若网络受限可指向内网或本地接口，并在运行时捕获异常或直接返回本地模拟响应。
### TASK-20250929-008
- 服务层：HumanizedActionService 在 Prepare/Execute 流程中分别生成计划动作与执行动作列表，写入统一的元数据键。
- 工具层：HumanizedActionTool、NoteCaptureTool 扩展返回模型，新增计划动作、执行动作、执行标记等字段，保持 HumanizedActions 向后兼容。
- 数据结构：NoteCaptureToolResult 增加 PlannedActions、ExecutedActions、NavigationExecuted，配套调整测试和序列化逻辑。
