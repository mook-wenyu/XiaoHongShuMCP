# HushOps.Servers.XiaoHongShu

> 基于 .NET 8 的 Model Context Protocol（MCP）本地 stdio 服务器，为小红书平台提供人性化自动化工具集。

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![MCP](https://img.shields.io/badge/MCP-stdio--only-FF6B6B)](https://modelcontextprotocol.io/)
[![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](./LICENSE)

## 核心特性

- **MCP 工具协议**：基于 stdio 通信的 MCP 服务器，通过 `ModelContextProtocol.Server` 托管并自动发现程序集内标记的工具
- **人性化行为编排**：多层次拟人化系统，支持行为档案配置（默认/谨慎/激进）、随机延迟抖动、滚动/点击/输入模拟，确保操作流畅自然
- **完整交互流程**：涵盖随机浏览、关键词搜索、发现页导航、笔记点赞、收藏、评论、批量捕获等完整工作流
- **浏览器指纹管理**：支持自定义 User-Agent、时区、语言、视口、设备缩放、触摸屏等指纹参数，每个配置独立缓存
- **网络策略控制**：可配置代理、出口 IP、请求延迟、重试策略、缓解措施等，应对反爬虫检测
- **灵活配置模式**：支持用户浏览器配置（自动探测路径）和独立配置（隔离环境），可通过 JSON 或环境变量配置

## 工具清单（Tool Catalog）

项目通过 MCP 协议暴露以下工具，可通过 `dotnet run --project <项目路径>/HushOps.Servers.XiaoHongShu.csproj -- --tools-list` 查看完整列表；该命令同样可用于验证客户端是否完成连接。（Run the command to list tools and confirm client connectivity.)

| 工具名称 (Tool) | 类型 (Category) | 功能描述 (Description) |
|-----------------|-----------------|-------------------------|
| `browser_open` | 会话管理 / Session management | 打开或复用浏览器配置，支持用户模式与隔离模式 (open or reuse browser profile) |
| `xhs_random_browse` | 业务流程 / Business flow | 按画像或默认关键词随机浏览，并概率性点赞收藏 (random browse with probabilistic engagements) |
| `xhs_keyword_browse` | 业务流程 / Business flow | 使用关键词数组浏览并执行互动 (keyword-driven browse with engagements) |
| `xhs_navigate_explore` | 交互步骤 / Interaction step | 导航到发现页 (navigate to discover feed) |
| `xhs_search_keyword` | 交互步骤 / Interaction step | 在搜索框输入关键词并搜索 (type keyword in search box) |
| `xhs_select_note` | 交互步骤 / Interaction step | 根据关键词选择笔记 (select note matching keywords) |
| `xhs_like_current` | 交互步骤 / Interaction step | 点赞当前打开的笔记 (like current note) |
| `xhs_favorite_current` | 交互步骤 / Interaction step | 收藏当前打开的笔记 (favorite current note) |
| `xhs_comment_current` | 交互步骤 / Interaction step | 评论当前笔记 (comment on current note) |
| `xhs_scroll_browse` | 交互步骤 / Interaction step | 拟人化滚动当前页面 (humanized scroll) |
| `xhs_note_capture` | 数据采集 / Data capture | 拟人化逐条采集笔记并导出 CSV (capture notes with navigation) |
| `xhs_capture_page_notes` | 数据采集 / Data capture | 在当前列表页直接采集笔记并导出 CSV (capture current page notes) |
| `xhs_publish_note` | 内容创作 / Content authoring | 上传图片、填写标题正文并暂存草稿 (upload, fill content, save draft) |
| `ll_execute` | 低级动作 / Low-level control | 执行单个拟人化动作 (execute discrete humanized action) |

### 参数说明（Parameter Reference）

> ⚠️ v1.1.0 (2025-10-02)：所有 MCP 工具的字符串参数类型从 `string?` 统一调整为非空 `string`，默认值为空字符串 `""`，以提升序列化兼容性。（All string parameters are now non-nullable `string` with default `""` for better serialization.)

**常用归一化规则（Normalization rules）**
- `browserKey: ""` → 自动归一化为 `"user"`；其它值会在 `storage/browser-profiles/<browserKey>` 下创建独立配置。（Empty browserKey normalizes to `user`; other values map to isolated profile directories.)
- `behaviorProfile: ""` → 自动归一化为 `"default"`。（Empty behaviorProfile normalizes to `default`.)
- `profilePath: ""` → `browser_open` 自动探测本地浏览器配置路径，仅 `user` 模式可手动指定。（Empty profilePath auto-detects; explicit path only allowed in user mode.)

下表列出各工具参数，`必填` 表示是否必须提供非空值；留空时将使用默认值并按照上述规则归一化。（Tables describe tool parameters; Required indicates if a non-empty value is mandatory.)

#### `browser_open`

| 参数 (Parameter) | 类型 (Type) | 必填 (Required) | 默认值 (Default) | 说明 (Description) |
|------------------|-------------|-----------------|------------------|--------------------|
| `profilePath` | string | 否 / No | `""` | 用户浏览器配置目录；为空时自动探测。（Local profile path; auto-detected when empty.) |
| `profileKey` | string | 否 / No | `""` → `"user"` | 用户模式或隔离模式键。（Profile key for user vs isolated profiles.) |

#### `xhs_random_browse`

