# 需求摘要

| 字段 | 内容 |
| --- | --- |
| 更新时间 | 2025-09-28 |
| 责任人 | Codex |
| 关联提交 | 待提交 |

## 当前任务需求

### TASK-20250928-004
- 评估现有 STDIO MCP 服务器与官方 C# SDK 最新标准的差异，确认能力声明、版本依赖与托管模式的缺口。
- 明确日志能力改造目标：显式声明 `ServerCapabilities.Logging`，实现 `logging/setLevel` 级别控制与基于 `notifications/message` 的结构化日志通道。
- 输出配置、依赖与验证流程的改造路线图，覆盖日志敏感信息过滤、节流策略以及 Inspector/Claude 等主流客户端的联调计划。

### TASK-20250928-005
- 补齐 MCP 日志能力的端到端自动化验证，模拟客户端发送 `logging/setLevel` 后接收 `notifications/message`。
- 确保服务器初始化响应中声明 `logging` 能力，并在测试中断言该字段始终存在。
- 为后续与真实 MCP 客户端联调提供可复用的脚本和验证基线。

### TASK-20250928-006
- 解决 Playwright 浏览器未下载导致的 `Executable doesn't exist at ...\ms-playwright\chromium-1124\chrome.exe` 错误，制定标准安装流程与排查指南。citeturn1search0
- 默认在运行时检测浏览器是否存在，缺失时自动执行 `playwright install`（通过 `Microsoft.Playwright.Program.Main` 调用），输出安装日志并处理失败提示；默认仅下载 Chromium 与 FFMPEG，不随仓库附带离线浏览器包。citeturn1search0
- 同步提供安装脚本与文档指引，便于 CI/CD 场景显式执行安装并配置共享缓存路径；若需脱机安装，由使用方自行准备浏览器镜像或在有网络的环境预装。citeturn1search0turn1search7

### TASK-20250928-007
- 将拟人化浏览交互封装为通用能力，覆盖 hover、click、double click、context click、drag、wheel、scroll、input、hotkey、idle 等动作类型，供所有工具复用。citehttps://playwright.dev/docs/best-practices
- 定义统一定位数据结构（role、text、label、placeholder、altText、title、testId 单值字段），减少重复实现并提升定位稳定性。
- 引入随机化策略（曲线鼠标路径、点击落点抖动、滚动步长变化、键入延迟与错字修正、停顿/观察动作），并与指纹、网络策略协同降低反自动化检测风险。citehttps://docs.agentql.com/avoiding-bot-detection/user-like-behaviorhttps://medium.com/@domadiyamanan/preventing-playwright-bot-detection-with-random-mouse-movements-10ab7c710d2a
- 构建行为档案（速度区间、误操作概率、随机种子等）与日志监测，提供快速回退/开关机制。
- 工具需复用公共能力：`HumanizedActionTool` 输出脚本摘要与解析关键词；`NoteCaptureTool` 集成拟人化导航并返回筛选选择、行为档案等元数据。
- 扩展会话一致性校验覆盖代理配置、GPU 信息、硬件并发、网络连通性与自动化指示器，行为档案需提供视窗容差、代理前缀与自动化容忍等阈值配置，并输出结构化健康报告供监控使用。

### TASK-20250928-001
- 修复 `NoteCaptureService` 导出 CSV 时指标列顺序不稳定的问题，确保所有记录与表头一致。
- 缺失的指标列需输出空字符串，以避免列数不一致。
- 保持 `includeAnalytics` 默认行为不变，后续若需调整再行评审。

### TASK-20250928-002
- 避免 MCP STDIO 输出被日志污染：需将所有框架日志写入 stderr，保证 stdout 仅包含协议 JSON。
- 验证 MCP 客户端端到端连接，确认错误不再出现；若其他组件输出 stdout，应统一整改。

