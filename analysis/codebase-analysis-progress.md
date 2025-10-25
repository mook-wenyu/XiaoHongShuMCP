# Codebase Analysis Progress

项目：HushOps.Servers.XiaoHongShu（Node.js + TypeScript + Playwright + MCP）

最近更新：2025-10-25

---

## 工作流与方法（用于新会话快速续接）
- 三阶段法：
  1) 发现与架构（Discovery & Architecture）
  2) 组件分析（Component Analysis）
  3) 文档与改进建议（Documentation & Recommendations）
- 约束：仅技术向；输出面向开发者；保存到 `analysis/` 目录；跨会话以本文件为单一真源。
- 安全：避免泄露凭据/.env 内容；如需敏感文件仅做存在性校验与字段清单，不写入具体值。

## 项目上下文（摘要）
- 目标：提供面向小红书（XHS）的自动化/MCP 工具与“拟人化”交互能力（鼠标/滚动/键入等）。
- 技术栈：Node ≥18、TypeScript、Playwright、MCP（Model Context Protocol）、Pino、Zod。
- 入口：`src/cli.ts`（CLI）、`src/mcp/server.ts`（MCP stdio）。
- 核心域：`src/domain/xhs/*`（导航、搜索、模态内点赞/收藏/评论/关注、网络监听）。
- 人性化层：`src/humanization/*`（曲线/分布/随机/节律、plans、actions）。
- 选择器层：`src/selectors/*`（弹性定位、健康度、报表）。
- 服务层：`src/services/*`（PlaywrightConnector、ConnectionManager、Policy、Artifacts、Pages）。

## 已完成阶段
- Phase 1（完成）
  - 扫描源码目录结构（depth≤3）。
  - 识别关键模块与数据流（ServiceContainer→Roxy/Playwright→Pages→Domain）。
  - 初稿文档：
    - `analysis/project-overview.md`
    - `analysis/architecture-analysis.md`
- Phase 2（完成-初稿）
  - 组件深度分析：domain/services/selectors/humanization（见 component-deep-dives/*）
- Phase 3（进行中）
  - 开发者上手、排障、全面指南、技术建议落地与验收标准（文档见 analysis/*）

## 关键发现（当前）
- 架构分层清晰：Core/Services/Domain(Humanization+Selectors)/MCP Tools。
- XHS“笔记详情模态动作”已强约束在 engage-bar，并以接口回执为准（减少 UI 波动影响）。
- 选择器具备健康度收集与报表脚本，支持熔断与重试策略；Windows 环境下 PowerShell 参数转义需注意。

## 待办与优先级（Next Steps）
1) Phase 2（已产出初稿）
   - 已生成：
     - component-deep-dives/domain-xhs-navigation.md
     - component-deep-dives/domain-xhs-search.md
     - component-deep-dives/domain-xhs-noteActions.md
     - component-deep-dives/domain-xhs-netwatch.md
     - component-deep-dives/selectors-resilient.md
     - component-deep-dives/selectors-card.md
     - component-deep-dives/services-connection-and-pages.md
     - component-deep-dives/services-playwrightConnector.md
     - component-deep-dives/services-policy.md
     - component-deep-dives/humanization-plans-and-actions.md
   - 计划验证与改进：
     - [P1] 关注动作：在 noteActions 增加“文案变更为已关注则判成功”的降级（保留告警）。
     - [P1] 健康度落盘：health-sink 增加批量/退避策略与 P95 报表。
     - [P2] 容器映射：按 PageType 区分容器选择器（selectors/card）。
     - [P2] 参数一致性巡检：humanization plans/actions/options。
     - [P3] 文档补强：MCP 用法、Windows 引号与 @file 示例、排障清单。
2) Phase 3（文档与建议）
   - 完善 `technical-recommendations.md` 的执行步骤与验收标准。
   - 生成开发者上手与常见问题文档（onboarding/troubleshooting）。

## 如何续接（新聊天请先阅读本节）
- 发起新会话后，先阅读本文件，再执行：
  "Begin codebase analysis. Read codebase-analysis-progress.md for project settings and current status, then continue with the next phase of analysis work."
- 本轮已进入 Phase 1→Phase 2 的过渡；优先按照“待办与优先级”推进。