| 参数 (Parameter) | 类型 (Type) | 必填 (Required) | 默认值 (Default) | 说明 (Description) |
|------------------|-------------|-----------------|------------------|--------------------|
| `keywords` | string[] | 否 / No | `null` | 关键词候选；为空时根据画像或默认配置选择。（Keyword candidates; falls back to portrait/default.) |
| `portraitId` | string | 否 / No | `""` | 画像 ID；为空使用全局画像。（Portrait identifier.) |
| `browserKey` | string | 否 / No | `""` → `"user"` | 浏览器配置键。（Browser profile key.) |
| `behaviorProfile` | string | 否 / No | `""` → `"default"` | 行为档案键。（Behavior profile key.) |

#### `xhs_keyword_browse`

| 参数 (Parameter) | 类型 (Type) | 必填 (Required) | 默认值 (Default) | 说明 (Description) |
|------------------|-------------|-----------------|------------------|--------------------|
| `keywords` | string[] | 否 / No | `null` | 关键词数组；为空时回退到画像或默认关键词。（Keyword list; falls back when empty.) |
| `portraitId` | string | 否 / No | `""` | 画像 ID；用于关键词兜底。（Portrait fallback.) |
| `browserKey` | string | 否 / No | `""` → `"user"` | 浏览器配置键。（Browser profile key.) |
| `behaviorProfile` | string | 否 / No | `""` → `"default"` | 行为档案键。（Behavior profile key.) |

#### 交互步骤工具（Interaction step tools）

| 工具 (Tool) | 参数 (Parameter) | 类型 (Type) | 必填 (Required) | 默认值 (Default) | 说明 (Description) |
|---------------|------------------|-------------|-----------------|------------------|--------------------|
| `xhs_navigate_explore` | `browserKey` | string | 否 / No | `""` → `"user"` | 浏览器配置键。（Browser profile key.) |
| | `behaviorProfile` | string | 否 / No | `""` → `"default"` | 行为档案。（Behavior profile.) |
| `xhs_search_keyword` | `keyword` | string | 是 / Yes | — | 搜索关键词。（Keyword to search.) |
| | `browserKey` | string | 否 / No | `""` → `"user"` | 浏览器配置键。（Browser profile key.) |
| | `behaviorProfile` | string | 否 / No | `""` → `"default"` | 行为档案。（Behavior profile.) |
| `xhs_select_note` | `keywords` | string[] | 否 / No | `null` | 关键词候选；为空使用当前上下文。（Optional keyword candidates.) |
| | `browserKey` | string | 否 / No | `""` → `"user"` | 浏览器配置键。（Browser profile key.) |
| | `behaviorProfile` | string | 否 / No | `""` → `"default"` | 行为档案。（Behavior profile.) |
| `xhs_like_current` | `browserKey` | string | 否 / No | `""` → `"user"` | 浏览器配置键。（Browser profile key.) |
| | `behaviorProfile` | string | 否 / No | `""` → `"default"` | 行为档案。（Behavior profile.) |
| `xhs_favorite_current` | `browserKey` | string | 否 / No | `""` → `"user"` | 浏览器配置键。（Browser profile key.) |
| | `behaviorProfile` | string | 否 / No | `""` → `"default"` | 行为档案。（Behavior profile.) |
| `xhs_comment_current` | `commentText` | string | 是 / Yes | — | 评论文本。（Comment text.) |
| | `browserKey` | string | 否 / No | `""` → `"user"` | 浏览器配置键。（Browser profile key.) |
| | `behaviorProfile` | string | 否 / No | `""` → `"default"` | 行为档案。（Behavior profile.) |
| `xhs_scroll_browse` | `browserKey` | string | 否 / No | `""` → `"user"` | 浏览器配置键。（Browser profile key.) |
| | `behaviorProfile` | string | 否 / No | `""` → `"default"` | 行为档案。（Behavior profile.) |

#### 数据采集工具（Data capture tools）

| 工具 (Tool) | 参数 (Parameter) | 类型 (Type) | 必填 (Required) | 默认值 (Default) | 说明 (Description) |
|---------------|------------------|-------------|-----------------|------------------|--------------------|
| `xhs_note_capture` | `keywords` | string[] | 否 / No | `null` | 关键词候选；为空时随机或画像推荐。（Keyword candidates.) |
| | `portraitId` | string | 否 / No | `""` | 画像 ID。（Portrait id.) |
| | `targetCount` | int | 否 / No | `20` | 采集数量上限（1-200）。（Collection limit.) |
| | `browserKey` | string | 否 / No | `""` → `"user"` | 浏览器配置键。（Browser profile key.) |
| | `behaviorProfile` | string | 否 / No | `""` → `"default"` | 行为档案。（Behavior profile.) |
| `xhs_capture_page_notes` | `targetCount` | int | 否 / No | `20` | 当前页面采集数量上限（1-200）。（Current page collection limit.) |
| | `browserKey` | string | 否 / No | `""` → `"user"` | 浏览器配置键。（Browser profile key.) |

#### 内容创作工具（`xhs_publish_note`）

| 参数 (Parameter) | 类型 (Type) | 必填 (Required) | 默认值 (Default) | 说明 (Description) |
|------------------|-------------|-----------------|------------------|--------------------|
| `imagePath` | string | 是 / Yes | — | 必须提供的图片路径（相对或绝对）。(Image path to upload.) |
| `noteTitle` | string | 否 / No | `""` | 空值使用默认标题模板。（Default title when empty.) |
| `noteContent` | string | 否 / No | `""` | 空值使用默认正文模板。（Default content when empty.) |
| `browserKey` | string | 否 / No | `""` → `"user"` | 浏览器配置键。（Browser profile key.) |
| `behaviorProfile` | string | 否 / No | `""` → `"default"` | 行为档案。（Behavior profile key.) |

