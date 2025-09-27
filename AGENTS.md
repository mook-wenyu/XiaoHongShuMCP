# Repository Guidelines

## 项目结构与模块组织
- 根目录包含 `HushOps.Servers.XiaoHongShu.csproj` 与入口 `Program.cs`，主要服务代码位于 `Services/`，基础设施与外部依赖适配位于 `Infrastructure/`。
- 配置模型与选项集中在 `Configuration/`，共享扩展在 `ServiceCollectionExtensions.cs`；上线资源或示例载荷放入 `storage/`，CLI 与脚本放在 `Tools/`。
- 若需新增测试，请在同级目录创建 `Tests/HushOps.Servers.XiaoHongShu.Tests/`，按照模块划分子文件夹，保持命名与源模块一致。

## 构建、测试与本地运行命令
- `dotnet restore`：恢复解决方案依赖，任何首次构建前必须执行。
- `dotnet build HushOps.Servers.XiaoHongShu.csproj -c Release`：编译主服务并执行编译期分析。
- `dotnet run --project HushOps.Servers.XiaoHongShu.csproj`：以默认配置启动本地开发实例。
- `dotnet test`：在 `Tests/` 目录存在时运行全部测试，提交前务必通过。

## 编码风格与命名约定
- 使用 .NET 默认四空格缩进，类、接口、公共成员采用 PascalCase，私有字段使用 `_camelCase`。
- 配置记录和值对象命名以 `Xhs` 前缀保持一致，例如 `XhsSettings`、`XhsClientOptions`。
- 格式化统一使用 `dotnet format` 或 Rider/.editorconfig 预设，提交前确保无额外警告。

## 测试指引
- 推荐使用 xUnit；测试类命名为 `<模块名>Tests`，方法命名遵循 `方法_场景_结果` 中文描述。
- 关键路径（配置绑定、服务注册、外部 API 适配）须具备正反用例，目标覆盖率不低于 70%，缺口需要在 PR 中声明缓解计划。

## 提交与 PR 指南
- Git 历史同时存在中文摘要与 Conventional Commits 格式，例如 `refactor(config): ...`；建议采用 `类型(范围): 描述` 或精炼中文标题。
- PR 描述需包含变更概览、测试结果、关联 Issue 与必要截图，若涉及配置或脚本调整请附迁移注意事项。
- 在 PR checklist 中确认文档、代码、测试同步更新，并请求至少一位熟悉模块的审阅者。

## 安全与配置提示
- 环境机密通过 `Configuration/` 下的绑定模型接收，禁止将密钥写入仓库；本地调试使用 `dotnet user-secrets`。
- 外部依赖（如红书 API）调用必须封装在 `Services/` 层，记录重试与限流策略，并在描述中链接相应文档。

# AGENTS.md — 全局指南

## 0. 阅读须知
- 本指南对仓库全体目录生效，若子目录另有 AGENTS.md，则以子目录指南为准。
- 所有沟通、文档、注释统一使用中文；文件编码采用 UTF-8（无 BOM）。
- 用户显式指令优先，优先级高于任一禁止条款或约束；执行时如涉及潜在风险，需即时在文档中留痕。

## 1. 治理总则
### 1.1 适用范围
- 本指南覆盖开发、文档、测试、交付的全部活动。

### 1.2 执行优先级
1. 用户显式指令（可覆盖其他禁止条款及约束）。
2. 子目录 AGENTS.md。
3. 本文件。
4. 其他项目文档及默认约定。

