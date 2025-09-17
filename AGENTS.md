--- project-doc ---

# Repository Guidelines

## Project Structure & Modules
- `HushOps/`: .NET 8 console app and MCP server entry (`Program.cs`).
- `HushOps/Services/`: core services including:
  - `UniversalApiMonitor.cs`: multi-endpoint API monitoring system
  - `FeedApiConverter.cs`, `FeedApiModels.cs`: Feed API data processing
  - `XhsSettings.cs`: 单一配置聚合类（仅注册这一项）
  - Browser, selectors, search, humanized interaction services
- `HushOps/Tools/`: MCP tool definitions (`XiaoHongShuTools.cs`).
- `Tests/`: NUnit tests (~67+ tests) covering `Services/`, `Models/`, `Tools/`.
- 配置：采用代码内默认配置（根节 `XHS`）；仅注册一个配置类 `XhsSettings`；使用环境变量（双下划线映射冒号）或命令行参数覆盖。日志输出到 `HushOps/logs/`（Serilog）。
  - 支持“命名空间级覆盖”：`XHS:Logging:Overrides:<Namespace>=<Level>`；示例 env `XHS__Logging__Overrides__HushOps.Services.UniversalApiMonitor=Debug`。

## Build, Test, Run
```bash
dotnet restore                      # restore packages
dotnet build                        # build solution
dotnet test Tests                   # run NUnit tests
dotnet run --project XiaoHongShuMCP # start MCP server (Dev)
dotnet publish -c Release           # produce release build
```
Playwright (first run): `pwsh ./XiaoHongShuMCP/bin/Debug/net8.0/playwright.ps1 install` after build.

## Coding Style & Naming
- C#: 4-space indent; `nullable enable`; favor async/await over blocking calls.
- Naming: `PascalCase` for types/methods; `camelCase` for locals/params; interfaces prefixed `I` (e.g., `IAccountManager`).
- Organization: keep service contracts in `Interfaces.cs`; implementations under `Services/` subfolders.
- Logging: use Serilog; avoid logging secrets. Prefer structured logs.

## Testing Guidelines
- Frameworks: NUnit + Moq (+ Playwright where appropriate). Target `net8.0`.
- Run all: `dotnet test Tests`
- Coverage: `dotnet test Tests --collect:"XPlat Code Coverage"`
- Naming: files end with `*Tests.cs`; methods describe behavior (e.g., `Search_returns_results_for_valid_keyword`).
- Tests must be deterministic; mock external calls and avoid hitting real XiaoHongShu endpoints.

## Commit & PR Guidelines
- Commits: use Conventional Commits (e.g., `feat:`, `fix:`, `docs:`, `test:`, `refactor:`, `chore:`). Write imperative, concise messages.
- PRs: include summary, linked issue, scope of changes, test coverage notes, and any relevant logs/output. Keep changes focused.

## Security & Configuration Tips
- Do not commit credentials; pass secrets via environment variables (root section `XHS`, use double underscores to map `:`) or CLI args.
- Example env overrides: `XHS__Serilog__MinimumLevel=Debug`, `XHS__BrowserSettings__Headless=true`, `XHS__InteractionCache__TtlMinutes=5`, `XHS__EndpointRetry__MaxRetries=2`.
- Browser debugging: launch Chrome/Edge with `--remote-debugging-port=9222` before running the server.
- Environment: set `DOTNET_ENVIRONMENT=Development` when iterating locally.

---

## 0. 必读要点（MUST）

- 必写“前置说明”，并在答复末尾附“工具调用简报”（若发生外呼）。
- 退避与降级：429→退避20s；5xx/超时→退避2s且最多一次重试；仍失败→给出保守离线答案+局限。
- 网络只读与合规：不得上传敏感信息；优先官方与权威来源。
- 必须始终使用中文回复；文件必须 UTF‑8（无 BOM）。
- 编码前必须填写“Sequential‑Thinking 分析”；最小化变更边界（只改必要行/文件）。
- 禁止危险命令（如 `rm -rf`）与泄露密钥/令牌/内部链接。