#### 低级动作工具（`ll_execute`）

| 参数 (Parameter) | 类型 (Type) | 必填 (Required) | 默认值 (Default) | 说明 (Description) |
|------------------|-------------|-----------------|------------------|--------------------|
| `browserKey` | string | 否 / No | `""` → `"user"` | 浏览器配置键。（Browser profile key.) |
| `behaviorProfile` | string | 否 / No | `""` → `"default"` | 行为档案。（Behavior profile key.) |
| `actionType` | enum `HumanizedActionType` | 是 / Yes | — | 需要执行的动作类型。（Action type to execute.) |
| `target` | `ActionLocator` | 否 / No | `null` | 元素定位信息。（Element locator hints.) |
| `parameters` | `HumanizedActionParameters` | 否 / No | `null` | 附加参数（文本、滚动距离等）。（Additional parameters.) |
| `timing` | `HumanizedActionTiming` | 否 / No | `null` | 动作节奏控制。（Timing configuration.) |

## 核心架构

### 服务层次

```
MCP 工具层 (Tools/)
  ├─ BrowserTool              # 浏览器会话管理
  ├─ BehaviorFlowTool         # 行为流程编排（浏览/点赞/收藏/评论/发现页全链路）
  ├─ InteractionStepTool      # 业务交互步骤执行（8 个高级工具）
  ├─ LowLevelInteractionTool  # 低级交互动作执行（xhs_ll_execute）
  └─ NoteCaptureTool          # 笔记批量捕获

服务层 (Services/)
  ├─ Browser/
  │   ├─ BrowserAutomationService          # 页面导航、随机浏览
  │   ├─ PlaywrightSessionManager          # Playwright 会话管理
  │   ├─ Fingerprint/ProfileFingerprintManager  # 浏览器指纹管理
  │   └─ Network/NetworkStrategyManager    # 网络策略（代理、重试、缓解）
  ├─ Humanization/
  │   ├─ HumanizedActionService            # 人性化动作编排核心
  │   ├─ KeywordResolver                   # 关键词解析（候选词→画像→默认）
  │   ├─ HumanDelayProvider                # 延迟时间生成
  │   ├─ Behavior/DefaultBehaviorController # 行为控制器（根据档案生成动作序列）
  │   └─ Interactions/
  │       ├─ DefaultHumanizedActionScriptBuilder  # 动作脚本构建
  │       ├─ HumanizedInteractionExecutor         # 执行器（点击/输入/滚动/延迟）
  │       └─ InteractionLocatorBuilder            # 元素定位器构建
  └─ Notes/
      ├─ NoteEngagementService             # 笔记互动（点赞/收藏/评论）
      ├─ NoteCaptureService                # 笔记数据捕获
      └─ NoteRepository                    # 笔记数据存储

配置层 (Configuration/)
  ├─ XiaoHongShuOptions              # 默认关键词、画像、人性化节奏
  ├─ HumanBehaviorOptions            # 行为档案配置（default/cautious/aggressive）
  ├─ FingerprintOptions              # 浏览器指纹配置
  ├─ NetworkStrategyOptions          # 网络策略配置
  ├─ PlaywrightInstallationOptions   # Playwright 安装配置
  └─ VerificationOptions             # 验证运行配置

基础设施 (Infrastructure/)
  ├─ ToolExecution/                  # 工具执行结果封装
  └─ FileSystem/                     # 文件系统抽象
```

### 关键设计模式

- **依赖注入**：所有服务在 `ServiceCollectionExtensions.AddXiaoHongShuServer()` 中注册为单例
- **工具发现**：MCP 框架通过 `WithToolsFromAssembly()` 自动扫描 `[McpServerToolType]` 和 `[McpServerTool]` 标记
- **行为档案**：三种内置档案（默认/谨慎/激进），可通过 `behaviorProfile` 参数切换或自定义
- **会话缓存**：每个 `profileKey` 对应独立的 Playwright 上下文，避免重复初始化
- **关键词解析**：优先级为 `请求参数 → 画像标签 → 默认配置`

### 工具架构分层

本项目 MCP 工具分为两个抽象层次：

#### 业务工具层 (Business Tools)

面向常见小红书交互场景，封装完整业务流程：

- `xhs_random_browse`: 基于画像的随机浏览流程
- `xhs_keyword_browse`: 关键词驱动浏览并按策略互动
- `xhs_note_capture`: 拟人化逐条采集并导出笔记
- `xhs_publish_note`: 上传素材并暂存草稿
- `xhs_navigate_explore`: 导航到发现页
- `xhs_search_keyword`: 搜索关键词
- `xhs_select_note`: 选择笔记
- `xhs_like_current`: 点赞当前笔记
- `xhs_favorite_current`: 收藏当前笔记
- `xhs_comment_current`: 评论当前笔记
- `xhs_scroll_browse`: 拟人化滚动浏览

**特点**：
- ✅ 简单参数（`browserKey`、`behaviorProfile`）
- ✅ 自动编排动作序列（内置延迟和拟人化行为）
- ✅ 适合快速实现常见场景

#### 低级工具层 (Low-Level Tools)

直接操作浏览器交互动作，提供最大灵活性：

- `ll_execute`: 执行单个底层动作（支持 11 种 `HumanizedActionType`）
  - `Hover`: 鼠标悬停
  - `Click`: 点击元素
  - `MoveRandom`: 随机移动鼠标
  - `Wheel`: 滚轮滚动
  - `ScrollTo`: 滚动到目标位置
  - `InputText`: 输入文本
  - `PressKey`: 按键
  - `Hotkey`: 组合键
  - `WaitFor`: 等待元素出现
  - `Delay`: 延迟等待
  - `MoveToElement`: 移动到元素

