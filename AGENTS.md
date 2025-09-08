<!--
文档名称: AGENTS.md（用户级指南）
版本: 1.0.0
最后更新: 2025-09-08
适用范围: Codex CLI 项目与同类工程
作者: 项目维护者
摘要: 本文面向“使用者”（而非框架开发者），给出与智能体协作的标准流程、质量与安全要求，以及可复制的清单与模板。
-->

# AGENTS（用户级指南）

> 目标：帮助你高效、安全地与智能体协作，产出最小而正确的变更，并保持可验证、可追溯与可复用。

---

## 1. 适用对象与前置认知

- 面向角色：使用 Codex CLI 或相似代理工具的“任务执行者/贡献者”。
- 使用边界：严禁生成恶意/破坏性代码；仅修改与任务直接相关的文件；遵守项目既有风格与安全策略。
- 默认约定：
  - 全文与沟通均使用中文。
  - 先获取上下文（阅读 README、目录结构与相关代码），再进入计划与实现。
  - 修改通过补丁（`apply_patch`）完成，避免无控制的手工散点编辑。

---

## 2. 执行前检查清单（必须逐项确认）

- [ ] 中文：确认任务沟通与文档输出使用中文。
- [ ] 上下文：已阅读仓库结构与关键文件（≤250 行分块）。若缺失 README，记录现状与假设。
- [ ] 工具：明确将使用的工具（`rg`/`shell`/`apply_patch`/`update_plan` 等）。
- [ ] 安全：不引入破坏性命令与外泄信息；仅改动最小必要范围。
- [ ] 质量：设计可测试、可维护的改动；遵循既有风格与规范。

> 说明：若任何一项无法满足，先补齐或同步阻塞点，再继续执行。

---

## 3. 标准工作流（Research → Plan → Implement → Verify → Deliver）

1) 研究（Research）
- 使用 `rg -n "keyword"` 检索，分块读取关键文件；形成任务理解与约束清单。

2) 计划（Plan）
- 用 `update_plan` 维护步骤，始终仅一个 `in_progress`；给出里程碑与回退策略。

3) 实施（Implement）
- 按最小可行修改实施；相关操作按逻辑分组；通过 `apply_patch` 提交变更。

4) 验证（Verify）
- 运行构建/测试（若存在）；检查边界条件、日志与性能风险；确保无破坏性影响。

5) 交付（Deliver）
- 总结变更、风险与验证结果；给出下一步建议；必要时补充文档与注释。

> 前置说明：工具执行前，用一句话说明“做什么 + 为什么”。示例：
> “我将先检索仓库与 README，确认文档结构后再补充实现。”

---

## 4. 研究-计划-实施模式（含验证与提交）

- 研究阶段：仅阅读与分析，禁止编码；输出关键文件、约束与不确定点。
- 计划阶段：拆解任务、排序依赖、明确一个 `in_progress` 步骤；约定验证标准。
- 实施阶段：小步快跑，最小变更；保持与计划同步。
- 验证阶段：运行测试/构建；核对需求、边界与日志；记录结果。
- 提交阶段：采用 Conventional Commits，附带动机、范围、影响面与验证方式。

---

## 5. 编码前强制要求：Sequential‑Thinking 分析（模板）

在任何编码前，先填写如下模板并留存于回复或变更说明中：

```
Sequential‑Thinking 分析
- 目标：用一句话描述要达成的结果。
- 约束：罗列来自任务/仓库/环境的硬性限制与准则。
- 上下文结论：列出已阅读到的关键事实与文件（若缺失 README 需注明）。
- 风险与未知：明确潜在风险、可验证点与待确认问题。
- 方案：给出最小可行路径（含回退方案）。
- 验证：说明验证方式（测试/构建/静态检查/人工审阅）。
```

---

## 6. 工具优先级与用法

- 搜索/阅读：`rg -n "keyword"`；Windows 可用 `Select-String`；分块读取 `Get-Content -TotalCount 250`。
- 终端执行：`shell`（在 CLI 中发起命令执行）。
- 代码修改：`apply_patch`（一次性补丁、分块小步提交）。
- 计划同步：`update_plan`（长任务、存在依赖或需要校验时必用）。
- 图片查看：`view_image`（仅当问题涉及图像时）。
- 网络访问：仅在必要时，优先本地上下文；外部资料需说明来源与目的。

