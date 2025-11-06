# HushOps.Servers.XiaoHongShu — 自动化浏览/MCP 服务

面向“小红书（Little Red Book）”相关业务的人机浏览自动化与 MCP 服务。以 RoxyBrowser 提供的多账号隔离（持久化 Context）为基础，通过 Playwright CDP 接入实现稳定、最小的原子动作工具面；截图、快照与选择器健康数据统一沉淀到 `artifacts/<dirId>` 便于离线验证与审计。

---

## 能力总览
- 多账号隔离：`dirId` 表示账号窗口；1 个 `dirId` = 1 个持久化 BrowserContext；同 Context 可创建多 Page。
- 原子化工具（唯一标准命名）：`browser.*`、`page.*`、`xhs.*`、`resources.*`。
- Roxy 管理工具（保留 roxy 命名空间，仅用于管理）：`roxy.*`（工作区/窗口管理）默认注册，无需开关。
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
- `OFFICIAL_ADAPTER_REQUIRED`（默认 `true`。官方桥接包不可用时是否终止 MCP 启动）
- `HUMAN_PROFILE`（`default`/`cautious`/`rapid`），决定鼠标/滚动/输入节律的默认值
- `HUMAN_TRACE_LOG`（可选，设为 `true` 将拟人化事件落盘到 `artifacts/<dirId>/human-trace.ndjson`）

完整环境变量列表请参考 [.env.example](.env.example)，包括：
- **连接配置**: `ROXY_API_*`, `ROXY_DEFAULT_WORKSPACE_ID`, `ROXY_DIR_IDS`
- **并发与超时**: `MAX_CONCURRENCY`, `TIMEOUT_MS`
- **选择器韧性**: `SELECTOR_RETRY_*`, `SELECTOR_BREAKER_*`
- **小红书配置**: `XHS_SCROLL_*`, `XHS_SELECT_MAX_SCROLLS`, `DEFAULT_URL`
- **日志**: `LOG_LEVEL`, `LOG_PRETTY`, `MCP_LOG_STDERR`
- **拟人化**: `HUMAN_PROFILE`, `HUMAN_TRACE_LOG`
- **兼容性**: `OFFICIAL_ADAPTER_REQUIRED`

**废弃变量**：
- `ENABLE_ROXY_ADMIN_TOOLS`（自 0.2.x 起 `roxy.*` 管理工具默认注册，无需开关）
- `POLICY_*`（已由 `SELECTOR_BREAKER_*` 替代）

---

## 安装与最小自检
1) 安装依赖
```
npm install
```
2) 配置环境变量
```
Copy-Item .env.example .env
# 编辑 .env，填写 ROXY_API_TOKEN，设置 ROXY_API_BASEURL 或 HOST/PORT
```
3) 预检（可选）
```
npm run preflight
```
4) 自检脚本（可选）
```
npm run check:env
```
5) 工具面对照检查（可选）
```
npm run check:tools
```
6) 编码巡检（可选，CI 推荐）
```
npm run check:encoding
```

---

## 启动与集成（stdio MCP）
1) 构建（或直接 TS 运行）
```
npm run build
```
2) 启动 MCP Server（stdio）
```
npm run mcp
```
提示：若未安装官方 RoxyBrowser Playwright MCP 桥接包（候选如 `@roxybrowser/playwright-mcp`），在 `OFFICIAL_ADAPTER_REQUIRED=true`（默认）时将退出；设为 `false` 可允许服务继续启动，但浏览器相关工具将不可用。可执行：

```
npm run advisor:official   # 自动给出一键安装命令（按当前包管理器）
```

在内网/私有源环境，请在 `.npmrc` 配置作用域 registry 后再安装，例如：

```
@roxybrowser:registry=https://registry.npmjs.org/
```

---

## MCP 工具清单（唯一标准）

### 浏览器与页面管理
- `browser.open` / `browser.close` - 打开/关闭浏览器窗口（dirId 映射持久化 Context）
- `page.create` / `page.list` / `page.close` - 创建/列出/关闭页面
- `page.navigate` - 导航到指定 URL

### 页面交互（支持拟人化）
- `page.click` - 点击元素（支持语义定位：role/name/label/text/testId/selector）
- `page.hover` - 悬停元素
- `page.scroll` - 滚动页面（支持相对/绝对/元素滚动）
- `page.type` - 输入文本（支持 WPM 逐字延时）
- `page.input.clear` - 清空输入框

### 页面信息获取
- `page.screenshot` - 截图（返回文本 JSON + image/png 内容项）
- `page.snapshot` - 可访问性快照（a11y 树 + url/title + 统计信息）

### 小红书专用
- `xhs.session.check` - 检查会话状态（基于 cookies 和首页加载）
- `xhs.navigate.home` - 导航到小红书首页并验证

### 资源管理
- `resources.listArtifacts` - 列出 artifacts/<dirId> 下的所有文件
- `resources.readArtifact` - 读取 artifacts 文件（自动识别图片/文本）