**特点**：
- 🔧 细粒度控制（手动指定 `ActionLocator`、`HumanizedActionParameters`、`HumanizedActionTiming`）
- 🎯 适合高级用户和特殊场景
- ⚠️ 需要更多参数和配置

**推荐实践**：
- ✅ **优先使用业务工具层**：大多数场景可通过业务工具完成
- 🔧 **特殊场景使用低级工具**：需要精确控制交互细节时使用 `ll_execute`

## 快速开始（Quick Start）

### 首次运行流程图（First Run Flow）

```mermaid
graph TD
    A[安装 .NET 8 SDK / Install .NET 8 SDK] --> B[恢复依赖 / Restore dependencies]
    B --> C[配置 MCP 客户端 / Configure MCP clients]
    C --> D[启动服务器并触发 Playwright 安装 / Start server & trigger Playwright install]
    D --> E[验证工具列表 (--tools-list) / Verify tool list]
    E --> F[执行验证运行 (--verification-run) / Run verification]
    F --> G[按场景使用工具 / Use scenario workflows]
```

### Windows（PowerShell）

```pwsh
# 切换到项目目录
Set-Location <项目路径>\HushOps.Servers.XiaoHongShu

# 恢复依赖
dotnet restore

# 编译（默认 Debug）
dotnet build .\HushOps.Servers.XiaoHongShu.csproj

# 启动本地 MCP 服务器（保持会话）
dotnet run --project .\HushOps.Servers.XiaoHongShu.csproj
```

> Windows 路径使用反斜杠 `\`；如需后台常驻，可结合 `Start-Process` 或使用任务计划程序。（Use backslashes for Windows paths; Start-Process or Task Scheduler helps background execution.)

### Linux / macOS（Bash）

```bash
# 切换到项目目录
cd <项目路径>/HushOps.Servers.XiaoHongShu

# 恢复依赖
dotnet restore

# 编译（默认 Debug）
dotnet build ./HushOps.Servers.XiaoHongShu.csproj

# 启动本地 MCP 服务器
dotnet run --project ./HushOps.Servers.XiaoHongShu.csproj
```

> Linux/macOS 使用正斜杠 `/`；可配合 `nohup`、`systemd` 或 `tmux` 让服务器在后台运行。（Use forward slashes and nohup/systemd/tmux for background runs.)

### 验证命令（Verification commands）

```bash
# 列出所有工具，确认客户端成功加载
dotnet run --project <项目路径>/HushOps.Servers.XiaoHongShu.csproj -- --tools-list

