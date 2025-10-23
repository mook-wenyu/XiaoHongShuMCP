# AGENTS.md（XiaoHongShu 子模块指引）

- 最近更新：2025-10-21

本子模块遵循仓库根目录 `AGENTS.md` 的全部强制指令与流程（§0–§8）。本文件仅用于在本目录范围内做“差异化补充或覆盖”。如无特别声明，以根 `AGENTS.md` 为唯一真源。

## 适用范围与优先级
- 作用域：`HushOps.Servers.XiaoHongShu/` 及其子目录。
- 优先级：当本文件条目与根 `AGENTS.md` 冲突时，以本文件中“明确标注覆盖”的条目为准；未覆盖部分全部继承根文档。

## 当前差异化条目（覆盖/补充）
- 依赖与运行时：本子模块迁移为 Node.js + TypeScript + Playwright（CDP 连接 RoxyBrowser）。多窗口 = 多上下文（一个账号=一个默认持久化 Context），多页 = 同 Context 多 Page。
- 环境变量：`ROXY_API_TOKEN`（必填）、`ROXY_API_BASEURL`（可选）或 `ROXY_API_HOST`+`ROXY_API_PORT`（默认 127.0.0.1:50000）、`ROXY_DIR_IDS`（逗号分隔）。
- MCP 日志规范（覆盖）：MCP stdio 模式下，容器级静默日志（禁止任何 stdout 输出），允许通过 stderr 或文件记录。实现：`new ServiceContainer(config, { loggerSilent: true })`。
- 最小自检命令：
  - 安装：`npm install`
  - 运行示例：`npm run run:example -- --url=https://example.com --limit=2`
  - 启动 MCP（stdio）：`npm run mcp`
- 目录说明（摘录）：
  - 入口（Node）：`src/cli.ts`、`src/mcp/server.ts`
  - 代码：`src/config/`、`src/clients/`、`src/services/`、`src/runner/`、`src/tasks/`
- 兼容性：允许不向后兼容重构，原 .NET 代码保留但不作为运行入口。

## 提交/PR 补充要求
- 若调整浏览器会话策略（独立上下文相关参数），需更新 `docs/browserhost-design.md` 并在 PR 中附“最小复现实验步骤”。

（无其他覆盖项时，本文件将保持精简，以避免与根文档内容重复。）