### 高权限管理工具（默认已注册）
- `roxy.workspaces.list` - 获取工作区列表
- `roxy.windows.list` - 获取浏览器窗口列表
- `roxy.window.create` - 创建新浏览器窗口

### 诊断与监控
- `server.capabilities` - 查看适配器信息（adapter/roxyBridge/adminTools）
- `server.ping` - 连通性检查与心跳

### MCP 资源（Resources）
- `xhs://artifacts/{dirId}/index` - 列出指定 dirId 的所有 artifacts 文件
- `xhs://snapshot/{dirId}/{page}` - 获取指定页面的 a11y 快照

示例（默认开启拟人化；两种方式关闭：`human=false` 或 `human.enabled=false`，可按需细化参数）：
- 打开窗口：`browser.open`，`{"dirId":"user"}`
- 导航：`page.navigate`，`{"dirId":"user","url":"https://example.com"}`
- 语义点击（拟人化）：
  - 快速关闭：`{"dirId":"user","target":{"text":"登录"},"human":false}`
  - 细化参数：`{"dirId":"user","target":{"text":"登录"},"human":{"enabled":true,"steps":24,"randomness":0.2}}`
- 输入（拟人化）：`page.type`，`{"dirId":"user","target":{"role":"textbox","name":"标题"},"text":"今天好开心","human":{"enabled":true,"wpm":180}}`
- 截图：`page.screenshot` → 返回文本 JSON + `image/png`
- 快照：`page.snapshot` → 返回 `url/title/a11y` 摘要 + 统计

---

## 适配层（Adapter）策略
- 优先尝试官方 Playwright MCP 桥接包（多名称候选，动态加载并读取版本）。若可用，走官方上下文 `getContext/openContext`；否则依据 `OFFICIAL_ADAPTER_REQUIRED` 决定是否终止。
- 能力探针：调用 `server.capabilities` 可查看 `{ adapter:"official", roxyBridge:{loaded,package,version}, adminTools:true }`。

---

## 常用脚本
- 检查环境：`npm run check:env`
- 工具面对照检查：`npm run check:tools`
- 选择器健康报告：`npm run report:selectors`
- 本地演示：`npm run demo:local`
- Roxy 创建窗口（管理工具）：`npm run roxy:create-window`

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
- 仅保留官方命名 `browser.*` / `page.*` 作为唯一标准；移除 roxy.* 浏览别名以降低心智负担。
- 高权限管理类 `roxy.*` 默认注册（无需环境变量开关）。
- 官方桥接包不可用时可通过 `OFFICIAL_ADAPTER_REQUIRED=false` 继续（但浏览器相关工具不可用）。
- 页面快照节点上限受 `SNAPSHOT_MAX_NODES` 保护。

---

## 官方桥接安装指引
若启动提示官方桥接不可用，可运行：
```
npm run advisor:official
```

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
tools/call: page.click { dirId, target: {...}, human: true }

// 2) 等待主要内容可见（由外部工作流自行 wait/sleep）
sleep(random(3200, 6000)) // 图文；视频建议 random(5000, 10000) 并先触发播放

// 3) 可选小幅滚动（提升自然度）
tools/call: page.scroll { dirId, human: { segments: 4, perSegmentMs: 120 } }

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
  - 证据确认：在关键节点调用 `page.snapshot` 抽取 `url/title/a11y`，作为“已到达/已可见”的低成本证据。

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
  - 截图：`page.screenshot`（文本 + image/png）；命名规则可带上步骤号与毫秒时间戳。
  - 资源：`resources.listArtifacts`/`resources.readArtifact` 直接读取 `artifacts/<dirId>` 下的文件。
  - 快照：关键操作后调用 `page.snapshot` 留存 a11y 树摘要与统计（clickableCount 等）。

- 错误分类与处理
  - 定位错误（selector/locator）→ 先回退轻滚动/切换候选 → 再次定位。
  - 动作错误（action）→ 检查元素遮挡/不可交互 → 轻移动/微停顿 → 再尝试；必要时切换为非拟人化点击。
  - 导航错误（navigate）→ 回退至上一步 URL，再行导航。

- 诊断与能力自检
  - 启动前/运行中可调用 `server.capabilities` 检查 `{ adapter, roxyBridge, adminTools }`。
  - 可开启 `HUMAN_TRACE_LOG=true` 将关键拟人化事件落盘到 `artifacts/<dirId>/human-trace.ndjson`（仅在需要时）。

- 参考调用片段（MCP 客户端）
  - 进入详情：
    - tools/call `page.click` → 外部 `sleep(random(3200,6000))`（图文）
  - 视频播放：
    - tools/call `page.click`（播放按钮）→ 外部 `sleep(random(5000,10000))`
  - 证据：
    - tools/call `page.screenshot` → tools/call `page.snapshot`
