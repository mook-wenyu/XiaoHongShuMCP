# HushOps.Servers.XiaoHongShu — 自动化浏览/MCP 服务

![Node](https://img.shields.io/badge/node-%3E%3D18-brightgreen.svg)
![TypeScript](https://img.shields.io/badge/language-TypeScript-3178C6.svg)
![Playwright](https://img.shields.io/badge/Playwright-1.56%2B-45ba4b.svg)
![MCP](https://img.shields.io/badge/MCP-stdio-blue.svg)
![License](https://img.shields.io/badge/license-Apache--2.0-informational.svg)

面向“小红书（Little Red Book）”相关业务的人机浏览自动化与 MCP 服务。以 RoxyBrowser 提供的多账号隔离（持久化 Context）为基础，通过 Playwright CDP 接入实现稳定、最小的原子动作工具面；截图、快照与选择器健康数据统一沉淀到 `artifacts/<dirId>` 便于离线验证与审计。

## 快速开始（TL;DR）

- 做什么：提供稳定的原子浏览动作（打开/导航/点击/输入/截图/快照）与小红书专用采集工具面，支持多账号隔离。
- 为什么有用：最小依赖、MCP 标准工具、可离线验证的工件沉淀，便于在任意编排器中组合业务流程。
- 如何使用：准备 Node 环境与 Roxy API Token，安装依赖并启动 stdio MCP，或直接运行示例脚本。

```
# 1) 安装依赖
npm install

# 2) 准备环境变量（至少设置 ROXY_API_TOKEN 与 Roxy 连接）
Copy-Item .env.example .env
# 编辑 .env，填写 ROXY_API_TOKEN，设置 ROXY_API_BASEURL 或 ROXY_API_HOST/PORT

# 3) 预检（环境/官方桥接/Playwright 模块）
npm run preflight

# 4) 启动 stdio MCP（供客户端连接）
npm run mcp

# 5) 运行示例（打开并截图）
npm run run:example -- --dir-ids=user --url=https://example.com --limit=2
```

更多命令、环境变量与工具清单见文档下方章节与 `AGENTS.md`。

## 目录

- [快速开始（TL;DR）](#快速开始tldr)
- [能力总览](#能力总览)
- [环境要求](#环境要求)
- [安装与最小自检](#安装与最小自检)
- [启动与集成（stdio MCP）](#启动与集成stdio-mcp)
- [MCP 工具清单](#mcp-工具清单唯一标准)
- [RoxyBrowser 集成架构](#roxybrowser-集成架构)
- [常用脚本](#常用脚本)
- [目录结构](#目录结构)
- [测试与质量](#测试与质量)
- [设计与限制](#设计与限制)
- [变更要点（0.2.x）](#变更要点02x)
- [人机参数模板](#人机参数模板快速参考)
- [有效停留策略](#有效停留策略外部编排工作流)
- [外部工作流最佳实践清单](#外部工作流最佳实践清单)
- [故障排查](#故障排查)
- [贡献](#贡献)
- [许可证](#许可证)

---

## 能力总览

- 多账号隔离：`dirId` 表示账号窗口；1 个 `dirId` = 1 个持久化 BrowserContext；同 Context 可创建多 Page。
- 原子化工具（前缀+下划线命名）：`browser_*`、`page_*`、`xhs_*`、`resources_*`。
- Roxy 管理工具（仅用于管理）：`roxy_*`（工作区/窗口管理）默认注册，无需开关。
- 选择器韧性与拟人化：语义优先选择器（role/name/label/testId/text）→ CSS 兜底；人类化鼠标与输入（可按参数关闭）。
- 工件真源：截图、快照与选择器健康日志统一落盘 `artifacts/<dirId>`，支持 MCP 资源读取。

---

## 环境要求

- Node.js >= 18（建议 22.x）
- 可用的 RoxyBrowser 本地/远程 API 与有效 `ROXY_API_TOKEN`
- 首次安装通过 `postinstall` 自动安装 Playwright Chromium

关键环境变量：

- `ROXY_API_TOKEN`（必填）
- `ROXY_API_BASEURL` 或 `ROXY_API_HOST` + `ROXY_API_PORT`
- `ROXY_DEFAULT_WORKSPACE_ID`（可选，用于默认上下文）
- `SNAPSHOT_MAX_NODES`（页面快照节点上限，默认 800）
- `HUMAN_PROFILE`（`default`/`cautious`/`rapid`），决定鼠标/滚动/输入节律的默认值
- `HUMAN_TRACE_LOG`（可选，设为 `true` 将拟人化事件落盘到 `artifacts/<dirId>/human-trace.ndjson`）

完整环境变量列表请参考 [.env.example](.env.example)，包括：

- **连接配置**: `ROXY_API_*`, `ROXY_DEFAULT_WORKSPACE_ID`, `ROXY_DIR_IDS`
- **并发与超时**: `MAX_CONCURRENCY`, `TIMEOUT_MS`
- **选择器韧性**: `SELECTOR_RETRY_*`, `SELECTOR_BREAKER_*`
- **小红书配置**: `XHS_SCROLL_*`, `XHS_SELECT_MAX_SCROLLS`, `DEFAULT_URL`
- **日志**: `LOG_LEVEL`, `LOG_PRETTY`, `MCP_LOG_STDERR`
- **拟人化**: `HUMAN_PROFILE`, `HUMAN_TRACE_LOG`

**说明**：

- `ENABLE_ROXY_ADMIN_TOOLS`（自 0.2.x 起 `roxy_*` 管理工具默认注册，无需开关）
- 断路器相关参数以 `SELECTOR_BREAKER_*` 为主；`POLICY_*` 作为全局策略限流/熔断兼容项仍保留（见 `.env.example` 注释），两者用途不同。

---

## 安装与最小自检

1. 安装依赖

```
npm install
```

2. 配置环境变量

```
Copy-Item .env.example .env
# 编辑 .env，填写 ROXY_API_TOKEN，设置 ROXY_API_BASEURL 或 HOST/PORT
```

3. 预检（可选）

```
npm run preflight
```

4. 自检脚本（可选）

```
npm run check:env
```

5. 工具面对照检查（可选）

```
npm run check:tools
```

6. 编码巡检（可选，CI 推荐）

```
npm run check:encoding
```

---

## 启动与集成（stdio MCP）

1. 构建（或直接 TS 运行）

```
npm run build
```

2. 启动 MCP Server（stdio）

```
npm run mcp
```

---

## MCP 工具清单（唯一标准）

### 浏览器与页面管理

- `browser_open` / `browser_close` - 打开/关闭浏览器窗口（dirId 映射持久化 Context）
- `page_create` / `page_list` / `page_close` - 创建/列出/关闭页面
- `page_navigate` - 导航到指定 URL

### 页面交互（支持拟人化）

- `page_click` - 点击元素（支持语义定位：role/name/label/text/testId/selector）
- `page_hover` - 悬停元素
- `page_scroll` - 滚动页面（支持相对/绝对/元素滚动）
- `page_type` - 输入文本（支持 WPM 逐字延时）
- `page_input_clear` - 清空输入框

### 页面信息获取

- `page_screenshot` - 截图（默认仅返回 `path` 文本；如需图片数据，传 `returnImage=true` 附带 `image/png`）
- `page_snapshot` - 可访问性快照（a11y 树 + url/title + 统计信息）

### 小红书专用

- `xhs_session_check` - 检查会话状态（基于 cookies 和首页加载）
- `xhs_navigate_home` - 导航到小红书首页并验证
- `xhs_open_context` - 确保浏览器上下文已打开，返回页面数量与首个页面 URL（若存在）
- `xhs_note_extract_content` - 提取笔记完整内容（标题/正文/标签/互动数据）

> 提示：`xhs_note_extract_content` 采用“API→DOM 兜底”的两段式提取。
>
> - 优先拦截 `\/api\/sns\/web\/v1\/feed` 获取详情；
> - 当导出链接（如 `?xsec_token=...`）未触发 API 时，自动从 DOM 解析 `#detail-title`、`#detail-desc .note-text`、`a.tag#hash-tag` 等；
> - 返回 JSON 会附带 `extraction_method=api|dom`，便于上游统计命中率（消费方可忽略额外字段）。

### 小红书快捷工具（语义化）

- `xhs_close_modal` - 关闭当前笔记详情模态（Esc→关闭按钮→遮罩）
- `xhs_navigate_discover` - 导航到“发现”推荐流（含 homefeed 接口软校验）
- `xhs_search_keyword` - 站内搜索关键词（拟人化输入 + 接口软校验）
- `xhs_collect_search_results` - 在站内搜索后收集前 N 条笔记结果（优先 API 回执，DOM 兜底）
- `xhs_keyword_browse` - 关键词浏览（轻量滚动，提升可见文本覆盖）
- `xhs_select_note` - 在当前页（首页/发现/搜索）按关键词匹配卡片并点击，优先点击封面，标题兜底；成功判定为“模态优先，其次 URL 进入详情”，返回 `openedPath="modal"|"url"`
- `xhs_note_like` / `xhs_note_unlike` - 点赞 / 取消点赞当前笔记（需模态已打开）
- `xhs_note_collect` / `xhs_note_uncollect` - 收藏 / 取消收藏当前笔记（需模态已打开）
- `xhs_user_follow` / `xhs_user_unfollow` - 关注 / 取关当前笔记作者（需模态已打开）
- `xhs_comment_post` - 发表评论（拟人化输入 + 接口软校验，需模态已打开）

示例（命令行脚本）：

```
# 导航到发现页（stdio MCP）
npm run mcp -- --tool xhs_navigate_discover --dirId user

# 站内搜索关键词
npm run mcp:call:search -- --dirId=user --keyword=美食

# 关闭笔记模态（若已打开）
npm run mcp -- --tool xhs_close_modal --dirId user

# 点赞/取关/评论（需笔记详情模态已打开）
npm run mcp -- --tool xhs_note_like --dirId user
npm run mcp -- --tool xhs_user_unfollow --dirId user
npm run mcp -- --tool xhs_comment_post --dirId user --text="写得不错！"
```

### 资源管理

- `resources_listArtifacts` - 列出 artifacts/<dirId> 下的所有文件
- `resources_readArtifact` - 读取 artifacts 文件（自动识别图片/文本）

### 高权限管理工具（默认已注册）

- `roxy_workspaces_list` - 获取工作区列表
- `roxy_windows_list` - 获取浏览器窗口列表
- `roxy_window_create` - 创建新浏览器窗口

### 诊断与监控

- `server_capabilities` - 查看适配器信息（adapter/roxyBridge/adminTools）
- `server_ping` - 连通性检查与心跳

### MCP 资源（Resources）

- `xhs://artifacts/{dirId}/index` - 列出指定 dirId 的所有 artifacts 文件
- `xhs://snapshot/{dirId}/{page}` - 获取指定页面的 a11y 快照

示例（默认开启拟人化；两种方式关闭：`human=false` 或 `human.enabled=false`，可按需细化参数）：

- 打开窗口：`browser_open`，`{"dirId":"user"}`
- 打开上下文：`xhs_open_context`，`{"dirId":"user"}` → 返回 `{"ok":true,"data":{"opened":true,"pages":1,"url":"https://..."}}`
- 导航：`page_navigate`，`{"dirId":"user","url":"https://example.com"}`
- 语义点击（拟人化）：
  - 快速关闭：`{"dirId":"user","target":{"text":"登录"},"human":false}`
  - 细化参数：`{"dirId":"user","target":{"text":"登录"},"human":{"enabled":true,"steps":24,"randomness":0.2}}`
- 输入（拟人化）：`page_type`，`{"dirId":"user","target":{"role":"textbox","name":"标题"},"text":"今天好开心","human":{"enabled":true,"wpm":180}}`
- 截图：`page_screenshot`（默认仅返回路径；如需图片数据，传 `returnImage=true`）
- 快照：`page_snapshot` → 返回 `url/title/a11y` 摘要 + 统计

---

## RoxyBrowser 集成架构

- 直接通过 RoxyBrowser REST API 获取 CDP WebSocket 端点
- 使用 Playwright 官方的 `chromium.connectOverCDP()` 方法连接浏览器
- 每个 `dirId` 对应一个持久化 BrowserContext（由 RoxyBrowser 管理）
- RoxyBrowserManager 负责连接管理、Context 缓存和生命周期控制
- 能力探针：调用 `server_capabilities` 可查看 `{ adapter:"roxyBrowser", version, integration, adminTools }`

---

## 常用脚本

- 检查环境：`npm run check:env`
- 工具面对照检查：`npm run check:tools`
- 选择器健康报告：`npm run report:selectors`
- 本地演示：`npm run demo:local`
- Roxy 创建窗口（管理工具）：`npm run roxy:create-window`

---

## 目录结构

```
.
├─ src/
│  ├─ cli.ts                 # CLI 入口（示例/调试）
│  ├─ mcp/server.ts          # MCP Server（stdio）入口
│  ├─ config/                # 配置解析与校验（zod schema）
│  ├─ services/              # RoxyBrowser 管理、日志、资源等服务
│  ├─ adapter/               # Playwright/CDP 适配层
│  ├─ selectors/             # 选择器语义解析与韧性定位
│  └─ humanization/          # 拟人化运动/输入节律
├─ scripts/                  # 自检、报告、诊断与演示脚本
├─ tests/                    # 单元/集成/守卫测试（Vitest）
├─ artifacts/                # 截图/快照/健康日志等工件
├─ docs/                     # 模块文档
├─ .env.example              # 环境变量示例
└─ README.md                 # 使用向文档（本文件）
```

说明：以 `AGENTS.md` 作为架构与开发协作细则的真源；本 README 仅承载「使用与上手」。

---

## 测试与质量

- 全量测试：`npm test`
- 指定用例：`npx vitest run tests/unit/selectors/resilient.test.ts --reporter=verbose`
- MCP stdout 守卫：`tests/mcp/stdout.guard.test.ts` 确保 stdio 日志仅走 stderr 或文件
- Roxy 相关集成测试：在 Roxy 可用且必要环境变量存在时运行

---

## 设计与限制

- 工具层最小化、稳定化：仅提供原子动作与页面管理，业务流程上移。
- 多账号隔离：`dirId` 对应持久化 Context；连接与清理由容器协调。
- 拟人化与韧性：默认开启拟人化；选择器语义优先，失败与时延沉淀 p95 指标；可通过 `human=false` 或 `human.enabled=false` 显式关闭。
- 编码与日志：所有文件 UTF-8 无 BOM；MCP stdio 禁止 stdout 日志（仅 stderr/文件）。

---

## 变更要点（0.2.x）

- 仅保留前缀+下划线命名 `browser_*` / `page_*` 作为唯一标准；移除 roxy\_\* 浏览别名以降低心智负担。
- 高权限管理类 `roxy_*` 默认注册（无需环境变量开关）。
- 架构升级：移除适配层抽象，直接使用 Playwright CDP 连接 RoxyBrowser。
- 页面快照节点上限受 `SNAPSHOT_MAX_NODES` 保护。

---

## 人机参数模板（快速参考）

- 档位（使用 `profile` 一键选择节律，亦可与下方细化参数叠加）：

```
// 默认节律（平衡型）
{"human": {"profile": "default"}}

// 稳健型（更慢、更稳）
{"human": {"profile": "cautious"}}

// 效率型（更快、更灵敏）
{"human": {"profile": "rapid"}}
```

- 点击/悬停（鼠标移动曲线与抖动）

```
{"human": {"steps": 24, "randomness": 0.2, "overshoot": true, "overshootAmount": 12, "microJitterPx": 1.2, "microJitterCount": 2}}
```

- 滚动（分段滚动与停顿）

```
{"human": {"segments": 6, "perSegmentMs": 120, "jitterPx": 20, "microPauseChance": 0.25, "microPauseMinMs": 60, "microPauseMaxMs": 160, "macroPauseEvery": 4, "macroPauseMinMs": 120, "macroPauseMaxMs": 260}}
```

- 输入（每分钟字数，逐字延时）

```
{"human": {"wpm": 180}}
```

- 关闭拟人化（一次调用）

```
{"human": false}
// 或：{"human": {"enabled": false}}
```

说明：

- 不传 `enabled` 时，对象形态默认开启拟人化；`human=false` 或 `human.enabled=false` 才会关闭。
- `profile` 提供预设节律，细化参数可按需叠加覆盖。

---

## 有效停留策略（外部编排工作流）

- 图文有效阅读：建议随机停留 3.2–6.0s（≥3s），可加一次小滚动提升自然度
- 视频早期有效观看：建议随机停留 5–10s（≥5s），必要时在流程层触发播放

外部工作流（MCP 客户端）调用序列示意：

```
// 1) 打开或聚焦目标页
tools/call: page_click { dirId, target: {...}, human: true }

// 2) 等待主要内容可见（由外部工作流自行 wait/sleep）
sleep(random(3200, 6000)) // 图文；视频建议 random(5000, 10000) 并先触发播放

// 3) 可选小幅滚动（提升自然度）
tools/call: page_scroll { dirId, human: { segments: 4, perSegmentMs: 120 } }

// 4) 返回或继续下一步
```

说明：

- 本服务只提供稳定的原子动作，不内置语义流程（wait/停留）。
- 外部工作流负责编排等待、重试与业务逻辑；如需更自然的微动作，调用时传入 `human` 即可（默认已开启）。

---

## 外部工作流最佳实践清单

- 工具调用顺序（常见骨架）
  - 打开/聚焦上下文 → 页面创建/列表 → 导航（如需要） → 定位并点击/悬停/输入 →（图文≥3s/视频≥5s）有效停留 → 截图/快照/落盘 → 下一步或返回。

- 等待与确认
  - 首选“可见性/可交互”准则：点击前等待关键内容可见（例如标题、正文、播放器按钮）。
  - 轻等待与抖动：使用外部 `sleep(random(300,600)ms)` 等短暂停顿衔接原子动作，避免“连发”。
- 证据确认：在关键节点调用 `page_snapshot` 抽取 `url/title/a11y`，作为“已到达/已可见”的低成本证据。

- 重试与回退
  - 选择器容错：优先语义定位（role/name/label/testId/text），必要时回退 CSS；失败可改用文本/正则匹配变体。
  - 动作级重试（1–2 次）：定位失败/超时 → 刷新/轻滚动再定位；持续失败则降级退出，避免无限重试。

- 节流与熔断
  - 客户端侧设置 QPS 上限与指数退避（jitter），避免“突发连点”。
  - 并发与隔离：`dirId` 映射持久化 Context；跨账号并行时保证每个 `dirId` 的动作队列化，避免页面竞态。

- 拟人化策略
  - 默认开启（无需传参）；全局档位 `HUMAN_PROFILE=default/cautious/rapid` 控制节律风格；按次可 `human=false` 关闭或用对象细化（steps/randomness/wpm/segments…）。
  - 输入建议使用逐字延时（WPM），标点增加额外停顿（本模块已内置）。

- 有效停留（仅外部编排）
  - 图文：随机停留 3.2–6.0s（≥3s），可加一次小幅滚动（0.25–0.5 视口高）。
  - 视频：随机停留 5–10s（≥5s）；若未自动播放先触发播放，再计时。

- 证据与归档
- 截图：`page_screenshot`（文本 + image/png）；命名规则可带上步骤号与毫秒时间戳。
- 资源：`resources_listArtifacts`/`resources_readArtifact` 直接读取 `artifacts/<dirId>` 下的文件。
- 快照：关键操作后调用 `page_snapshot` 留存 a11y 树摘要与统计（clickableCount 等）。

- 错误分类与处理
  - 定位错误（selector/locator）→ 先回退轻滚动/切换候选 → 再次定位。
  - 动作错误（action）→ 检查元素遮挡/不可交互 → 轻移动/微停顿 → 再尝试；必要时切换为非拟人化点击。
  - 导航错误（navigate）→ 回退至上一步 URL，再行导航。

- 诊断与能力自检
- 启动前/运行中可调用 `server_capabilities` 检查 `{ adapter, roxyBridge, adminTools }`。
  - 可开启 `HUMAN_TRACE_LOG=true` 将关键拟人化事件落盘到 `artifacts/<dirId>/human-trace.ndjson`（仅在需要时）。

- 参考调用片段（MCP 客户端）
- 进入详情：
  - tools/call `page_click` → 外部 `sleep(random(3200,6000))`（图文）
  - 视频播放：
    - tools/call `page_click`（播放按钮）→ 外部 `sleep(random(5000,10000))`
  - 证据：
    - tools/call `page_screenshot` → tools/call `page_snapshot`

---

## 故障排查

- Roxy 401/403 或连接失败
  - 检查 `ROXY_API_TOKEN` 是否有效；`ROXY_API_BASEURL` 或 `ROXY_API_HOST/PORT` 是否可达
  - 若有内网/白名单限制，请确认客户端 IP 已允许
  - 用 `npm run check:env` 与 `npm run preflight` 快速定位

- Playwright 浏览器未安装/版本不匹配
  - 运行 `npx playwright install chromium` 或重新执行 `npm install`（触发 `postinstall`）
  - 确认 Node 版本满足 `>=18`（建议 22.x）

- MCP stdout 日志造成客户端协议干扰
  - 本项目在 MCP 模式下默认仅使用 stderr/文件日志；如需调试，请设置 `MCP_LOG_STDERR=true`
  - 参考测试用例 `tests/mcp/stdout.guard.test.ts`

- Windows 终端转义/路径问题
  - PowerShell 中 JSON/引号请使用反引号或正确转义：`--payload='{\"url\":\"https://example.com\"}'`
  - 建议优先使用提供的 npm 脚本参数而非手写长命令

- 中文编码/乱码
  - 所有文件需为 UTF-8 无 BOM；运行 `npm run check:encoding` 自检

---

## 贡献

- 欢迎 Issue/PR！在提交前请：
  - 代码校验：`npm run lint`、`npm run format:check`
  - 单元/集成测试：`npm test`
  - 保持中文注释与一致的命名/风格（见 `AGENTS.md`）

开发建议：

- 使用 `scripts/` 下的诊断与演示脚本便捷复现问题
- 对 MCP 工具的新增/修改，请同步更新 `README.md` 工具清单与 `AGENTS.md`

---

## 许可证

本项目采用 Apache-2.0 许可证，详见 `LICENSE`。
