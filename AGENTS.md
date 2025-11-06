# AGENTS.md（XiaoHongShu 子模块指引）

- 最近更新：2025-11-06

本子模块遵循仓库根目录 AGENTS.md 的全部强制指令。本文件在本目录范围内做差异化补充：官方命名、MCP 静默、Roxy/Playwright 运行约束与测试策略等。

## 范围与优先级
- 作用域：`HushOps.Servers.XiaoHongShu/` 及子目录。
- 冲突时：以本文件明确标注“覆盖”的条目为准，其余继承。

## 关键差异（覆盖/补充）
- 依赖与运行时：Node.js + TypeScript + Playwright（CDP 连接 RoxyBrowser）。多窗口=多上下文（1 个 `dirId`=1 个持久化 Context），多页=同 Context 多 Page。
- 环境变量：
  - **必填**: `ROXY_API_TOKEN`
  - **连接**: `ROXY_API_BASEURL` 或 `ROXY_API_HOST/PORT`
  - **上下文**: `ROXY_DEFAULT_WORKSPACE_ID`, `ROXY_DIR_IDS`
  - **拟人化**: `HUMAN_PROFILE`（default/cautious/rapid）, `HUMAN_TRACE_LOG`
  - **选择器**: `SELECTOR_RETRY_*`, `SELECTOR_BREAKER_*`
  - **快照**: `SNAPSHOT_MAX_NODES`（默认 800）
  - **废弃**: `ENABLE_ROXY_ADMIN_TOOLS`（自 0.2.x 已默认开启）
- MCP 日志（覆盖）：stdio 模式下容器级静默（禁止 stdout），允许 stderr/文件。实现：`new ServiceContainer(config, { loggerSilent: true })`。
- 最小自检命令：
  - 安装：`npm install`
  - 预检（环境/官方桥接/Playwright 模块）：`npm run preflight`
  - 环境检查：`npm run check:env`
  - 工具面对照检查：`npm run check:tools`
  - 编码巡检（UTF-8 无 BOM / 中文乱码）：`npm run check:encoding`
  - 运行示例：`npm run run:example -- --dir-ids=user --url=https://example.com --limit=2`
  - 启动 MCP：`npm run mcp`
- 目录：入口 `src/cli.ts`、`src/mcp/server.ts`；核心代码在 `src/config/`、`src/services/`、`src/adapter/`、`src/mcp/tools/`、`src/selectors/`、`src/humanization/`。
- 兼容性：不向后兼容重构已生效；仅保留官方命名 `browser.*` / `page.*` 作为唯一标准；高权限管理类 `roxy.*`（工作区/窗口管理）默认注册，无需任何环境变量开关；不保留 roxy.* 浏览器动作别名。
- 工作流边界：本模块仅提供稳定的原子动作，不内置或实践业务工作流（等待/停留/重试等均由外部编排）。

## MCP 工具面（官方命名）
- 浏览器管理：`browser.open/close`
- 页面管理：`page.create/list/close/navigate`
- 页面交互：`page.click/hover/scroll/type/input.clear`
- 信息获取：`page.screenshot/snapshot`
- 小红书：`xhs.session.check/navigate.home`
- 资源管理：`resources.listArtifacts/readArtifact`
- 管理工具（高权限）：`roxy.workspaces.list/windows.list/window.create`
- 诊断工具：`server.capabilities/ping`
- MCP 资源：`xhs://artifacts/{dirId}/index`, `xhs://snapshot/{dirId}/{page}`

设计意图：等待/输入/采集等语义交给业务流程层；工具层仅提供稳定的原子动作与页面管理。

## 拟人化与选择器韧性
- 拟人化：默认开启（无需传参）；支持贝塞尔曲线、轻过冲、终点微抖动；滚动分段+缓动曲线+微/宏停顿；可通过 `human=false` 或 `human.enabled=false` 关闭，或传入参数细化节律。
- 选择器：`resolveLocatorResilient` 优先语义（role/name/label/testId/text）→ CSS 兜底；失败/耗时写入 `artifacts/selector-health.ndjson`，`scripts/selector-health-report.ts` 汇总 p95 等。

## RoxyBrowser 集成架构
- 核心类：`src/services/roxyBrowser.ts:26` - `RoxyBrowserManager`
- 实现方式：直接调用 RoxyBrowser REST API 获取 CDP WebSocket 端点，使用 Playwright 的 `chromium.connectOverCDP()` 连接
- Context 管理：每个 dirId 对应一个持久化 BrowserContext，支持缓存与连接验证
- 生命周期：由 ServiceContainer 协调，支持优雅关闭与资源清理

## 测试与验证
- 单测优先覆盖：纯函数计划层、人机轨迹、选择器与错误路径。
- MCP 最小测试：`tests/mcp/*.test.ts`（工具枚举与基本调用），在缺少 Roxy 时自动跳过依赖外部的用例。
- artifacts 为文件真源：`resources.*` 工具直接读取 `artifacts/<dirId>`，便于在无 Roxy 环境下验证。

## 安全与日志
- A2 安全清零：不实现或恢复任何认证/指纹/防护逻辑；若发现应立即移除并记录。
- MCP stdout 禁止输出；必要信息写 stderr 或文件。

## 提交与文档
- 小步变更、就近测试、UTF-8 无 BOM。
- 使用向文档：`README.md`；架构与开发细节：`AGENTS.md`。
