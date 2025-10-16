# HushOps XiaoHongShu MCP 服务器 · 用户使用教程

> 本地 stdio 模式的 Model Context Protocol（MCP）服务器，为小红书提供人性化自动化工具集。你只需安装 .NET 8、准备最小配置并启动服务，即可在支持 MCP 的客户端中直接调用工具。

## 项目简介
- 定位：面向桌面端的本地 stdio MCP 服务器，在支持 MCP 的客户端内以“工具”的方式操控小红书相关流程。
- 价值：零端口暴露、本地可控、拟人化动作链路、可复用浏览器会话、按需配置画像与网络策略。
- 适用对象：希望在 Claude Desktop / Claude Code / Codex CLI / Cherry Studio 等客户端中自动化浏览、采集、互动与草稿发布的用户与团队。

## 能做什么（能力一览）

本服务器提供 **16 个 MCP 工具**，涵盖 5 大功能类别：

- **通用工具**（2个）：浏览器会话管理、低级拟人化动作
- **登录工具**（2个）：登录入口、会话状态检查
- **交互步骤工具**（7个）：导航、搜索、选择笔记、点赞/收藏/评论、滚动
- **流程工具**（2个）：随机浏览、关键词浏览（自动化完整流程）
- **数据采集工具**（2个）：当前页采集、关键词批量采集
- **内容创作工具**（1个）：发布笔记草稿