- 中文文档注释与必要细节注释：所有新增/变更代码必须包含中文文档注释与必要的中文行内注释。
- 单元测试为强制项：必须始终编写或更新单元测试覆盖新增/变更逻辑；无测试不合并。
- 设计与风格：必须始终实现 SOLID 原则；严格遵守所用语言的标准代码风格，并启用格式化/静态检查。
 - 完整具体实现：必须提交可运行的完整具体代码；严禁 MVP/最小实现/占位/TODO；发现即当次补齐为完整实现。
 - 多工具协同：必须调用所需 MCP 工具；必须在“工具调用简报”中如实列出本次工具链与因果。
 - 结果验证优化：回复前必须对照“执行前检查清单”逐项自检并确保符合最佳实践。

- 涉及“任务编排/待办清单/模板/里程碑与状态跟踪”的场景，必须选择 `shrimp-task-manager` 。
  - 若运行环境不可用，则降级使用 `update_plan`，并在“工具调用简报”中注明降级原因。

- 变更策略（必须）：正确性优先于兼容性；必要时采取革命性/颠覆性改动；不保持向后兼容；删除过时/冗余的代码、接口与文档；在 PR 中明确迁移方案或“无迁移、直接替换”。

- 记忆加固：首次执行即将本文件关键规则写入 Memory；每次开始任务先读取 Memory 并回显关键约束；如上下文过长导致遗忘，必须回溯本文件并以其为准纠偏。

---

## 1. MCP 选择规则（用哪个）

- SequentialThinking：用于规划/分解/里程碑；仅输出可执行计划，不暴露中间推理。
- Context7：查官方文档/API/版本差异；先 `resolve-library-id` 再 `get-library-docs`；提供 `topic`，`tokens` 默认≤5000；输出需标注库 ID/版本与出处。
- DuckDuckGo：找最新网页/公告/官方链接；用 12 个精准关键词+限定词（`site:`/`filetype:`/`after:YYYY-MM`）；`safesearch=moderate`，`maxResults≤35`，`timeout=5s`，域名去重、剔除内容农场。
- Serena（可选）：跨文档语义检索/总结/规划；指定数据域/路径；`retrieve.top_k=5`，`answer.max_tokens=700`，`citations=true`。
- shrimp-task-manager：当需要“任务编排/待办模板/里程碑追踪/状态看板”时，必须选用；用于将计划落到可执行任务/流水（使用本地目录`.shrimp`，中文模板可用）。
- DeepWiki：GitHub 仓库问答/阅读 Wiki/结构；提供 `owner/repo`；优先“结构→内容→问答”（`read_wiki_structure` → `read_wiki_contents` → `ask_question`）；输出标注仓库与路径。
- MicrosoftDocs：查询 Microsoft Learn 官方文档（.NET/Azure/Windows 等）；优先官方内容；涉及版本差异时必须注明版本与适用范围。
- Fetch：直接抓取指定 URL（网页/PDF）；遵循退避策略（429→20s；5xx/超时→2s 且最多一次重试）；必要时分段抓取并合并摘要。
- Memory：跨轮次持久化关键约束/决定/术语；默认不记录敏感信息；按“最小必要”写入，可按需清除。
 - 降级路径：Context7→DuckDuckGo；DuckDuckGo→保守离线；Sequential/Serena→输出最小可行计划。

### 优先级调用策略
- Microsoft 技术：优先 `microsoft-docs-mcp`。
- GitHub 文档：`context7` → `deepwiki`。
- Web 通用：先 `duckduckgo-search` 获取权威来源，再 `fetch` 定点抓取页面。

---

## 2. 最小工作流（R→P→I→V→D）

- Research：`rg -n` 检索并分块阅读；深度思考/分析/评价/反思；记录约束、未知与风险假设。
- Plan：使用 SequentialThinking 产出仅含可执行步骤的计划（不暴露推理）；`update_plan` 维护步骤与验证标准；涉及编排用 `shrimp-task-manager`（不可用则降级并记录）。
- Implement：`apply_patch` 小步提交；所有代码含中文文档注释/必要行内注释；落实 SOLID 与标准风格；删除过时内容；按“破坏性变更”执行，不保留向后兼容。
 - Verify：必须新增/更新单元测试覆盖变更路径（建议关键路径≈100% 或总体≥80%）；运行构建/静态检查/格式化；评估边界、性能与可维护性；执行代码审查与必要优化。