### 1.3 基本原则
1. 复用主流生态与官方 SDK，禁止重复造轮子。
2. 质量唯一门槛：代码可编译、核心功能可用，出现缺陷先修复。
3. 文档同步：需求、设计、任务拆解、实现、编码决策、测试结论必须实时落盘 `docs/`。
4. 工具约束：写操作仅限补丁机制工具，读操作仅限内部只读工具，禁止 shell/python 等直接命令读写。
5. MCP 工具强制自动加载：Codex CLI 启动时必须主动连接全部核心 MCP（见第 3 章），不得跳过或延后；若某 MCP 无法自动连接或任务紧急，允许暂时降级为补丁工具并在 `docs/coding-log.md` 留痕，随后补充回退原因、恢复计划与再验证结果。
6. 透明可审计：关键决策、文档、代码差异需可追溯；可删除的场景除外。
7. 结果导向：按交付成果、SLO/SLI 衡量成效。

### 1.4 统一专业化工作流
- 所有任务，无论规模与持续时间，均按专业项目流程执行：Research → Design → Plan → Implement → Verify → Deliver → Changelog，不设简化通道。
- 每个阶段须产出对应文档并达成复核，Design 阶段负责形成决策与风险缓解方案，Plan 阶段基于设计结论拆解任务，通过文档驱动交付，确保可追溯、可审计。
- 工具选择以安全合规为前提：Serena 可用时优先，若出现不可用立即切换补丁工具，但仍保持全流程文档记录并在 `docs/coding-log.md` 留痕。

## 2. 强制约束（MUST）
### 2.1 工作执行
- 仅运行安全命令，严禁 `rm -rf` 等破坏性操作及敏感信息泄露。
- 严禁直接使用 shell/python/编辑器脚本写入文件。
- 代码、文档、配置改动须补齐必要注释与说明。
- 如用户显式指令要求突破安全或工具约束，必须同步风险评估结果并在 `docs/coding-log` 记录执行过程。

### 2.2 文档管理
- `docs/requirements|design|tasks|implementation|coding-log|testing` 汇总各阶段的最新结论与跨任务规范，仅保留最近任务摘要，并附指向任务级文档的链接。
- 每个任务必须在 `docs/workstreams/<TASK-ID>/` 下建立 `research|design|plan|implementation|verification|delivery|changelog|operations-log` 等文件，使用统一表头（任务 ID、来源、更新时间、责任人、关联提交、状态）记录细节；并发任务通过独立目录避免信息交叉。
- 对公共模板、复盘材料，可放置于 `docs/workstreams/shared/`，在顶层索引中引用。
- Serena 记忆写入默认不再强制：
  - 若 Serena 无法使用或任务需紧急处理，优先补齐 `docs/requirements|design|tasks|implementation|coding-log|testing` 及 `docs/index.md`，并在 `docs/coding-log` 记录“未写入记忆”的原因。
  - 工具恢复后执行“文档完整性复核”（需求、设计、实现、测试、任务拆解、日志、索引日期一致且内容覆盖交付结论）；复核通过即视为知识已沉淀，保持不写入 Serena 记忆。
  - 仅当复核发现文档缺口或利害相关方明确要求追加知识共享时，方可补录 Serena 记忆，并在 `docs/coding-log` 补记补录时间与范围。
- 文档完整性最佳实践：
  - 编写时逐项同步更新需求→设计→任务→实现→测试→交付→索引→更改日志，保持顶层与任务级文件一致。
  - 每次提交前执行“文档对勾检查”，确认六大汇总文档与对应 `docs/workstreams/<TASK-ID>/` 文件均更新到最新日期。
  - 测试与验证需注明覆盖范围、结论及遗留风险，缺失风险须在 `docs/coding-log` 留痕，并在任务子目录的 `verification.md` 中同步。
  - 发布前由同伴或自检对 `docs/index.md` 进行二次确认，确保索引内引用的文档均存在、时间戳一致，并列出进行中任务列表。
- 更改日志管理：
  - 每个交付创建或更新 `docs/changelog.md` 条目，包含任务 ID、变更摘要、影响评估、迁移/回滚提示及关联文档链接。
  - 更改日志需与交付时间保持一致，记录责任人和审核人；如为紧急修复，须标注补录时间。
  - 定期（至少每月）复核更改日志，清理重复条目并确认与 `docs/index.md`、提交记录一致。
