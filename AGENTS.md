# AGENTS.md（XiaoHongShu 子模块指引）

- 最近更新：2025-10-25

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

## 开发与架构细节（本子模块）

> 本节面向研发与运维同学，沉淀“为何如此设计、如何扩展与排障”的工程细节；使用者请参见 `README.md`。

### 架构总览（摘要）
- 运行时：Node.js + TypeScript + Playwright（通过 CDP 连接 RoxyBrowser）。
- 上下文模型：`dirId` 唯一标识账号窗口（1 个 `dirId` = 1 个持久化 Context），同 Context 支持多 Page。
- 服务容器：`ServiceContainer` 统一创建 `RoxyClient`、`PlaywrightConnector`、`ConnectionManager`、`PolicyEnforcer` 与日志。
- MCP 暴露：仅“原子动作”与“页面管理”，stdio 模式启用容器级静默日志（stdout 禁止，stderr 允许）。

### MCP 工具面（约束）
- 保留：`roxy.openDir`、`page.new/list/close`、`action.navigate/click/hover/scroll/screenshot`。
- 下线：`action.waitFor*`、`action.type`、`action.upload`、`action.extract`、`action.evaluate`、`action.scrollBrowse`、`action.comment`、`runner.runTask`、`tasks.list`、`xhs_detect_page`、`xhs_dump_html`、`roxy.window.detail`。
- 设计意图：将“等待/输入/采集/评论”等语义性交给平台流程，工具层仅提供稳定、可组合的原子动作。

### 人性化交互（默认开启）
- 鼠标：三次贝塞尔曲线 + 轻度过冲 + 终点微抖动（幅度≈0.6px，次数≈4）。
- 滚动：分段滚动 + 缓动曲线 + 随机微停顿/周期宏停顿；默认开启，可通过参数关闭。
- 可调参数（动作 options）：`steps`、`randomness`、`overshoot`、`overshootAmount`、`microJitterPx/microJitterCount`；滚动额外支持 `segments/jitterPx/perSegmentMs/easing/microPause*/macroPause*`。

### 选择器与健康度
- 解析入口：`resolveLocatorResilient(page, hints, { selectorId, ... })`，提供重试（指数退避）、断路器与 Locator 验证。
- 语义优先：`role/name/label/placeholder/testId/text` → CSS 兜底；为关键节点提供 `alternatives`。
- 健康度：选择器解析成功/失败/耗时写入 NDJSON（`artifacts/selector-health.ndjson`），进程退出/信号前尽力 flush。
- 指标报表：`scripts/selector-health-report.ts` 可汇总成功率、p95 等；滚动导航的进度与保留率亦写入同一 NDJSON。

### 等待与网络监听
- 统一模式：先挂监听（如 `waitFeed`/`waitHomefeed`/`waitSearchNotes`）→ 触发动作 → `await` 回执。
- 禁用盲等：避免 `waitForTimeout`；不将 `networkidle` 用作通用就绪信号。

### 产物与目录
- 账号产物：`artifacts/<dirId>/actions|capture|note-actions-repro|human-trace-like/...`。
- 全局指标：`artifacts/selector-health.ndjson`。
- 烟测与工具脚本：见 `scripts/*`（如 `smoke-like-locator.js`、`human-trace-like.js`）。

### 环境变量（工程向摘录）
- 连接：`ROXY_API_TOKEN`（必填）、`ROXY_API_BASEURL` 或 `ROXY_API_HOST/PORT`、`ROXY_DEFAULT_WORKSPACE_ID`。
- 策略：`MAX_CONCURRENCY`、`TIMEOUT_MS`、`POLICY_QPS/FAILURES/OPEN_SECONDS`。
- 截图：`ACTION_SNAP_ON_ERROR=true|false`。
- 选择器健康度：`SELECTOR_HEALTH_PATH`、`SELECTOR_HEALTH_DISABLED`。
- 导航滚动：`XHS_SELECT_MAX_SCROLLS`、`XHS_SCROLL_DEBUG_SHOT`、`XHS_SCROLL_METRICS`、`XHS_SCROLL_STEP_RATIO` 等。

### 测试与验证
- 单测：纯函数计划层（曲线/随机/滚动计划/断路器）+ 选择器与错误路径覆盖。
- 集成：Roxy 依赖用例在无服务/Token 下跳过；本地建议先跑 unit。
- 实机烟测：
  - 主按钮定位与点击：`node scripts/smoke-like-locator.js --dirId=<DIRID> --click`
  - 拟人化轨迹采样：`node scripts/human-trace-like.js --dirId=<DIRID> --click`

### 兼容与安全
- 安全清零（A2）：不实现或恢复任何认证/指纹/防护逻辑；如发现相关实现，应立即移除并记录。
- 日志：MCP 模式禁用 stdout 输出，避免污染协议；必要信息写 stderr 或文件。

### 提交与文档
- 提交粒度：小步变更、就近测试、保持 UTF-8 无 BOM。
- 文档定位：
  - 使用向：`README.md`（只讲“怎么用”）。
  - 开发与架构：本文件（AGENTS.md）。
