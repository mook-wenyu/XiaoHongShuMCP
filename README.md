# HushOps XiaoHongShu MCP 服务器 · 用户使用教程

> 本地 stdio 模式的 Model Context Protocol（MCP）服务器，为小红书提供人性化自动化工具集。你只需安装 .NET 8、准备最小配置并启动服务，即可在支持 MCP 的客户端中直接调用工具。

## 项目简介
- 定位：面向桌面端的本地 stdio MCP 服务器，在支持 MCP 的客户端内以“工具”的方式操控小红书相关流程。
- 价值：零端口暴露、本地可控、拟人化动作链路、可复用浏览器会话、按需配置画像与网络策略。
- 适用对象：希望在 Claude Desktop / Claude Code / Codex CLI / Cherry Studio 等客户端中自动化浏览、采集、互动与草稿发布的用户与团队。

## 能做什么（能力一览）
- 会话管理：打开/复用浏览器配置、登录入口、会话检查
- 交互步骤：发现页导航、关键词搜索、选择笔记、点赞/收藏/评论、拟人化滚动
- 数据采集：按关键词或当前页采集笔记，导出 CSV/JSON
- 内容创作：上传图片、填写标题与正文、保存草稿
- 低级动作：单步拟人化动作（点击/输入/滚动等）

## 快速导航
- 2. 首次准备（一次性）
- 3. 快速开始（在 MCP 客户端中启动）
- 4. 配置速览（appsettings.json + 环境变量）
- 5. 启动后的验证（在客户端）
- 6. 常用场景示例
- 9. 客户端配置速查（JSON / TOML）
- 10. 发布与二进制运行（MCP）
- 7. 故障排查

## 1. 系统要求
- 操作系统：Windows / macOS / Linux
- 运行时：.NET 8（Desktop 或 Server Runtime 均可）
- 磁盘空间：≥ 1 GB（首次安装 Playwright 浏览器缓存）
- 依赖文件：确认项目根目录存在 `libs/FingerprintBrowser.dll`

## 2. 首次准备（一次性）
- 安装 Playwright 浏览器：
  - Windows：在项目根目录运行 `Tools/install-playwright.ps1`
  - Linux / macOS：在项目根目录运行 `Tools/install-playwright.sh`
- 若你处于受限网络环境，可多运行几次安装脚本或配置代理后再执行。

## 3. 快速开始

### 方式 A：在 MCP 客户端中启动（推荐）
将以下片段加入你的 MCP 客户端配置（以“xiao-hong-shu”作为服务器标识）：

```json
{
  "mcpServers": {
    "xiao-hong-shu": {
      "command": "<发布产物路径>/HushOps.Servers.XiaoHongShu",
      "args": [],
      "env": { "DOTNET_ENVIRONMENT": "Production" }
    }
  }
}
```

保存后重启/热加载你的客户端，即可自动连接该服务器。


## 4. 配置速览（可选但推荐）
服务器按如下优先级加载配置：`appsettings.json` → 环境变量（前缀 `HUSHOPS_XHS_SERVER_`）。

最小示例（保存为 `appsettings.json`，放在可执行文件所在目录或作为工作目录下的配置文件加载）：

```json
{
  "xhs": {
    "DefaultKeyword": "旅行攻略",
    "Headless": false
  },
  "HumanBehavior": {
    "DefaultProfile": "default"
  },
  "NetworkStrategy": {
    "DefaultTemplate": "default"
  }
}
```
环境变量覆盖示例（PowerShell）：

```powershell
$env:HUSHOPS_XHS_SERVER_xhs__Headless = "false"
$env:HUSHOPS_XHS_SERVER_NetworkStrategy__DefaultTemplate = "default"
```

## 5. 启动后的验证（在客户端）

- 在所用 MCP 客户端中选择 `xiao-hong-shu` 服务器，连接成功后应展示工具列表。
- 任选一个工具（如 `browser_open`）发起一次调用，客户端应返回结构化结果并在日志中可见执行过程。
- 若工具列表为空或调用失败，请检查客户端配置中的 `command` 路径与权限，以及环境变量是否生效。
## 6. 常用场景示例

> 说明：以下示例为在 MCP 客户端中调用对应工具时的请求体示例。

- 打开浏览器并准备会话：

```json
{ "tool": "browser_open", "arguments": { "profileKey": "user", "profilePath": "" } }
```

- 进行随机浏览（带画像）：

```json
{ "tool": "xhs_random_browse", "arguments": { "portraitId": "travel-lover", "browserKey": "user", "behaviorProfile": "default" } }
```

- 按关键词搜索并进入笔记：

```json
{ "tool": "xhs_search_keyword", "arguments": { "keyword": "旅行攻略", "browserKey": "user", "behaviorProfile": "cautious" } }
```

- 批量采集当前页面的笔记：

```json
{ "tool": "xhs_capture_page_notes", "arguments": { "targetCount": 20, "browserKey": "user" } }
```
## 7. 故障排查

- 工具列表为空：
  - 确认服务器正在运行；
  - 检查你的 MCP 客户端配置路径与分隔符；
  - 重新保存或重启客户端以刷新配置。

- Playwright 下载失败或缓慢：
  - 先配置代理后重试安装脚本；
  - 多尝试几次，或在非高峰时段安装。

- 会话异常：
  - 删除或更换 `profileKey` 以创建全新浏览器目录；
  - 再次执行登录后重试相关工具。

## 8. 支持
- 问题与建议：可通过本仓库的 Issue 反馈
- 联系方式：1317578863@qq.com

## 9. 客户端配置速查（JSON / TOML）

> 选择你正在使用的 MCP 客户端，按对应格式（JSON/TOML/UI）添加本服务器；以下示例均以本地 stdio 启动为例。