- 所有任务须满足同等文档深度与完备度，不得以临时性、紧急性为理由降低标准。
- 每份文档需包含任务 ID、来源、更新时间、责任人、关联提交、状态；未同步禁止进入下一阶段或合并代码。
- `docs/index.md` 负责追踪所有文档及最新更新时间。

### 2.3 变更策略
- 优先清理过时接口与文档，可做不兼容改动但需在交付说明标注迁移策略。
- 禁止交付半成品或占位实现。

### 2.4 工具管控
- 写操作：仅允许 Codex CLI、Explored 及未来确认仍基于补丁机制的工具。
- 读操作：仅允许 Codex CLI、Explored 等只读工具及 Serena/内部检索接口。

#### 2.4.1 Serena 不可用处理清单
1. 当 Serena 调用返回失败、提示不可用或持续无响应时，立即认定为不可用，无需额外等待或重试。
2. 在 `docs/coding-log` 记录触发时间、症状与后续处理方案。
3. 立刻改用补丁工具完成当前子任务，保持小步修改并遵守既定工具约束。
4. Serena 恢复后，如有必要再补充记忆或附加说明。

### 2.5 操作留痕
- Serena 正常时按标准流程执行；Serena 受阻且使用补丁工具时视为常规操作，无需额外登记。
- 仍须保留满足审计的最终成果（代码、文档提交记录）。

## 3. 工具与调研
### 3.1 MCP 强制自动调用策略
- `config.toml` 中列出的核心 MCP（默认包含 `chrome-devtools`、`context7`、`deepwiki`、`sequential-thinking`、`serena`）必须在 Codex CLI 启动时自动连接，任何任务开始前需确认全部连接成功。
- 启动脚本或 CLI 配置需开启 MCP 自动侦测、强制重连与失败报警；若某 MCP 支持 `autostart`、`force` 等参数，必须启用并在 `config.toml` 中注明。
- CLI 启动日志需保留 MCP 自动加载结果，至少包含成功/失败状态、重试次数与耗时，供 `docs/workstreams/<TASK-ID>/verification.md` 审阅；若连接失败未恢复，禁止继续后续阶段。

### 3.2 配置基线与健康检查
- 每个 MCP 配置应记录调用方式（如 `npx`、本地二进制、代理 URL）、运行时依赖（Node/npm 版本、Python、网络访问策略）及健康检查命令。
- 至少在任务开始与每日首次启动时执行健康检查，检查结果需记录在任务 `operations-log.md` 或 `coding-log.md`，若失败须立即触发降级流程。
- 不再使用的 MCP 应在设计阶段评估后移除或标记为可选，同时在 `docs/index.md` 标注状态变更与原因。

### 3.3 自动化变更流程
1. 在 Design 阶段评估新增/调整 MCP 对自动调用的影响，含依赖、启动耗时、失败回退，并写入 `docs/workstreams/<TASK-ID>/design.md`。
2. Plan 阶段将自动调用的触发节点、健康检查与监控责任人同步至 `docs/tasks.md` 与任务 `plan.md`。
3. 修改 `config.toml` 或相关脚本后，须在 `docs/workstreams/<TASK-ID>/operations-log.md` 与 `docs/coding-log.md` 登记时间、责任人、原因、回退方案。
4. 交付前在 `docs/workstreams/<TASK-ID>/verification.md` 附上自动调用日志（截屏或命令输出摘要），确认所有 MCP 成功加载或列出失败与缓解措施。

### 3.4 运行期管控与降级
- Codex CLI 应在 MCP 自动调用失败时自动重试至少一次，并弹出提示查看日志；若仍失败，必须触发 2.4.1 的降级流程并记录。
- 运行过程中监控 MCP 会话，如检测到意外断开需立即重连并记录在 `operations-log.md`，必要时切换备用 MCP。
- 对性能影响较大的 MCP（例如浏览器相关服务）允许延迟加载，但必须配置自动触发条件、健康检查与日志校验，并确保网络请求仍优先交由 `chrome-devtools`。

