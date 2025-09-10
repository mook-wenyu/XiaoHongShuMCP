--- project-doc ---

# Repository Guidelines

## Project Structure & Modules
- `XiaoHongShuMCP/`: .NET 8 console app and MCP server entry (`Program.cs`).
- `XiaoHongShuMCP/Services/`: core services including:
  - `UniversalApiMonitor.cs`: multi-endpoint API monitoring system
  - `SmartCollectionController.cs`: intelligent collection controller (refactored)
  - `FeedApiConverter.cs`, `FeedApiModels.cs`: Feed API data processing
  - `SearchTimeoutsConfig.cs`: search wait and convergence timeouts
  - Browser, selectors, search, humanized interaction services
- `XiaoHongShuMCP/Tools/`: MCP tool definitions (`XiaoHongShuTools.cs`).
- `Tests/`: NUnit tests (~69+ tests) covering `Services/`, `Models/`, `Tools/`.
- 配置：采用代码内默认配置；使用环境变量 `XHS__...` 或命令行参数覆盖。日志输出到 `XiaoHongShuMCP/logs/`（Serilog）。
  - 支持“命名空间级覆盖”：`Logging:Overrides:<Namespace>=<Level>`；示例 env `XHS__Logging__Overrides__XiaoHongShuMCP.Services.UniversalApiMonitor=Debug`。

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
- Do not commit credentials; pass secrets via environment variables (prefix `XHS__`) or CLI args.
- Example env overrides: `XHS__Serilog__MinimumLevel=Debug`, `XHS__BrowserSettings__Headless=true`, `XHS__InteractionCache__TtlMinutes=5`.
- Browser debugging: launch Chrome/Edge with `--remote-debugging-port=9222` before running the server.
- Environment: set `DOTNET_ENVIRONMENT=Development` when iterating locally.

---

## 0. 必读要点（MUST）

- 必写“前置说明”，并在答复末尾附“工具调用简报”（若发生外呼）。
- 退避与降级：429→退避20s；5xx/超时→退避2s且最多一次重试；仍失败→给出保守离线答案+局限。
- 网络只读与合规：不得上传敏感信息；优先官方与权威来源。
- 中文沟通；文件必须 UTF‑8（无 BOM）。
- 编码前必须填写“Sequential‑Thinking 分析”；最小化变更边界（只改必要行/文件）。
- 禁止危险命令（如 `rm -rf`）与泄露密钥/令牌/内部链接。

- 涉及“任务编排/待办清单/模板/里程碑与状态跟踪”的场景，必须选择 `shrimp-task-manager` 。

- 变更策略（必须）：正确性优先于兼容性；必要时采取革命性/颠覆性改动；不保持向后兼容；删除过时/冗余的代码、接口与文档；在 PR 中明确迁移方案或“无迁移、直接替换”。

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

---

## 2. 最小工作流（R→P→I→V→D）

- Research：`rg -n` 检索并分块阅读；记录约束与未知。
- Plan：`update_plan` 维护步骤，约定验证标准。
- Implement：`apply_patch` 小步提交，最小变更，中文 Doc 注释。
- Verify：运行构建/测试（若有）；检查边界与性能风险；无破坏性影响。
- Deliver：总结变更/风险/验证结果；若外呼，附“工具调用简报”。

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