> 工具调用前务必给出一句“前置说明”。

---

## 7. 质量标准（工程/代码/性能/测试）

- 工程原则：遵循 SOLID、DRY、关注点分离；保持与现有风格一致。
- 代码质量：清晰命名、合理抽象、必要注释（中文 Doc 注释）；避免一字母变量。
- 性能意识：评估算法复杂度、内存使用、IO；提前识别热点与外部交互成本。
- 测试思维：可测试设计、覆盖边界与异常路径；优先使用相邻/现有测试结构。

> 输出前自检：正确性、可读性、最小变更、无机密泄露、与规范一致。

---

## 8. 安全与合规（红线）

- 严禁恶意/破坏性代码；禁止执行危险命令（如 `rm -rf`）除非任务明确且可回滚。
- 不写入用户私有路径；不泄露密钥、令牌或内部链接。
- 变更边界最小化：仅触达任务直接相关的文件与模块。

---

## 9. 模板与示例

### 9.1 前置说明（示例）

> “我将先检索服务与测试，然后补充实现。”

### 9.2 `update_plan`（示例）

```json
[
  {"step": "探索代码", "status": "in_progress"},
  {"step": "实现修复", "status": "pending"}
]
```

### 9.3 `apply_patch`（最小变更示例）

```diff
*** Begin Patch
*** Update File: path/to/file.cs
@@
- // TODO: 未实现
+ // 实现：修复空引用问题（中文注释）
+ Guard.Against.Null(arg, nameof(arg));
*** End Patch
```

### 9.4 提交信息（Conventional Commits）

- `docs: add AGENTS.md user guide`
- `fix: handle null input in Parser`
- `test: add boundary cases for Parser`

### 9.5 PR/变更说明结构

- 动机：为什么改？
- 范围：改了什么？
- 影响面：对接口/部署/性能的影响。
- 验证：如何验证的（测试/构建/人工检查）。
- 风险与回退：失败时如何回滚。

---

## 10. 语言/领域子角色（Sub‑roles）

- C#/.NET：遵循现有结构（如 `Services/`、`Tools/`、测试命名 `*Tests.cs`）；`dotnet test` 验证。
- JS/TS、Python：套用等价最佳实践与测试策略；仅在确有需要时引入依赖。

---

## 11. 执行守则（Enforcement）

- 触发器：
  - 会话开始 → 执行前检查清单。
  - 工具调用前 → 前置说明。
  - 回复前 → 质量自检与最小变更核对。
- 自我改进：
  - 成功 → 沉淀决策与经验（记录于注释/文档/记忆）。
  - 失败 → 更新流程，标注风险与回避策略。

---

## 12. 常见场景 Playbooks

- 仅补文档：研究→拟定大纲→`apply_patch` 更新文档→校对→总结。
- 小型修复：定位影响面→最小变更→补充相邻测试→运行验证→文档与提交说明。
- 新信息查询：先声明目的与来源→最小引用外部资料→标注时间与可信度→落文档。

---

## 13. FAQ（精选）

- 何时联网？当本地上下文不足且必要时；需说明来源与目的。
- 何为最小变更？只修改完成任务所不可少的行与文件。
- 无 README 怎么办？记录现状与假设，优先补齐基础文档或在 PR 中说明。

---

## 14. 版本与变更记录

- 1.0.0（2025-09-07）：初始版本；整合检查清单、标准工作流、RPI 模式、模板与安全红线；强调中文 Doc 注释与最小变更原则。

--- project-doc ---

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
- Example env overrides: `XHS__Serilog__MinimumLevel=Debug`, `XHS__BrowserSettings__Headless=true`.
- Browser debugging: launch Chrome/Edge with `--remote-debugging-port=9222` before running the server.
- Environment: set `DOTNET_ENVIRONMENT=Development` when iterating locally.