### 3.5 网络访问优先策略
- 所有网络获取、网络搜索、网页截图等操作默认使用 `chrome-devtools` MCP；除非经设计评审确认不可行，否则禁止直接改用其他工具。
- 若 `chrome-devtools` 暂不可用，需在 `docs/coding-log.md` 与任务 `operations-log.md` 记录降级原因，并优先选择可审计的替代 MCP；恢复后必须补做验证。
- 编写计划与验证文档时须注明网络操作所依赖的 MCP，提交交付前提供相关日志或截图作为凭证。

### 3.6 推荐工具清单
- Serena MCP：用于项目激活、符号检索、自动编辑，默认自动连接；若因策略限制不可用，需在文档说明降级原因。
- Sequential Thinking MCP：自动记录思考链与计划步骤，保持启用。
- `chrome-devtools` MCP：承担网络获取、搜索、网页操作的首要职责，需保持高可用性。
- 其他官方检索（Context7、DeepWiki、web.run 等）：保持在自动调用清单中，若暂无法连通需在任务文档声明并说明对网络策略的影响。
- 仅当全部 MCP 无法恢复时，方可临时使用补丁工具执行写操作，并在 `docs/coding-log.md` 留痕，待 MCP 恢复后补充审计。

## 4. 标准工作流
1. **Research**：获取上下文、明确约束与成功标准；输出 `docs/workstreams/<TASK-ID>/research.md` 与 `docs/requirements.md` 中的需求摘要。Serena 可用时参与检索，若检测到不可用即改用只读工具并在 `docs/coding-log.md` 留痕。
2. **Design**：基于研究成果提出可选方案、评估风险与决策理由；沉淀到 `docs/workstreams/<TASK-ID>/design.md`，并在 `docs/design.md` 中同步最新任务摘要。必要时组织评审，记录决策编号与风险缓解策略。
3. **Plan**：通过 `update_plan` 拆解任务并维护状态机，同步 `docs/workstreams/<TASK-ID>/plan.md` 与 `docs/tasks.md`；所有计划需标注责任人、依赖、预计完成时间，并与设计结论保持一致。
4. **Implement**：执行代码或文档变更，小步提交；记录在 `docs/workstreams/<TASK-ID>/implementation.md` 与 `docs/implementation.md`。Serena 可用时首选；如不可用，立即改用补丁工具并在 `docs/coding-log.md` 标记原因。
5. **Verify**：完成必要的测试、审计或验收，结果写入 `docs/workstreams/<TASK-ID>/verification.md` 与 `docs/testing.md`，注明覆盖范围、结论与遗留风险。
6. **Deliver**：整理交付物、风险、迁移/回滚策略，更新 `docs/workstreams/<TASK-ID>/delivery.md`、`docs/index.md`，并在交付说明中引用相关文档。
7. **Changelog**：将本次交付条目加入 `docs/workstreams/<TASK-ID>/changelog.md` 与 `docs/changelog.md`，记录任务 ID、交付时间、责任/审核人、影响范围与关联文档；若无变更，应显式记录“本次交付无更新”。

## 5. 质量门槛
- 构建/静态检查必须通过，确保代码可编译。
- 核心功能按需求验证完整可用。

## 6. 交付与存档
- 交付清单需包含迁移/回滚方案、相关文档清单、最新提交信息。
- 未补齐 `docs/` 记录或索引的变更不得交付。

## 7. 内部工具白名单与黑名单
### 7.1 写入白名单
- Codex CLI
- Explored
- Serena/其他经确认仍基于补丁机制的内部工具

### 7.2 读取白名单
- Codex CLI
- Serena/内部检索接口

## 8. 工程师行为准则
- 先求证后行动，结论须有来源或工具结果支撑。
- 复用标准方案，避免自研。
- 按职责共享质量与文档责任。
- 保持透明沟通，一致遵循本指南。

