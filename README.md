# HushOps.Servers.XiaoHongShu · 使用指南（User Guide）

面向使用者的快速上手与常用操作说明。本指南不涉及内部实现细节与架构设计，聚焦“怎么用，如何把事做成”。

---

## 我能用它做什么？
- 连接 RoxyBrowser 指纹浏览器窗口（按 `dirId` 区分账号）。
- 通过 Playwright/CDP 执行稳定的“拟人化”原子动作：打开页面、点击、悬停、滚动、截图。
- 以最小 MCP 工具面（stdio）暴露上述能力，便于在智能体/IDE 中直接调用。
- 产物与日志统一落地到 `artifacts/<dirId>/...`，便于回看与审计。

## 你需要准备什么
- Node.js ≥ 18（推荐 22.x）。
- RoxyBrowser 本地服务可用，且获取到 API Token。
- `.env` 文件中配置：`ROXY_API_TOKEN=<你的 Token>`（必填）。

> 备注：`npm install` 后会自动安装 Playwright Chromium（postinstall）。

---

## 安装与配置（3 步）
1) 安装依赖
```
npm install
```
2) 配置环境变量
```
# Windows PowerShell（示例）
Copy-Item .env.example .env
# 打开 .env，填写 ROXY_API_TOKEN
```
3) 快速检查
```
# 可选：校验 .env，并探活 Roxy（如设置 CHECK_ROXY_HEALTH=true）
npm run check:env
```

---


## Claude MCP 配置与自然语言使用教程

> 目标：在 Claude Desktop / Claude Code 中把本项目作为 MCP 服务器接入，并直接调用 `page.*` / `action.*` 工具。

1) 构建（或直接 TS 运行）
```
# 推荐：构建后由 Claude 通过 node 启动
npm install
npm run build   # 生成 dist/**
```

2) 通过 Claude CLI 添加 MCP 服务器（推荐）
```
# macOS/Linux 或 Windows（PowerShell）
claude mcp add xhs-mcp -- env ROXY_API_TOKEN=YOUR_TOKEN \
  node ./dist/mcp/server.js

# 校验
claude mcp list
claude mcp get xhs-mcp
```
提示：Windows 下若使用相对路径失败，可改为 `cmd /c node .\\dist\\mcp\\server.js`。

3) 或编辑 Claude Desktop 配置文件
- 配置文件：
  - Windows：`%APPDATA%\Claude\claude_desktop_config.json`
  - macOS：`~/Library/Application Support/Claude/claude_desktop_config.json`
  - Linux：`~/.config/Claude/claude_desktop_config.json`
- 追加片段（保持 JSON 合法）：
```json
{
  "mcpServers": {
    "xhs-mcp": {
      "type": "stdio",
      "command": "node",
      "args": ["dist/mcp/server.js"],
      "env": {
        "ROXY_API_TOKEN": "YOUR_TOKEN",
        "ROXY_API_BASEURL": "http://127.0.0.1:50000"
      }
    }
  }
}
```

4) 在 Claude 中用“自然语言”调用（示例）
- 示例提示词（Claude 会自动选择合适的工具调用）：
  - “在 dir <DIRID> 打开/复用浏览器会话，然后访问小红书发现页并全页截图。”
  - “在 dir <DIRID> 页面中滚动两屏，最后再截图。”
  - “在 dir <DIRID> 查找文字为‘登录’的按钮并点击。”

> 小贴士
> - 不要在 MCP 模式下输出 stdout 日志；本项目已默认静默（stderr/文件可用）。
> - `dirId` 必填，指向 Roxy 的账号窗口；`workspaceId` 可选，未提供时读取 `ROXY_DEFAULT_WORKSPACE_ID`。

---

## 使用 MCP（在 IDE/智能体中调用）
- 启动 MCP 服务器（stdio）
```
npm run mcp
```
- 最小工具面（部分）：
  - `roxy.openDir`（建立/复用上下文）
  - `page.new` / `page.list` / `page.close`
  - `action.navigate` / `action.click` / `action.hover` / `action.scroll` / `action.screenshot`

- 在 IDE/智能体中用“自然语言”发出指令，客户端会自动选择合适的工具：
  - “在 dir <DIRID> 打开或复用浏览器会话。”
  - “在当前页面全页截图并保存到 artifacts 目录。”
  - “点击页面上名称为 ‘登录’ 的按钮。”
> 提示：本仓库自带脚本 `scripts/mcp-call.ts` / `scripts/mcp-call-search.ts` 可用于本地调试，但推荐在 Claude/Codex 内直接用自然语言操作。

---

## 人性化默认与返回值
- 鼠标移动默认带“轻微微抖动”（幅度≈0.6px、次数≈4），点击前自动靠近目标中心；滚动为“分段+缓动+微/宏停顿”。
- 所有动作统一返回 `ok/value`，失败时返回 `error{ code,message,screenshotPath? }`。
- 失败截图由 `ACTION_SNAP_ON_ERROR=true|false` 控制（默认 true）。

---