详细说明请参见 [第 6 章：MCP 工具完整说明](#6-mcp-工具完整说明)。

## 快速导航
- 2. 首次准备（一次性）
- 3. 快速开始（在 MCP 客户端中启动）
- 4. 配置速览（appsettings.json + 环境变量）
- 5. 启动后的验证（在客户端）
- 6. MCP 工具完整说明（16 个工具详解）
- 7. 故障排查
- 8. 支持
- 9. 客户端配置速查（JSON / TOML）
- 10. 发布与二进制运行（MCP）

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

### 5.1 已登录却显示未登录？（方案 C vs 方案 A）
- 现象：`browserKey: "user"` 打开后页面仍显示“登录/注册”。
- 原因：默认“用户模式”在无法附着已运行浏览器时会落到 SDK 自建目录，此目录与系统浏览器的默认配置不同，自然没有你的登录态。
- 方案 C（实验，优先）：复用系统浏览器 `userDataDir + --profile-directory`
  - 调用 `browser_open`：
    ```json
    { "tool": "browser_open", "arguments": {
      "profileKey": "user",
      "profilePath": "%LOCALAPPDATA%/Microsoft/Edge/User Data",
      "profileDirectory": "Default"
    }}
    ```
  - 注意：不要与系统浏览器并发使用同一 `userDataDir`；若报“目录可能被占用”，请先关闭占用该目录的浏览器实例。
  - 可选：设置 `HUSHOPS_PROFILE_DIRECTORY=Default` 作为默认目录名。
- 方案 A（稳妥，后备）：不传路径，使用 SDK 自建目录；首次 `xhs_open_login` 在该目录登录一次，之后长期复用。

> 提示：我们不会导出/脚本读取 Cookie；仅通过浏览器自身的持久化目录自然复用登录态。

## 6. MCP 工具完整说明

### 6.1 工具分类

本服务器提供 16 个 MCP 工具，按功能分为 5 大类：

#### 通用工具（2个）
- `browser_open` - 打开/复用浏览器配置
- `ll_execute` - 低级拟人化动作执行

#### 登录工具（2个）
- `xhs_open_login` - 打开登录入口
- `xhs_check_session` - 检查会话状态

#### 交互步骤工具（7个）
- `xhs_navigate_explore` - 导航到发现页
- `xhs_search_keyword` - 搜索关键词
- `xhs_select_note` - 根据关键词选择笔记
- `xhs_like_current` - 点赞当前笔记
- `xhs_favorite_current` - 收藏当前笔记
- `xhs_comment_current` - 评论当前笔记
- `xhs_scroll_browse` - 拟人化滚动

#### 流程工具（2个）
- `xhs_random_browse` - 随机浏览（选择+概率点赞/收藏）
- `xhs_keyword_browse` - 关键词浏览（选择+概率点赞/收藏）

#### 数据采集工具（2个）
- `xhs_capture_page_notes` - 采集当前页面笔记
- `xhs_note_capture` - 按关键词批量采集笔记

#### 内容创作工具（1个）
- `xhs_publish_note` - 发布笔记

### 6.2 工具详细说明

#### 6.2.1 通用工具

**browser_open** - 打开或复用浏览器配置
- **功能**：打开浏览器会话，支持用户配置和独立配置
- **参数**：
  - `profileKey`（可选）：浏览器键，`"user"` 表示用户配置，其他值作为独立配置目录名
  - `profilePath`（可选）：（实验）系统浏览器的 `userDataDir` 根路径，仅在 `profileKey="user"` 时有效，例如：`%LOCALAPPDATA%/Microsoft/Edge/User Data` 或 `%LOCALAPPDATA%/Google/Chrome/User Data`
  - `profileDirectory`（可选）：（实验）Chromium 的 `--profile-directory` 名称，默认按顺序推断：环境变量 `HUSHOPS_PROFILE_DIRECTORY` → `Default` → 第一个 `Profile *`
- **示例**：
```json
{
  "tool": "browser_open",
  "arguments": {
    "profileKey": "user",
    "profilePath": "%LOCALAPPDATA%/Microsoft/Edge/User Data",
    "profileDirectory": "Default"
  }
}
```
> 说明：当不提供 `profilePath/profileDirectory` 时，系统将使用 SDK 自建的持久化目录；这是更稳健的默认行为。上述参数仅在本机受控环境、明确知悉风险时启用。
**ll_execute** - 低级拟人化动作执行
- **功能**：执行单个底层拟人化动作（点击、输入、滚动等）
- **使用场景**：需要精确控制元素定位、时间参数、动作序列时使用
- **支持的动作类型**：
  - `Hover` - 鼠标悬停
  - `Click` - 点击元素
  - `MoveRandom` - 随机移动鼠标
  - `Wheel` - 滚轮滚动
  - `ScrollTo` - 滚动到目标位置
  - `InputText` - 输入文本（支持拟人化输入间隔）
  - `PressKey` - 按键
  - `Wait` - 等待
- **参数**：
  - `actionType`：动作类型
  - `locator`：元素定位器
  - `parameters`：动作参数（JSON 对象）
  - `timing`：时间参数（延迟、超时等）
  - `browserKey`：浏览器键

#### 6.2.2 登录工具

**xhs_open_login** - 打开登录入口并等待人工登录
- **功能**：导航到小红书首页，等待用户手动登录
- **参数**：
  - `browserKey`（可选）：浏览器键，默认 `"user"`
- **说明**：此工具不处理认证细节，仅打开登录入口
- **示例**：
```json
{
  "tool": "xhs_open_login",
  "arguments": {
    "browserKey": "user"
  }
}
```

**xhs_check_session** - 检查当前会话是否已登录
- **功能**：启发式检查页面是否已登录（检测登录/注册按钮）
- **参数**：
  - `browserKey`（可选）：浏览器键，默认 `"user"`
- **返回**：`isLoggedIn`（布尔值）、`url`、`heuristic`（启发式结果）
- **示例**：
```json
{
  "tool": "xhs_check_session",
  "arguments": {
    "browserKey": "user"
  }
}
```

#### 6.2.3 交互步骤工具

**xhs_navigate_explore** - 导航到发现页
- **功能**：点击导航栏发现按钮，进入发现页
- **参数**：
  - `browserKey`（可选）：浏览器键，默认 `"user"`
  - `behaviorProfile`（可选）：行为档案键，默认 `"default"`
- **示例**：
```json
{
  "tool": "xhs_navigate_explore",
  "arguments": {
    "browserKey": "user",
    "behaviorProfile": "default"
  }
}
```

**xhs_search_keyword** - 在搜索框输入关键词并搜索
- **功能**：在搜索框输入关键词并执行搜索
- **参数**：
  - `keyword`（必填）：搜索关键词
  - `browserKey`（可选）：浏览器键
  - `behaviorProfile`（可选）：行为档案键
- **示例**：
```json
{
  "tool": "xhs_search_keyword",
  "arguments": {
    "keyword": "旅行攻略",
    "browserKey": "user",
    "behaviorProfile": "default"
  }
}
```

**xhs_select_note** - 根据关键词选择笔记
- **功能**：根据关键词数组选择笔记（命中任意关键词即成功）
- **参数**：
  - `keywords`（必填）：关键词数组
  - `browserKey`（可选）：浏览器键
  - `behaviorProfile`（可选）：行为档案键
- **示例**：
```json
{
  "tool": "xhs_select_note",
  "arguments": {
    "keywords": ["旅行", "攻略", "美食"],
    "browserKey": "user"
  }
}
```

**xhs_like_current** - 点赞当前打开的笔记
- **功能**：点赞当前打开的笔记
- **参数**：
  - `browserKey`（可选）：浏览器键
  - `behaviorProfile`（可选）：行为档案键
- **示例**：
```json
{
  "tool": "xhs_like_current",
  "arguments": {
    "browserKey": "user"
  }
}
```

**xhs_favorite_current** - 收藏当前打开的笔记
- **功能**：收藏当前打开的笔记
- **参数**：
  - `browserKey`（可选）：浏览器键
  - `behaviorProfile`（可选）：行为档案键
- **示例**：
```json
{
  "tool": "xhs_favorite_current",
  "arguments": {
    "browserKey": "user"
  }
}
```

**xhs_comment_current** - 评论当前打开的笔记
- **功能**：在当前打开的笔记下发表评论
- **参数**：
  - `commentText`（必填）：评论内容
  - `browserKey`（可选）：浏览器键
  - `behaviorProfile`（可选）：行为档案键
- **示例**：
```json
{
  "tool": "xhs_comment_current",
  "arguments": {
    "commentText": "写得太好了！",
    "browserKey": "user"
  }
}
```

**xhs_scroll_browse** - 拟人化滚动浏览
- **功能**：模拟真人滚动行为，随机滚动距离和延迟
- **参数**：
  - `browserKey`（可选）：浏览器键
  - `behaviorProfile`（可选）：行为档案键
- **示例**：
```json
{
  "tool": "xhs_scroll_browse",
  "arguments": {
    "browserKey": "user"
  }
}
```

#### 6.2.4 流程工具

**xhs_random_browse** - 随机浏览
- **功能**：根据用户画像或随机选择笔记，打开详情，概率性点赞/收藏
- **参数**：
  - `portraitId`（可选）：用户画像 ID
  - `browserKey`（可选）：浏览器键
  - `behaviorProfile`（可选）：行为档案键
- **说明**：自动编排"选择笔记 → 概率点赞 → 概率收藏"完整流程
- **示例**：
```json
{
  "tool": "xhs_random_browse",
  "arguments": {
    "portraitId": "travel-lover",
    "browserKey": "user",
    "behaviorProfile": "default"
  }
}
```

**xhs_keyword_browse** - 关键词浏览
- **功能**：根据关键词数组选择笔记，打开详情，概率性点赞/收藏
- **参数**：
  - `keywords`（必填）：关键词数组
  - `portraitId`（可选）：用户画像 ID
  - `browserKey`（可选）：浏览器键
  - `behaviorProfile`（可选）：行为档案键
- **说明**：自动编排"选择笔记 → 概率点赞 → 概率收藏"完整流程
- **示例**：
```json
{
  "tool": "xhs_keyword_browse",
  "arguments": {
    "keywords": ["旅行", "美食"],
    "browserKey": "user",
    "behaviorProfile": "default"
  }
}
```

#### 6.2.5 数据采集工具

**xhs_capture_page_notes** - 采集当前页面笔记
- **功能**：采集当前页面（发现页/搜索页）的笔记数据
- **参数**：
  - `targetCount`（必填）：目标采集数量
  - `browserKey`（可选）：浏览器键
- **返回**：CSV 文件路径、采集数量
- **说明**：通过滚动页面监听 API 响应，采集笔记元数据
- **示例**：
```json
{
  "tool": "xhs_capture_page_notes",
  "arguments": {
    "targetCount": 50,
    "browserKey": "user"
  }
}
```

**xhs_note_capture** - 按关键词批量采集笔记
- **功能**：按关键词搜索并批量采集笔记数据
- **参数**：
  - `keywords`（必填）：关键词数组
  - `targetCount`（必填）：目标采集数量
  - `browserKey`（可选）：浏览器键
- **返回**：CSV 文件路径、采集数量、所用关键词
- **说明**：自动导航、搜索、滚动采集完整流程
- **示例**：
```json
{
  "tool": "xhs_note_capture",
  "arguments": {
    "keywords": ["旅行攻略", "美食推荐"],
    "targetCount": 100,
    "browserKey": "user"
  }
}
```

#### 6.2.6 内容创作工具

**xhs_publish_note** - 发布笔记
- **功能**：上传图片、填写标题和正文、保存草稿
- **参数**：
  - `imagePaths`（必填）：图片文件路径数组
  - `title`（必填）：笔记标题
  - `content`（必填）：笔记正文
  - `saveAsDraft`（可选）：是否保存为草稿，默认 `true`
  - `browserKey`（可选）：浏览器键
- **说明**：自动上传图片、填写表单、保存草稿，不自动发布
- **示例**：
```json
{
  "tool": "xhs_publish_note",
  "arguments": {
    "imagePaths": ["D:/images/photo1.jpg", "D:/images/photo2.jpg"],
    "title": "我的旅行日记",
    "content": "今天去了一个很美的地方...",
    "saveAsDraft": true,
    "browserKey": "user"
  }
}
```

### 6.3 常用场景示例

#### 场景 1：登录并检查会话

```json
// 1. 打开浏览器
{ "tool": "browser_open", "arguments": { "profileKey": "user" } }

// 2. 打开登录页面（手动登录）
{ "tool": "xhs_open_login", "arguments": { "browserKey": "user" } }

// 3. 检查会话状态
{ "tool": "xhs_check_session", "arguments": { "browserKey": "user" } }
```

#### 场景 2：搜索并点赞笔记

```json
// 1. 搜索关键词
{ "tool": "xhs_search_keyword", "arguments": { "keyword": "旅行攻略", "browserKey": "user" } }

// 2. 选择笔记
{ "tool": "xhs_select_note", "arguments": { "keywords": ["旅行", "攻略"], "browserKey": "user" } }

// 3. 点赞
{ "tool": "xhs_like_current", "arguments": { "browserKey": "user" } }

// 4. 收藏
{ "tool": "xhs_favorite_current", "arguments": { "browserKey": "user" } }
```

#### 场景 3：批量采集笔记数据

```json
// 方式 A：采集当前页面（需先手动导航或搜索）
{ "tool": "xhs_capture_page_notes", "arguments": { "targetCount": 50, "browserKey": "user" } }

// 方式 B：按关键词自动采集（自动导航+搜索+采集）
{ "tool": "xhs_note_capture", "arguments": { "keywords": ["美食"], "targetCount": 100, "browserKey": "user" } }
```

#### 场景 4：发布笔记草稿

```json
{
  "tool": "xhs_publish_note",
  "arguments": {
    "imagePaths": ["C:/Users/me/Pictures/photo1.jpg"],
    "title": "我的旅行分享",
    "content": "今天去了一个很棒的地方，分享给大家...",
    "saveAsDraft": true,
    "browserKey": "user"
  }
}
```

#### 场景 5：使用流程工具自动化浏览

```json
// 随机浏览（根据画像）
{
  "tool": "xhs_random_browse",
  "arguments": {
    "portraitId": "travel-lover",
    "browserKey": "user",
    "behaviorProfile": "default"
  }
}

// 关键词浏览
{
  "tool": "xhs_keyword_browse",
  "arguments": {
    "keywords": ["旅行", "美食"],
    "browserKey": "user",
    "behaviorProfile": "cautious"
  }
}
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

> 自 vNEXT 起，Release 配置已默认启用 Windows x64 单文件（自带运行时、压缩开启、禁用裁剪）。如需跨平台或关闭单文件，可在命令行覆盖 RID/属性。


- 框架依赖（FDD，跨平台 DLL）
  - 发布：`dotnet publish -c Release`

- 框架依赖（含宿主，RID 指定）
  - 发布（示例 Windows）：`dotnet publish -c Release -r win-x64 --self-contained false`

- 自包含（SCD，可选单文件）
  - 发布（示例 Windows 单文件）：`dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true`
  - 本仓 Release 默认已开启（win-x64）：直接 `dotnet publish -c Release` 即生成单文件于 `bin/Release/net8.0/win-x64/publish/`

> 配置文件：将 `appsettings.json` 放在“可执行文件所在目录”或以该目录为工作目录运行，环境变量前缀为 `HUSHOPS_XHS_SERVER_`。

### 10.2 目录占用提示（可选增强）
- 当你传入系统 `userDataDir` 时，若发现锁文件近期活跃（例如 `SingletonLock/SingletonCookie/SingletonSocket/LOCK`），系统会友好报错提示“目录可能被占用”。
- 处理方式：关闭正在使用该目录的浏览器实例，或改用 SDK 自建目录（方案 A）。

### 10.3 Playwright 依赖
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
