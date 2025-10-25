# 开发者上手指南（Developer Onboarding)

最近更新：2025-10-25

## 环境准备
- Node.js ≥ 18（推荐 22）
- 安装依赖：`npm install`（首次会执行 `playwright install chromium`）
- 环境变量（.env）：
  - `ROXY_API_TOKEN`（必填）
  - 可选：`ROXY_DEFAULT_WORKSPACE_ID`

## 常用脚本
- 构建与测试
  - `npm run build:test`：TypeScript 构建
  - `npx vitest run`：全量测试
  - `npx vitest run tests/unit/**`：仅单元
- MCP 服务器（stdio）
  - `npm run mcp`
- 探活与演示
  - `npx tsx scripts/roxy-connection-info.ts --dirId=<dirId>`：连接信息
  - `npx tsx scripts/xhs-keywords-repro.ts --dirId=<dirId> --keywords=独立,游戏`
  - `node scripts/run-note-actions.js --dirId=<dirId> --keywords=独立,游戏 --comment='[微笑R]'`

## 目录导览
- 入口：`src/cli.ts`、`src/mcp/server.ts`
- 域：`src/domain/xhs/*`（导航/搜索/模态动作/网络监听）
- 人性化：`src/humanization/*`（plans/actions/core）
- 选择器：`src/selectors/*`（resilient/health/report）
- 服务：`src/services/*`（PlaywrightConnector、ConnectionManager、Policy、Pages、Artifacts）
- 分析文档：`analysis/*`

## 典型开发流程
1) 运行单测确保基线：`npm run build:test && npx vitest run`
2) 配置 `.env`（仅本机保留，不入库）
3) 实机演示：`node scripts/run-note-actions.js --dirId=<dirId> --keywords=独立,游戏 --comment='[微笑R]'`
4) 查看产物：`artifacts/<dirId>/note-actions-repro/<ts>/{result.json, final.png}`
5) 修改代码 → 局部单测（按目录/文件）→ 再跑集成脚本验证

## Windows PowerShell 传参注意
- 数组/中文与特殊字符建议使用单引号：`--keywords='独立,游戏'`
- JSON 建议 `--payload=@file` 或双引号转义：`--payload="{\"url\":\"https://example.com\"}"`

## 常用 MCP 工具（节选）
- 页面与动作：`page.new|list|close`, `action.navigate|click|hover|scroll|screenshot`
- XHS 语义工具：`xhs.navigate.home|discover`, `xhs_search_keyword`, `xhs_select_note`
- 模态内动作：`xhs.note.like|unlike|collect|uncollect|comment|follow|unfollow`

## 产物与诊断
- 截图/JSON：`artifacts/<dirId>/**`
- 选择器健康度：`artifacts/selector-health.ndjson`（可用脚本 `scripts/selector-health-report.ts` 汇总）

## 代码规范
- TypeScript + ESM；中文注释优先；小步补丁；禁止泄露凭据；遵循项目 AGENTS.md 约束。