- Deliver：总结变更/风险/验证结果与迁移指引；若外呼，附“工具调用简报”。

---

## 3. MCP 执行命令（以 .codex/config.toml 为准）

- `sequential-thinking`（stdio）：`npx -y @modelcontextprotocol/server-sequential-thinking`
- `context7`（stdio）：`npx -y @upstash/context7-mcp`
- `memory`（stdio）：`npx -y @modelcontextprotocol/server-memory`
- `shrimp-task-manager`（stdio）：`npx -y mcp-shrimp-task-manager`（`DATA_DIR=.shrimp`，`TEMPLATES_USE=zh`，`ENABLE_GUI=false`）
- `duckduckgo-search`（stdio）：`uvx duckduckgo-mcp-server`
- `fetch`（stdio）：`uvx mcp-server-fetch`
- `serena`（stdio）：`uvx --from git+https://github.com/oraios/serena serena start-mcp-server --context codex`
- `deepwiki`（stdio）：`mcp-proxy --transport streamablehttp https://mcp.deepwiki.com/mcp`
- `microsoft-docs-mcp`（stdio）：`mcp-proxy --transport streamablehttp https://learn.microsoft.com/api/mcp`

---

## 4. 工具调用简报（模板）

```
工具: <SequentialThinking|Context7|DuckDuckGo|Serena|shrimp-task-manager|DeepWiki|MicrosoftDocs|Fetch|Memory>
触发原因: <为何需要该工具>
输入摘要: <关键词/库ID/Topic/查询意图>
参数: <tokens/结果数/时间窗 等>
结果概览: <条数/命中/主要来源域名 或 库ID>
重试/退避: <无|20s|2s + 重试1次>
时间: <UTC 时间戳>
来源: <Context7 的库ID/版本；DuckDuckGo 的来源域名清单；shrimp 的任务模板/清单 ID/路径；DeepWiki 的 repo；MicrosoftDocs 的文档路径/产品版本；Fetch 的原始 URL>
```

---

## 5. 违规处理（Mandatory Enforcement）

- 发现未遵守“最小必要/简报/退避/UTF‑8/中文”等 MUST：立即停止外呼并回到“研究/计划”补齐缺项。
- 回复中显式标注修正动作与理由；必须提供保守离线答案并注明局限与下一步。
- 重复违规：在 PR 增补“回退策略/开关”，必要时拆分任务降低风险。

---

## 6. 执行前检查清单（强制）
- [ ] 中文：回复/注释/文档为中文，UTF‑8（无 BOM）。
- [ ] 上下文：已读取关键文件/Memory 并回显约束。
- [ ] 工具：按“优先级调用策略”选择并记录降级。
- [ ] 安全：遵守只读网络/不泄露敏感/退避策略。
- [ ] 质量：SOLID/风格/测试覆盖/性能与边界。

## 7. 标准工作流（6 步）
1) 分析需求 → 2) 获取上下文 → 3) 选择工具 → 4) 执行任务 → 5) 验证质量 → 6) 存储知识（Memory）。

## 8. 研究-计划-实施模式（强化版）
- 研究：读取/比对文件与资料，形成问题画像，禁止编码。
- 计划：创建“详细任务规划”，仅列可执行步骤（用 SequentialThinking/`update_plan`）。
- 实施：按计划提交完整实现（中文注释/无占位/删除过时）。
- 验证：运行测试与静态检查，补齐边界用例与错误处理。
- 提交：生成迁移说明与“工具调用简报”，必要时提供回滚策略。

## 9. 强制触发器与自我改进
- 强制触发器：会话开始→检查约束；工具调用前→核对流程；回复前→执行“执行前检查清单”。
- 自我改进：成功→写入 Memory；失败→更新规则/补充测试；持续→优化策略与顺序。