# 执行端到端验证流程（检查浏览器会话/网络配置）
dotnet run --project <项目路径>/HushOps.Servers.XiaoHongShu.csproj -- --verification-run
```

- 预期输出包含 `STATUS: ok` 与 `TOOLS: ...`，若缺失请检查 MCP 客户端配置或服务器日志。（Expect STATUS: ok and tool names; otherwise review MCP client configuration and server logs.)
- `--verification-run` 会自动调用 Playwright 会话并访问状态页，可验证代理、重试策略及浏览器指纹配置。（Verification run exercises Playwright session, proxies, retries, and fingerprint normalization.)

### Playwright 自动安装说明（Playwright auto-install）

- 首次运行 `dotnet run` 时，若 Playwright 浏览器未缓存，服务器会调用 `Microsoft.Playwright.Program.Main("install")` 自动下载 Chromium 与 FFMPEG，并在日志输出进度。（Server auto-installs Chromium/FFMPEG on first run and logs progress.)
- 若希望复用缓存或镜像，可在配置文件设置 `playwrightInstallation.browsersPath` 与 `playwrightInstallation.downloadHost`。（Use shared cache/mirror via configuration.)
- CI/离线环境可手动执行 `Tools/install-playwright.ps1`（Windows）或 `Tools/install-playwright.sh`（Linux/macOS）；脚本支持 `--browser`、`--cache-path`、`--skip-if-present` 等参数。（Use bundled scripts in restricted environments.)
- 安装失败时，请手动运行 `pwsh bin/<Configuration>/<TFM>/playwright.ps1 install` 或 `./bin/<Configuration>/<TFM>/playwright.sh install` 并检查代理/防火墙。（Manual install commands help diagnose proxy/firewall issues.)

## MCP 客户端配置（MCP Client Configuration）

### 配置文件位置与加载方式（Configuration locations & loading）

- **Claude Desktop**：
  - Windows：`%APPDATA%\Claude\claude_desktop_config.json`
  - macOS：`~/Library/Application Support/Claude/claude_desktop_config.json`
  - Linux：`~/.config/Claude/claude_desktop_config.json`
  - 保存即热加载；若未生效，可在 `Claude > Developer > Reload Config` 中手动刷新或重新启动应用。（Config reloads on save; use Developer > Reload Config or restart when in doubt.)
- **Cline（VS Code 扩展）**：
  - Windows：`%APPDATA%\Code\User\globalStorage\saoudrizwan.claude-dev\settings\cline_mcp_settings.json`
  - macOS：`~/Library/Application Support/Code/User/globalStorage/saoudrizwan.claude-dev/settings/cline_mcp_settings.json`
  - Linux：`~/.config/Code/User/globalStorage/saoudrizwan.claude-dev/settings/cline_mcp_settings.json`
  - 修改后使用命令面板 `Cline: Manage MCP Servers` → `Reload` 或重启 VS Code 以加载新配置。（Reload via command palette or restart VS Code.)
- **Cursor**：
  - 项目级：`<项目路径>/.cursor/mcp.json`（推荐，便于团队共享）
  - 用户级：Windows `%USERPROFILE%\.cursor\mcp.json`，Linux/macOS `~/.cursor/mcp.json`
  - Cursor 在启动或保存配置文件时即时重新加载，可在设置页 `Tools & Integrations > MCP` 查看状态。（Cursor watches files and exposes status under Tools & Integrations.)

### Claude Desktop 配置示例（Claude Desktop example）

```json
{
  "mcpServers": {
    "xiao-hong-shu": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "<项目路径>/HushOps.Servers.XiaoHongShu/HushOps.Servers.XiaoHongShu.csproj"
      ],
      "env": {
        "DOTNET_ENVIRONMENT": "Production"
      }
    }
  }
}
```

- Windows 需将路径改为 `D:/...` 或使用转义的反斜杠；macOS/Linux 直接使用 `/`。（Adjust path separators per OS.)
- 保存后在 Claude Desktop 底部状态栏应出现 “Connected to MCP server: xiao-hong-shu”。（Expect status message confirming connection.)

### Cline（VS Code）配置示例

在 `cline_mcp_settings.json` 中加入：

```json
{
  "mcpServers": {
    "xiao-hong-shu": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "<项目路径>/HushOps.Servers.XiaoHongShu/HushOps.Servers.XiaoHongShu.csproj"
      ],
      "disabled": false,
      "metadata": {
        "category": "automation"
      }
    }
  }
}
```

> 若文件已存在 `mcpServers`，仅需追加服务器条目即可；保存后执行 `Cline: Manage MCP Servers` → 检查 `Installed` 标签是否显示 `xiao-hong-shu (running)`。（Append entry if the object already exists; verify status via Cline panel.)

### Cursor 配置示例

项目目录下创建或更新 `.cursor/mcp.json`：

```json
{
  "mcpServers": {
    "xiao-hong-shu": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "<项目路径>/HushOps.Servers.XiaoHongShu/HushOps.Servers.XiaoHongShu.csproj"
      ],
      "cwd": "<项目路径>/HushOps.Servers.XiaoHongShu",
      "env": {
        "DOTNET_ENVIRONMENT": "Production"
      }
    }
  }
}
```

> Cursor 会优先读取项目级配置；若找不到则回退到用户级配置。可以通过命令 `cursor-agent mcp list` 或在 `Settings > Tools & Integrations > MCP` 页面查看连接状态。（Use cursor-agent CLI or settings UI to verify active servers.)

### 故障排查与验证（Troubleshooting & validation）

1. 使用 `dotnet run -- --tools-list` 确认服务器能够返回工具列表。（Ensure server responds with tool catalogue.)
2. 查看客户端日志：
   - Claude Desktop：`View > Toggle Developer Tools` → Console 中应看到 `Connected to MCP server`。（Check devtools console for connection logs.)
   - Cline：打开 `Cline Output` 面板或 `Cline: Manage MCP Servers`，状态为绿色表示连接成功。（Ensure status indicator is running.)
   - Cursor：`cursor-agent mcp list` 或设置页的状态标签应为 `connected`。（CLI or settings should show connected.)
3. 若工具列表为空，确认命令路径、`dotnet` 是否在环境变量中，以及服务器是否已完成 Playwright 安装。（Validate command path, PATH, and Playwright installation state.)
4. 完成配置后执行 `dotnet run -- --verification-run` 以验证浏览器、代理和指纹模块是否工作正常。（Verification run validates browser/proxy/fingerprint modules.)

## 配置系统

### 配置加载优先级

配置按以下优先级加载（后者覆盖前者）：
1. 代码默认值
2. `appsettings.json`（可选）
3. `config/xiao-hong-shu.json`（可选）
4. 环境变量（前缀 `HUSHOPS_XHS_SERVER_`）

### 配置节说明

| 配置节 | 环境变量前缀 | 描述 |
|-------|-------------|------|
| `xhs` | `HUSHOPS_XHS_SERVER_XHS__` | 默认关键词、画像、人性化节奏 |
| `humanBehavior` | `HUSHOPS_XHS_SERVER_HumanBehavior__` | 行为档案配置 |
| `fingerprint` | `HUSHOPS_XHS_SERVER_Fingerprint__` | 浏览器指纹配置 |
| `networkStrategy` | `HUSHOPS_XHS_SERVER_NetworkStrategy__` | 网络策略配置 |
| `playwrightInstallation` | `HUSHOPS_XHS_SERVER_PlaywrightInstallation__` | Playwright 安装配置 |
| `verification` | `HUSHOPS_XHS_SERVER_Verification__` | 验证运行配置 |

### 核心配置示例

#### 1. 基础配置（`xhs` 节）

```json
{
  "xhs": {
    "defaultKeyword": "旅行攻略",
    "humanized": {
      "minDelayMs": 800,
      "maxDelayMs": 2600,
      "jitter": 0.2
    },
    "portraits": [
      {
        "id": "travel-lover",
        "tags": ["旅行", "美食", "摄影"],
        "metadata": {
          "category": "lifestyle",
          "region": "asia"
        }
      }
    ]
  }
}
```

#### 2. 行为档案配置（`humanBehavior` 节）

```json
{
  "humanBehavior": {
    "defaultProfile": "default",
    "profiles": {
      "default": {
        "preActionDelay": { "minMs": 250, "maxMs": 600 },
        "postActionDelay": { "minMs": 220, "maxMs": 520 },
        "typingInterval": { "minMs": 80, "maxMs": 200 },
        "scrollDelay": { "minMs": 260, "maxMs": 720 },
        "maxScrollSegments": 2,
        "hesitationProbability": 0.12,
        "clickJitter": { "minPx": 1, "maxPx": 4 },
        "mouseMoveSteps": { "min": 12, "max": 28 },
        "mouseVelocity": { "min": 280, "max": 820 },
        "randomIdleProbability": 0.1,
        "randomIdleDuration": { "minMs": 420, "maxMs": 960 },
        "requireProxy": false,
        "allowAutomationIndicators": false
      },
      "cautious": {
        "preActionDelay": { "minMs": 420, "maxMs": 820 },
        "postActionDelay": { "minMs": 360, "maxMs": 780 },
        "hesitationProbability": 0.22,
        "randomIdleProbability": 0.2
      },
      "aggressive": {
        "preActionDelay": { "minMs": 120, "maxMs": 280 },
        "postActionDelay": { "minMs": 140, "maxMs": 320 },
        "hesitationProbability": 0.05,
        "randomIdleProbability": 0.05
      }
    }
  }
}
```

**行为档案参数说明**：
- `preActionDelay`/`postActionDelay`：动作前后延迟范围（毫秒）
- `typingInterval`：输入字符间隔（毫秒）
- `scrollDelay`：滚动延迟（毫秒）
- `maxScrollSegments`：最大滚动分段数
- `hesitationProbability`：犹豫概率（0-1）
- `clickJitter`：点击位置抖动像素范围
- `mouseMoveSteps`：鼠标移动步数范围
- `mouseVelocity`：鼠标移动速度（像素/秒）
- `randomIdleProbability`：随机停顿概率
- `randomIdleDuration`：随机停顿时长范围（毫秒）
- `requireProxy`：是否强制要求代理
- `allowAutomationIndicators`：是否允许自动化检测特征

#### 3. 环境变量配置示例

```bash
# Windows
set HUSHOPS_XHS_SERVER_XHS__DefaultKeyword=旅行攻略
set HUSHOPS_XHS_SERVER_XHS__Humanized__MinDelayMs=800
set HUSHOPS_XHS_SERVER_XHS__Humanized__MaxDelayMs=2600
set HUSHOPS_XHS_SERVER_HumanBehavior__DefaultProfile=cautious