### 9.1 Codex CLI（TOML）

配置文件位置：用户主目录 `~/.codex/config.toml`

```toml
[mcp_servers."xiao-hong-shu"]
command = "<发布产物路径>/HushOps.Servers.XiaoHongShu"
args = []

# 可选：设置运行环境
env = { DOTNET_ENVIRONMENT = "Production" }

# 若采用 FDD（DLL）
# command = "dotnet"
# args = ["<发布产物路径>/HushOps.Servers.XiaoHongShu.dll"]
```

验证：在终端执行 `codex mcp list` 应能看到 `xiao-hong-shu`。

> 提示：TOML 键名含连字符需加引号；Windows 可用正斜杠或转义反斜杠。

### 9.2 Claude Desktop（JSON）

配置文件位置（示例）：
- Windows：`%APPDATA%/Claude/claude_desktop_config.json`
- macOS：`~/Library/Application Support/Claude/claude_desktop_config.json`
- Linux：`~/.config/Claude/claude_desktop_config.json`

最小示例：
```json
{
  "mcpServers": {
    "xiao-hong-shu": {
      "command": "<发布产物路径>/HushOps.Servers.XiaoHongShu",
      "args": [],
      "env": { "DOTNET_ENVIRONMENT": "Production" }
    }
  }
}
```

### 9.3 Claude Code（JSON / 命令行）

- 方案 A：项目根目录创建 `.mcp.json`
```json
{
  "mcpServers": {
    "xiao-hong-shu": {
      "type": "stdio",
      "command": "<发布产物路径>/HushOps.Servers.XiaoHongShu",
      "args": [],
      "env": { "DOTNET_ENVIRONMENT": "Production" }
    }
  }
}
```
- 方案 B：命令行添加
```bash
claude mcp add-json xiao-hong-shu '{
  "type":"stdio",
  "command":"/opt/app/publish/HushOps.Servers.XiaoHongShu",
  "args":[],
  "env":{"DOTNET_ENVIRONMENT":"Production"}
}'
```
- 方案 C：从 Claude Desktop 导入（如已在 Desktop 配置过）
```bash
claude mcp add-from-claude-desktop
```

### 9.4 Cherry Studio（图形界面）

- 打开 设置 → MCP Servers → Add Server
- Type 选择 `STDIO`
- Command：指向发布后的二进制（SCD）；如为 FDD，则 Command 填 `dotnet`，Args 填 `<发布产物路径>/HushOps.Servers.XiaoHongShu.dll`
- Args：SCD 留空；FDD 填 DLL 路径
- （可选）Env：新增 `DOTNET_ENVIRONMENT=Production`
- 保存并在对话页选择该服务器，工具列表应自动展示。

## 10. 发布与二进制运行（MCP）

> 目标：将服务器发布为离线可分发的产物，并在各 MCP 客户端中通过二进制启动（stdio）。建议默认生产环境：`DOTNET_ENVIRONMENT=Production`。

### 10.1 发布方式

- 框架依赖（FDD，跨平台 DLL）
  - 发布：`dotnet publish -c Release`

- 框架依赖（含宿主，RID 指定）
  - 发布（示例 Windows）：`dotnet publish -c Release -r win-x64 --self-contained false`

- 自包含（SCD，可选单文件）
  - 发布（示例 Windows 单文件）：`dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true`

> 配置文件：将 `appsettings.json` 放在“可执行文件所在目录”或以该目录为工作目录运行，环境变量前缀为 `HUSHOPS_XHS_SERVER_`。

### 10.2 Playwright 依赖
- 首次运行若未安装浏览器，服务器会自动安装（联网环境）。
- 受限/离线环境：可在有网机器先完成安装后复制浏览器缓存至目标主机，或设置镜像/缓存路径再安装。

### 10.3 客户端接入（二进制）

- Claude Desktop（JSON）
```json
{
  "mcpServers": {
    "xiao-hong-shu": {
      "command": "C:/path/to/publish/HushOps.Servers.XiaoHongShu.exe",
      "args": [],
      "env": { "DOTNET_ENVIRONMENT": "Production" }
    }
  }
}
```
- 若采用 FDD（DLL）：将 `command` 改为 `dotnet`，`args` 改为 `["C:/path/to/publish/HushOps.Servers.XiaoHongShu.dll"]`。

- Claude Code（JSON/CLI）
```json
{
  "mcpServers": {
    "xiao-hong-shu": {
      "type": "stdio",
      "command": "/opt/app/publish/HushOps.Servers.XiaoHongShu",
      "args": [],
      "env": { "DOTNET_ENVIRONMENT": "Production" }
    }
  }
}
```
或使用命令行：
```bash
claude mcp add-json xiao-hong-shu '{
  "type":"stdio",
  "command":"/opt/app/publish/HushOps.Servers.XiaoHongShu",
  "args":[],
  "env":{"DOTNET_ENVIRONMENT":"Production"}
}'
```
（FDD 时将 `command` 改为 `dotnet` 并在 `args` 中放 DLL 路径。）

- Cherry Studio（图形界面）
  - Settings → MCP Servers → Add Server
  - Type: STDIO
  - Command: 指向发布后的二进制（SCD）或 `dotnet`（FDD）
  - Args: 为空（SCD）或 `/<path>/HushOps.Servers.XiaoHongShu.dll`（FDD）
  - Env: `DOTNET_ENVIRONMENT=Production`

- Codex（TOML 示例）
```toml
[mcp_servers."xiao-hong-shu"]
command = "/opt/app/publish/HushOps.Servers.XiaoHongShu"
args = []
env = { DOTNET_ENVIRONMENT = "Production" }
```
（FDD 时设 `command = "dotnet"`，`args = ["/opt/app/publish/HushOps.Servers.XiaoHongShu.dll"]`。）