## 目录与产物
- 截图与结果：`artifacts/<DIRID>/**`。
- 选择器健康度：`artifacts/selector-health.ndjson`（自动批量写入，进程退出前会尽力 flush）。

---

## 常用环境变量（精简表）
| 变量 | 说明 | 必填 | 默认 |
|---|---|---|---|
| `ROXY_API_TOKEN` | Roxy API Token（Header `token`） | 是 | - |
| `ROXY_API_BASEURL` | Roxy API 基址（或用 `ROXY_API_HOST/PORT`） | 否 | `http://127.0.0.1:50000` |
| `ROXY_DEFAULT_WORKSPACE_ID` | 默认工作区 ID | 否 | - |
| `ACTION_SNAP_ON_ERROR` | 动作失败自动截图 | 否 | `true` |
| `XHS_SELECT_MAX_SCROLLS` | 选择笔记时的最大滚动轮次 | 否 | `18` |

> 更多变量可查看 `.env.example` 与 `package.json` 脚本说明。

---

## 故障排查（Quick Troubleshooting）
- 提示无 Token / 401：检查 `.env` 的 `ROXY_API_TOKEN`，或以环境变量方式注入。
- 连接正常但页面空白：确认 Roxy 窗口可用，且 `dirId` 指向存活的账号窗口。
- Windows 传 JSON 失败：PowerShell 需用双引号并转义，或 `--payload=@file`。
- 等待卡住/动作不生效：避免盲等；优先使用工具内置的语义等待与监听脚本。

---

## FAQ（精简）
- Q：`dirId` 是什么？
  - A：Roxy 窗口/账号的唯一标识。可用 `scripts/roxy-connection-info.ts` 进行探活与确认。
- Q：产物保存在哪里？
  - A：统一在 `artifacts/<DIRID>` 下，包含截图、结果 JSON 与轨迹/健康度文件等。
- Q：如何关闭“拟人化”微抖？
  - A：调用 `action.click/hover` 时传 `options.microJitterPx=0` 或 `microJitterCount=0`；滚动的停顿可通过 `microPauseChance/macroPauseEvery` 关闭。

---

## 许可证
本项目使用 Apache-2.0 许可证。详见 `LICENSE`。

### Claude CLI（claude）
> 使用 Anthropic 官方 `claude` 命令行，在终端里用自然语言驱动 MCP。

1) 添加本地 MCP 服务器（stdio）
```
claude mcp add xhs-mcp -- env ROXY_API_TOKEN=YOUR_TOKEN \
  node ./dist/mcp/server.js
```
2) 开启交互会话（示例）
```
claude chat
# 输入自然语言：
# 在 dir <DIRID> 打开/复用浏览器并访问 https://www.xiaohongshu.com/explore ，随后全页截图
```
3) 管理命令
```
claude mcp list
claude mcp get xhs-mcp
claude mcp remove xhs-mcp
```

### Codex CLI（~/.codex/config.toml）
> Codex CLI 也支持 MCP。将本项目注册为本地 stdio 服务器后，用自然语言让 Codex 自动调度工具。

1) 配置文件（新增或合并）
```toml
[mcp_servers.xhs]
command = "node"
args    = ["dist/mcp/server.js"]
enabled = true
startup_timeout_sec = 15
tool_timeout_sec    = 60

[mcp_servers.xhs.env]
ROXY_API_TOKEN   = "YOUR_TOKEN"
ROXY_API_BASEURL = "http://127.0.0.1:50000"
```
2) 启动 Codex 并对话
```
# 启动后在对话里输入：
# “在 dir <DIRID> 打开探索页，滚动两屏并保存一张全页截图。”
```
3) 验收要点
- 看到工具列表中含 `page.*` 与 `action.*`。
- 执行后在 `artifacts/<DIRID>/actions` 出现截图。

### Claude Code（VS Code 扩展）
> 在 VS Code 的 Claude 扩展中用自然语言调用 MCP 工具。

- 方式一：沿用 Claude CLI 的配置
  1) 先用 `claude mcp add` 注册本项目（参考上文）。
  2) 打开 VS Code，确保安装并登录 Claude 扩展；扩展会自动识别本地 MCP 服务器。
  3) 在 Claude 面板输入自然语言，例如：
     - “在 dir <DIRID> 打开/复用浏览器会话并访问小红书发现页，随后全页截图。”
     - “在 dir <DIRID> 滚动两屏并截图，保存到 artifacts。”

- 方式二：在 Claude Code 中手动添加
  - 打开扩展设置（Settings）或命令面板（Command Palette），搜索 MCP Servers/Add Server，按提示选择 `stdio` 并填写：
    - Command: `node`
    - Args: `dist/mcp/server.js`
    - Env: `ROXY_API_TOKEN=YOUR_TOKEN`（可选 `ROXY_API_BASEURL`）
  - 完成后在对话中以自然语言驱动操作。

> 参考：官方 MCP 文档关于 “Connectors / Claude / Claude Code” 章节与 Quickstart，推荐优先通过 Claude CLI 统一管理本地 MCP 服务器。