# Linux/macOS
export HUSHOPS_XHS_SERVER_XHS__DefaultKeyword="旅行攻略"
export HUSHOPS_XHS_SERVER_XHS__Humanized__MinDelayMs=800
export HUSHOPS_XHS_SERVER_XHS__Humanized__MaxDelayMs=2600
export HUSHOPS_XHS_SERVER_HumanBehavior__DefaultProfile=cautious
```

## 使用指南

### 场景 1：快速上手（Quick onboarding）

- 目标（Goal）：打开浏览器、完成一次登录并触发随机浏览，确认人性化动作链路正常。
- 准备（Setup）：建议先运行 `dotnet run -- --tools-list` 确认 MCP 工具已注册；若使用独立配置，请清理旧缓存目录。

```json
// 1) 启动用户浏览器配置（自动探测 Chrome/Edge）
{
  "tool": "browser_open",
  "arguments": {
    "profileKey": "user",
    "profilePath": ""
  }
}

// 2) 执行随机浏览（画像 travel-lover 将补全关键词）
{
  "tool": "xhs_random_browse",
  "arguments": {
    "portraitId": "travel-lover",
    "browserKey": "user",
    "behaviorProfile": "default"
  }
}
```

> 浏览器弹出后手动登录一次；后续会话会复用已登录状态。（Log in manually on first launch; sessions persist afterwards.)

### 场景 2：关键词搜索并互动（Keyword search & engagement）

- 目标：按关键词搜索 → 选择笔记 → 点赞收藏，验证交互步骤工具的串联。
- 提示：可根据需要在 `behaviorProfile` 中设置 `cautious` 以增加停顿。

```json
// 1) 在发现页搜索关键词
{
  "tool": "xhs_search_keyword",
  "arguments": {
    "keyword": "旅行攻略",
    "browserKey": "user",
    "behaviorProfile": "cautious"
  }
}

// 2) 选中匹配笔记（命中即进入详情页）
{
  "tool": "xhs_select_note",
  "arguments": {
    "keywords": ["旅行攻略", "亲子出行"],
    "browserKey": "user",
    "behaviorProfile": "cautious"
  }
}

