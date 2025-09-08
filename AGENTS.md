# Repository Guidelines

## 核心约束（Critical Constraints）
- 必须使用中文回复，先获取上下文（阅读 README、项目结构、相关代码）。
- 严禁生成恶意/破坏性代码；仅修改与任务直接相关的文件。
- 在执行工具前给出简短前置说明；变更通过 `apply_patch` 完成，避免手动编辑失控。
- 输出前自检：正确性、可读性、最小变更、无泄露机密。

## 标准工作流（Research → Plan → Implement → Verify → Deliver）
- 研究：用 `rg`/`Select-String` 搜索仓库，读取关键文件（≤250 行分块）。
- 计划：用 `update_plan` 维护步骤，始终保持仅一个 `in_progress`。
- 实施：编写最小可行修改；批量相关操作按逻辑分组提交。
- 验证：本地运行构建/测试；检查日志与功能边界。
- 交付：总结变更、给出下一步建议；必要时记录关键决策。

示例（计划与前置说明）
```
“我将先检索服务与测试，然后补充实现。”
update_plan: [ {step: 探索代码, in_progress}, {step: 实现修复, pending} ]
```

## 工具优先级与用法
- 搜索/阅读：`rg -n "keyword"`、`Get-Content -TotalCount 250 <path>`。
- 终端执行：`shell`；Windows 环境默认 PowerShell。
- 代码修改：`apply_patch`（一次性补丁、分块小步提交）。
- 计划同步：`update_plan`（长任务、存在依赖或需要校验时必用）。
- 图片查看：`view_image`（仅当问题涉及图像时）。
- 网络访问：仅在必要时，优先本地上下文；涉及外部资料先说明来源与目的。

## 编码与质量标准
- 工程：SOLID、DRY、关注点分离；清晰命名，避免一字母变量；保留现有风格。
- 安全：不写入用户路径及破坏性命令（如 `rm -rf`）除非用户明确要求。
- 可测试性：优先添加或运行相邻测试；.NET 项目示例：`dotnet test Tests`。
- 性能与鲁棒：考虑复杂度、IO、异常处理与日志（Serilog/Console）。

## 提交与变更说明（如需）
- 信息量小而聚焦；采用 Conventional Commits：`feat: …`、`fix: …`、`test: …`、`docs: …`。
- PR/变更说明包含：动机、范围、影响面、验证方式与结果。

## 语言/领域模式（Sub‑roles）
- C#/.NET：遵循项目现有结构（`Services/`、`Tools/`、测试命名 `*Tests.cs`）。
- JS/TS、Python 等：套用等价最佳实践与测试策略；仅在确有需要时引入依赖。

## 执行守则（Enforcement）
- 触发器：会话开始→检查清单；工具调用前→前置说明；回复前→质量自检。
- 自我改进：成功→沉淀决策与经验；失败→更新流程并标注风险与回避策略。

## Project Structure & Modules
- `XiaoHongShuMCP/`: .NET 9 console app and MCP server entry (`Program.cs`).
- `XiaoHongShuMCP/Services/`: core services including:
  - `UniversalApiMonitor.cs`: multi-endpoint API monitoring system
  - `SmartCollectionController.cs`: intelligent collection controller (refactored)
  - `FeedApiConverter.cs`, `FeedApiModels.cs`: Feed API data processing
  - `SearchTimeoutsConfig.cs`: search wait and convergence timeouts
  - Browser, selectors, search, humanized interaction services
- `XiaoHongShuMCP/Tools/`: MCP tool definitions (`XiaoHongShuTools.cs`).
- `Tests/`: NUnit tests (~57 tests) covering `Services/`, `Models/`, `Tools/`.
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
Playwright (first run): `pwsh ./XiaoHongShuMCP/bin/Debug/net9.0/playwright.ps1 install` after build.

## Coding Style & Naming
- C#: 4-space indent; `nullable enable`; favor async/await over blocking calls.
- Naming: `PascalCase` for types/methods; `camelCase` for locals/params; interfaces prefixed `I` (e.g., `IAccountManager`).
- Organization: keep service contracts in `Interfaces.cs`; implementations under `Services/` subfolders.
- Logging: use Serilog; avoid logging secrets. Prefer structured logs.

## Testing Guidelines
- Frameworks: NUnit + Moq (+ Playwright where appropriate). Target `net9.0`.
- Run all: `dotnet test Tests`
- Coverage: `dotnet test Tests --collect:"XPlat Code Coverage"`
- Naming: files end with `*Tests.cs`; methods describe behavior (e.g., `Search_returns_results_for_valid_keyword`).
- Tests must be deterministic; mock external calls and avoid hitting real XiaoHongShu endpoints.

## Commit & PR Guidelines
- Commits: use Conventional Commits (e.g., `feat:`, `fix:`, `docs:`, `test:`, `refactor:`, `chore:`). Write imperative, concise messages.
- PRs: include summary, linked issue, scope of changes, test coverage notes, and any relevant logs/output. Keep changes focused.

## Security & Configuration Tips
- Do not commit credentials; pass secrets via environment variables (prefix `XHS__`) or CLI args.
- Example env overrides: `XHS__Serilog__MinimumLevel=Debug`, `XHS__BrowserSettings__Headless=true`.
- Browser debugging: launch Chrome/Edge with `--remote-debugging-port=9222` before running the server.
- Environment: set `DOTNET_ENVIRONMENT=Development` when iterating locally.
