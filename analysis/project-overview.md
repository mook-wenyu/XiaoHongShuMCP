# Project Overview

项目：HushOps.Servers.XiaoHongShu
最近更新：2025-10-25

## 技术栈
- 语言：TypeScript（ESM）
- 运行时：Node.js ≥ 18
- 浏览器自动化：Playwright（Chromium，postinstall 安装）
- 协议：MCP（Model Context Protocol，stdio 模式）
- 校验与建模：Zod
- 日志：Pino

## 入口与运行
- CLI 入口：`src/cli.ts`
- MCP 入口：`src/mcp/server.ts`
- 常用脚本：
  - `npm run build:test`：类型构建
  - `npx vitest run`：测试
  - `npm run mcp`：MCP 服务器（stdio）
  - `npx tsx scripts/roxy-connection-info.ts`：Roxy 连接探活

## 目录结构（核心）
- `src/core`：容器与通用错误类型
- `src/services`：PlaywrightConnector、ConnectionManager、Policy、Artifacts、Pages 等
- `src/domain/xhs`：XHS 导航/搜索/模态内动作（点赞/收藏/评论/关注）/网络监听
- `src/humanization`：曲线/分布/随机/节律、plans、actions
- `src/selectors`：弹性定位、健康度采集与报表
- `src/mcp/tools`：对外暴露的 MCP 工具（action.* 与 xhs.* 语义工具）
- `scripts`：演示、探活与报表脚本

## 外部依赖（节选）
- `playwright`, `zod`, `undici`, `pino`

## 环境要求
- `ROXY_API_TOKEN`（必需，见 `.env.example`）
- `ROXY_DEFAULT_WORKSPACE_ID`（可选）
- Windows PowerShell 下传参建议使用 `--payload=@file` 或正确转义引号

## 当前状态
- 人性化默认：鼠标微抖动、滚动停顿/easing 已启用
- 模态动作：严格限定到 `.note-detail-mask` 外壳下的 `engage-bar`，以接口回执为准
- 选择器：支持熔断/重试与健康度报表，已落盘 `artifacts/selector-health.ndjson`