// 3) 点赞并收藏当前笔记
{
  "tool": "xhs_like_current",
  "arguments": {
    "browserKey": "user",
    "behaviorProfile": "cautious"
  }
}
{
  "tool": "xhs_favorite_current",
  "arguments": {
    "browserKey": "user",
    "behaviorProfile": "cautious"
  }
}
```

> 若需要评论，可追加调用 `xhs_comment_current` 并设置 `commentText`。（Add `xhs_comment_current` with `commentText` for comments.)

### 场景 3：批量数据采集（Bulk note capture）

- 目标：按关键词采集指定数量的笔记，并导出 CSV/JSON。
- 建议：首次运行前执行场景 1 以确保登录状态有效。

```json
// 使用 xhs_note_capture 执行拟人化采集
{
  "tool": "xhs_note_capture",
  "arguments": {
    "keywords": ["露营", "户外装备"],
    "targetCount": 40,
    "portraitId": "outdoor-maker",
    "includeAnalytics": true,
    "includeRaw": true,
    "browserKey": "analysis-bot",
    "behaviorProfile": "default"
  }
}
```

- 输出（Outputs）：`logs/note-capture/` 下自动生成 CSV 与可选原始 JSON，文件名包含关键词与时间戳。
- 当前页面无需导航时，可改用 `xhs_capture_page_notes`，仅需设置 `targetCount` 与 `browserKey`。

### 场景 4：发布笔记（Draft publishing）

- 目标：上传图片、填写标题正文并保存草稿，验证发布流程。
- 注意：图片路径需为本机可访问路径，支持相对路径（相对服务器工作目录）。

```json
// 使用 xhs_publish_note 创建草稿
{
  "tool": "xhs_publish_note",
  "arguments": {
    "imagePath": "<项目路径>/storage/samples/cover.jpg",
    "noteTitle": "秋季露营装备清单",
    "noteContent": "这是一份包含帐篷、睡袋、保暖装备的清单……",
    "browserKey": "creator-profile",
    "behaviorProfile": "default"
  }
}
```

> 成功调用后日志会输出 “已暂存并离开发布页面”，并在浏览器页面显示草稿已保存。（Logs will display the saved-draft message; browser UI should confirm draft saved.)

### 高级用法（Advanced usage）

#### 自定义行为档案（Custom behavior profiles）

```json
{
  "humanBehavior": {
    "profiles": {
      "my-custom-profile": {
        "preActionDelay": { "minMs": 300, "maxMs": 700 },
        "postActionDelay": { "minMs": 320, "maxMs": 820 },
        "hesitationProbability": 0.15,
        "requireProxy": true,
        "allowedProxyPrefixes": ["socks5://"]
      }
    }
  }
}
```

> 将 `behaviorProfile` 设置为 `my-custom-profile` 即可生效；若启用代理请确保在网络策略中同步配置。（Set behaviorProfile to the new key; configure matching proxy strategy when required.)

#### 使用画像（Portrait-driven keywords）

```json
{
  "xhs": {
    "portraits": [
      {
        "id": "tech-enthusiast",
        "tags": ["科技", "数码", "编程", "AI"],
        "metadata": {
          "interest_level": "high",
          "region": "china"
        }
      }
    ]
  }
}
```

> 调用时指定 `portraitId`，工具会从画像标签中随机选取关键词并在日志中标注来源。（Specify portraitId to reuse portrait tags; logs include keyword sources.)

#### 多账号隔离（Multi-account isolation）

```json
// 账号 A
{
  "tool": "browser_open",
  "arguments": {
    "profileKey": "account-a"
  }
}

// 账号 B
{
  "tool": "browser_open",
  "arguments": {
    "profileKey": "account-b"
  }
}
```

> 每个 `profileKey` 都会创建独立的浏览器目录与 Playwright 会话，适合 A/B 测试或多人共用同一服务器。（Each profileKey maps to an isolated browser context for safe multi-account workflows.)

## 测试与质量

### 运行测试

```bash
# 运行所有测试
dotnet test Tests/HushOps.Servers.XiaoHongShu.Tests/HushOps.Servers.XiaoHongShu.Tests.csproj

# 运行 Release 模式测试
dotnet test -c Release

# 运行特定测试
dotnet test --filter "FullyQualifiedName~HumanizedActionServiceTests"
```

### 验证运行

验证运行会执行示例浏览器流程并访问状态码端点，用于 CI/CD 或首次部署后的快速验证：

```bash
dotnet run -- --verification-run
```

**配置选项**：
- `verification.statusUrl`：状态码端点 URL（默认 `https://httpbin.org/status/429`）
- `verification.mockStatusCode`：本地拦截并返回的状态码（可选）
- 端点不可达时会记录警告，但不会导致程序退出失败

### 质量标准

- **测试覆盖率**：目标 70%
- **代码风格**：遵循 .NET 默认规则（四空格缩进、PascalCase 公共成员、`_camelCase` 私有字段）
- **编译警告**：`TreatWarningsAsErrors` 为 true，禁止提交时存在编译警告
- **可空引用**：启用 `Nullable` 引用类型，所有可空类型必须显式标注

## 开发与贡献

### 开发约定

- **代码风格**：遵循 .NET 默认规则（四空格缩进、PascalCase 公共成员、`_camelCase` 私有字段）
- **提交信息**：推荐使用 Conventional Commits（如 `refactor(config): ...`）或简洁中文摘要
- **文档同步**：代码变更必须同步更新文档，参考 [`CLAUDE.md`](./CLAUDE.md) 中的规范
- **测试先行**：提交前必须通过所有测试，推荐编写单元测试覆盖新功能

### 贡献流程

1. **Fork 并创建分支**
   ```bash
   git checkout -b feature/update-docs
   ```

2. **编写代码和测试**
   - 按照代码风格规范编写
   - 补充单元测试（目标覆盖率 70%）
   - 更新相关文档

3. **本地验证**
   ```bash
   # 构建项目
   dotnet build

   # 运行测试
   dotnet test

   # 验证运行（可选）
   dotnet run -- --verification-run
   ```

4. **提交 Pull Request**
   - 附上变更摘要
   - 提供测试结果截图
   - 关联相关 Issue
   - 请求熟悉模块的审阅者

### 项目结构说明

