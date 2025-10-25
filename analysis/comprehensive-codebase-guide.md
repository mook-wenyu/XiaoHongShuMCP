# 全面代码库指南（Comprehensive Codebase Guide）

最近更新：2025-10-25

## 一、项目综述
- 技术栈：TypeScript/Node ≥18、Playwright、MCP、Pino、Zod
- 核心模块：Core/Services/Domain.XHS/Humanization/Selectors/MCP Tools
- 主要入口：`src/cli.ts`、`src/mcp/server.ts`

## 二、运行与验证
- 构建：`npm run build:test`
- 单测：`npx vitest run`
- 实机演示：`node scripts/run-note-actions.js --dirId=<dirId> --keywords=独立,游戏 --comment='[微笑R]'`
- 产物：`artifacts/<dirId>/**`

## 三、架构与数据流
- ServiceContainer → RoxyClient/PlaywrightConnector/ConnectionManager/Policy
- 选择器：`resolveLocatorResilient`（重试/熔断/健康度）
- 人性化：plans→actions（鼠标/滚动/键入）
- XHS 模态：严格作用域 + 接口回执为准

## 四、常见开发任务
- 新增动作：在 `domain/xhs/*` 衍生原子动作（严格限定作用域），在 `mcp/tools` 暴露工具（如需）
- 调整滚动/关键词策略：在 `domain/xhs/navigation.ts` 的步长/复扫/降级参数中微调
- 选择器回归：通过 `selectors/health-sink.ts` 与报表脚本观测 P95

## 五、排障要点
- MODAL_REQUIRED/LIKE_FAILED/FEED_TIMEOUT：见 `analysis/troubleshooting-guide.md`
- Windows 传参与引号：见 `analysis/developer-onboarding-guide.md`

## 六、建议与路线图
- 见 `analysis/technical-recommendations.md`（含执行次序与验收标准）

## 七、文档索引
- Overview：`analysis/project-overview.md`
- Architecture：`analysis/architecture-analysis.md`
- 深度分析：`analysis/component-deep-dives/*`
- Onboarding：`analysis/developer-onboarding-guide.md`
- Troubleshooting：`analysis/troubleshooting-guide.md`
- Recommendations：`analysis/technical-recommendations.md`