### TASK-20250928-003
- 移除账号配置模型，允许服务器在未配置账号的情况下启动；所有工具依赖通过浏览器手工登录完成授权。
- 调整默认关键词策略，缺省时需要请求/画像提供关键词或显式配置 `defaultKeyword`。
- 禁用 Playwright `storage-state.json` 读取，避免服务层面持久化 Cookie；继续复用浏览器配置目录由用户自行决定。
- 更新文档与工具描述，明确“启动后需手工登录、默认复用浏览器配置目录”。

### TASK-20250927-004
- `profileKey` 统一作为浏览器键与（独立模式）目录名，取消 `folderName` 字段；用户模式仍允许显式 `profilePath`，独立模式禁止额外路径参数。
- 用户模式工具执行前自动检测缓存，缺失时自动探测或使用显式路径打开浏览器；独立模式需显式调用 `xhs_browser_open`。
- 浏览器会话结果新增 `ProfileDirectoryName`、`AutoOpened` 等字段，所有工具元数据需同步返回。
- 所有 MCP 工具方法及其参数需添加 `Description("中文 | English")` 注解，Schema 暴露双语说明。
- 所有工具请求体必须携带 `browserKey`（默认 `user`），以匹配上述缓存与自动打开策略。

### TASK-20250927-005
- 打造可参数化的拟人化行为模型（延迟、鼠标、键盘、滚动等），用于在执行 Playwright 工具前后模拟真实用户节奏。
- 引入浏览器指纹伪装与防探测策略：基于 `profileKey` 绑定用户数据目录、随机化 UA/时区/语言、隐藏 WebDriver、注入 Canvas/WebGL 扰动。
- 构建网络层防护：请求节流、随机失败重试、代理轮换/带宽模拟，减少短时高频特征。
- 统一日志与监控结构，记录行为参数、指纹摘要、网络出口、失败原因，为风控分析与调优提供证据。
- 设计回退策略：检测到风控信号时自动降级操作频率或转人工确认，并在文档中记录风险。

### TASK-20250927-003
- 在独立浏览器模式下，`profilePath` 不再允许传入，使用固定目录 `storage/browser-profiles/<folderName>`；必须提供 `folderName` 并作为缓存键默认值。
- 为打开的浏览器会话维护字典缓存，用户浏览器默认使用键 `user`，独立配置以传入的 `profileKey` 或 `folderName` 作为键；禁止同名键覆盖，不同路径时报错。
- 当请求与已打开会话键与路径完全一致时，返回警告并复用已有结果。
- 工具与服务接口需同步支持新参数（键名、目录名），并返回使用的键、路径、是否复用等信息。

### TASK-20250927-002
- 停止服务器启动阶段的自动浏览器启动，改为通过工具显式打开。
- 支持“用户浏览器配置”与“独立浏览器配置”两种模式，默认使用用户模式，可指定路径或自动发现/创建。
- 工具需返回实际路径、是否新建配置以及是否使用默认路径，文档同步说明。

### TASK-20250927-006
- 保持 `profileKey` 单字段语义，`user` 自动探测并拉起浏览器，其它键必须显式打开，禁止同名键覆盖。
- 将指纹、网络策略与拟人化行为绑定至会话元数据，记录指纹哈希、代理与缓解次数等指标。
- 所有 MCP 工具方法与参数采用 `Description("中文说明 | English description")` 格式，元数据输出需含 `autoOpened` 与行为日志字段。
- 提供“调用前自动拉起或复用 user 浏览器”的封装，仍允许手动调用 `xhs_browser_open`。
- 支持通过 `verification.statusUrl` 配置示例验证使用的状态码端点，网络受限时可替换为内网服务。
- 支持通过 `verification.mockStatusCode` 在本地拦截并返回指定状态码，确保在离线或受限网络中仍能验证缓解逻辑。
- 更新文档、日志与验证流程，标注真实环境缺失的风险与后续计划。
### TASK-20250929-008
- 为 NoteCaptureTool 增加“计划动作”与“实际执行动作”区分，便于输出层审计和回放。
- 扩展 HumanizedActionService，在 Plan/Execute 阶段分别生成动作列表与执行标记。
- 更新工具和响应模型，确保 MCP 客户端能够快速识别导航是否执行以及跳过的步骤。