```
HushOps.Servers.XiaoHongShu/
├── Configuration/           # 配置选项类
├── Infrastructure/          # 基础设施（文件系统、工具执行封装）
├── Services/
│   ├── Browser/            # 浏览器自动化、指纹管理、网络策略
│   ├── Humanization/       # 人性化动作编排、行为控制、关键词解析
│   ├── Notes/              # 笔记互动、数据捕获、仓储
│   └── Logging/            # MCP 日志桥接
├── Tools/                  # MCP 工具暴露层
├── storage/                # 本地存储（浏览器配置、笔记数据、导出文件）
├── Tests/                  # 单元测试和集成测试
└── docs/                   # 项目文档（架构、设计决策、实现日志）
```

## 常见问题（FAQ）

### Q1: 工具列表为空怎么办？（Tools list is empty）
- 确认服务器正在运行：在项目目录执行 `dotnet run -- --tools-list`，终端应返回 JSON 工具列表。
- 校验客户端命令：确保配置中的 `command` 与 `args` 指向正确的 `.csproj` 或发布目录，并与操作系统路径分隔符一致。
- 检查客户端日志：Claude Desktop 的开发者工具、Cline 的 Output 面板或 Cursor 的 MCP 设置页若显示 `connection refused`，多为进程未启动或端口被防火墙拦截。
- 若问题持续，可删除 `storage/browser-profiles/` 下的缓存后重试，以排除损坏的会话目录。

### Q2: 参数类型错误（string? vs string）怎么办？（Handling string vs string? mismatches）
- 自 v1.1.0 起所有字符串参数均为非空 `string`，客户端不应再发送 `null`。
- 可选字段留空时应传递 `""`，服务器会根据归一化规则自动填入默认值（如 `browserKey` → `user`）。
- 若已有旧版配置，可运行 `dotnet run -- --tools-list` 观察警告：日志会指出哪个参数被解析为 `null`。请在配置文件中将 `null` 替换为 `""`。

### Q3: Playwright 浏览器未安装或反复下载？（Playwright missing or re-installing）
- 首次运行会自动触发 `Microsoft.Playwright.Program.Main("install")`；若日志显示下载失败，请检查代理、防火墙或镜像源。
- 手动安装：
  - Windows：`pwsh Tools/install-playwright.ps1 -SkipIfPresent`
  - Linux/macOS：`bash Tools/install-playwright.sh --skip-if-present`
- 可在 `config/xiao-hong-shu.json` 指定 `playwrightInstallation.browsersPath` 指向共享缓存目录，或设置 `playwrightInstallation.downloadHost` 使用内网镜像。
- 若需清理残留下载，删除 `%LOCALAPPDATA%/ms-playwright`（Windows）或 `~/.cache/ms-playwright` 后重新执行安装脚本。

### Q4: FingerprintBrowser 依赖报错怎么办？（FingerprintBrowser dependency issues）
- 仓库根目录需要存在兄弟项目 `../FingerprintBrowser/` 并完成 `dotnet restore`；构建失败多因缺少该项目的输出。
- 运行 `dotnet build ..\FingerprintBrowser\FingerprintBrowser.csproj`（Windows）或 `dotnet build ../FingerprintBrowser/FingerprintBrowser.csproj`（Linux/macOS）以确认依赖可编译。
- 若运行时报 `IFingerprintBrowser` 未注册，请检查 `ServiceCollectionExtensions` 是否被调用（保持使用提供的 `Program.cs` 模板），并确保 `DOTNET_ENVIRONMENT` 未禁用默认配置。
- 验证修复：执行 `dotnet run -- --verification-run`，日志中应包含指纹加载成功的条目。

### Q5: 如何调试 MCP 通信？（Debugging MCP communication）
- 服务器侧：设置环境变量 `DOTNET_ENVIRONMENT=Development` 后运行，可获得更详细的日志；同时关注 `logs/` 和控制台输出。
- 客户端侧：
  - Claude Desktop：`View > Toggle Developer Tools`，筛选 `mcp` 关键字查看连接与请求日志。
  - Cline：执行 `Cline: Manage MCP Servers`，在 `Installed` 标签查看状态；如需抓包，可开启 VS Code Output → Cline。
  - Cursor：运行 `cursor-agent mcp list`，确认 `status` 为 `connected`。
- 双向验证：使用 `dotnet run -- --tools-list` 比对服务器返回的工具数与客户端面板展示是否一致。

### Q6: 如何使用自定义配置文件？（Using custom configuration files）
- 推荐在项目根目录创建 `config/xiao-hong-shu.json`，该文件会覆盖默认值；可按需拆分为多个 JSON 并使用 `jq`/脚本在部署时合并。
- 若需要环境区分，可设置环境变量 `DOTNET_ENVIRONMENT=Production|Staging` 并提供 `appsettings.{ENV}.json`；默认 `Host.CreateDefaultBuilder` 会自动加载匹配文件。
- 运行时临时覆盖使用环境变量前缀 `HUSHOPS_XHS_SERVER_`，例如 `HUSHOPS_XHS_SERVER_XHS__DefaultKeyword=球鞋`。
- 修改配置后无需重启客户端，但需重新启动服务器以载入最新设置（或在托管环境中使用热重载机制）。

## 许可证

项目采用 [Apache-2.0](./LICENSE) 许可证。欢迎在遵循许可证与平台条款的前提下复用与扩展。

## 支持

- 🐛 问题反馈：提交 Issue 至仓库所属团队
- 💡 功能建议：通过讨论区或 PR 附议
- 📧 联系方式：1317578863@qq.com

> 如果本项目对你有帮助，欢迎 star 支持！
