# HushOps.Servers.XiaoHongShu

> 基于 .NET 8 的 Model Context Protocol（MCP）本地 stdio 服务器，为小红书平台提供人性化自动化工具集。

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![MCP](https://img.shields.io/badge/MCP-stdio--only-FF6B6B)](https://modelcontextprotocol.io/)
[![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](./LICENSE)

## 项目概述

- **MCP 工具协议**：基于 stdio 通信的 MCP 服务器，通过 `ModelContextProtocol.Server` 托管并自动发现程序集内标记的工具
- **人性化行为编排**：多层次拟人化系统，支持行为档案配置（默认/谨慎/激进）、随机延迟抖动、滚动/点击/输入模拟，确保操作流畅自然
- **完整交互流程**：涵盖随机浏览、关键词搜索、发现页导航、笔记点赞、收藏、评论、批量捕获等完整工作流
- **浏览器指纹管理**：支持自定义 User-Agent、时区、语言、视口、设备缩放、触摸屏等指纹参数，每个配置独立缓存
- **网络策略控制**：可配置代理、出口 IP、请求延迟、重试策略、缓解措施等，应对反爬虫检测
- **灵活配置模式**：支持用户浏览器配置（自动探测路径）和独立配置（隔离环境），可通过 JSON 或环境变量配置

## 快速开始（Quick Start）

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

## 依赖说明（Dependency Notes）

### 依赖概览（Dependency Overview）
- 仓库默认附带 FingerprintBrowser 运行时依赖的预编译动态链接库，位于 `libs/` 目录：`FingerprintBrowser.dll` 以及按需提供的 `FingerprintBrowser.pdb`、`FingerprintBrowser.xml`。（The repository ships the prebuilt FingerprintBrowser runtime under `libs/`, including the DLL and optional PDB/XML files.）
- FingerprintBrowser 不再通过 NuGet 包或 LocalFeed 发布；保持 `libs/` 目录原样即可使用。若目录缺失，请向维护团队索取最新压缩包并重新解压覆盖。（FingerprintBrowser is no longer distributed via NuGet or LocalFeed; keep the bundled `libs/` folder in place or re-extract it from the official archive when missing.）
- `HushOps.Servers.XiaoHongShu.csproj` 已固定引用 `libs/FingerprintBrowser.dll`；请勿删除 `<HintPath>`，否则构建与运行都会缺失浏览器能力。（The `.csproj` pins the dependency through `<HintPath>libs\\FingerprintBrowser.dll</HintPath>`; removing it breaks build/runtime.）

### 指纹浏览器分发（FingerprintBrowser Distribution）
1. 运行前确认 `libs/` 目录存在 FingerprintBrowser DLL 及其配套文件；若缺失请重新解压官方交付包。（Check the `libs/` folder for the DLL bundle before running; re-unpack the official delivery if files are missing.）
2. 恢复依赖与编译仅需执行常规 `dotnet restore` / `dotnet build`；无需 `dotnet pack`，也无需配置 LocalFeed。（Standard `dotnet restore` / `dotnet build` is enough; no `dotnet pack` or LocalFeed configuration is required.）
3. 收到新的 FingerprintBrowser 版本时，替换 `libs/` 中的 DLL（及配套 PDB/XML），然后重启服务加载新版本。（When an update arrives, overwrite the DLL (and optional PDB/XML) inside `libs/` and restart the service.）

### 常见问题（FAQ）
- **Q: 编译报错“找不到 HushOps.FingerprintBrowser”怎么办？（Why does build fail with “missing HushOps.FingerprintBrowser”？）**
  - A: 检查 `libs/FingerprintBrowser.dll` 是否存在且未被安全工具隔离，并确认 `.csproj` 中 `<HintPath>libs\\FingerprintBrowser.dll</HintPath>` 仍保留；必要时重新解压官方 `libs/` 目录。（Verify the DLL exists, is not quarantined, and the `<HintPath>` in the project file remains. Re-extract the bundled `libs/` folder if needed.）
- **Q: 如何确认 FingerprintBrowser 的版本？（How do I verify the FingerprintBrowser version?）**
  - A: 查看 `libs/FingerprintBrowser.dll` 的文件属性或随包提供的变更记录；运行时日志也会打印已加载的版本号。（Inspect the DLL file metadata or the provided changelog; runtime logs echo the loaded version.）
- **Q: 可以改回使用 LocalFeed 或自行打包 DLL 吗？（Can I switch back to LocalFeed or build my own DLL?）**
  - A: 当前只支持维护团队提供的预编译 DLL，已停止发布源码与 LocalFeed；如需定制版，请联系维护者协调。（Only the maintainer-supplied prebuilt DLL is supported; LocalFeed/source distribution is no longer available. Reach out to maintainers for special builds.）

## 使用教程

### 工具清单（Tool Catalog）

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

#### 参数说明（Parameter Reference）

> ⚠️ v1.1.0 (2025-10-02)：所有 MCP 工具的字符串参数类型从 `string?` 统一调整为非空 `string`，默认值为空字符串 `""`，以提升序列化兼容性。（All string parameters are now non-nullable `string` with default `""` for better serialization.)

**常用归一化规则（Normalization rules）**
- `browserKey: ""` → 自动归一化为 `"user"`；其它值会在 `storage/browser-profiles/<browserKey>` 下创建独立配置。（Empty browserKey normalizes to `user`; other values map to isolated profile directories.)
- `behaviorProfile: ""` → 自动归一化为 `"default"`。（Empty behaviorProfile normalizes to `default`.)
- `profilePath: ""` → `browser_open` 自动探测本地浏览器配置路径，仅 `user` 模式可手动指定。（Empty profilePath auto-detects; explicit path only allowed in user mode.)

下表列出各工具参数，`必填` 表示是否必须提供非空值；留空时将使用默认值并按照上述规则归一化。（Tables describe tool parameters; Required indicates if a non-empty value is mandatory.)

##### `browser_open`

| 参数 (Parameter) | 类型 (Type) | 必填 (Required) | 默认值 (Default) | 说明 (Description) |
|------------------|-------------|-----------------|------------------|--------------------|
| `profilePath` | string | 否 / No | `""` | 用户浏览器配置目录；为空时自动探测。（Local profile path; auto-detected when empty.) |
| `profileKey` | string | 否 / No | `""` → `"user"` | 用户模式或隔离模式键。（Profile key for user vs isolated profiles.) |

##### `xhs_random_browse`

| 参数 (Parameter) | 类型 (Type) | 必填 (Required) | 默认值 (Default) | 说明 (Description) |
|------------------|-------------|-----------------|------------------|--------------------|
| `keywords` | string[] | 否 / No | `null` | 关键词候选；为空时根据画像或默认配置选择。（Keyword candidates; falls back to portrait/default.) |
| `portraitId` | string | 否 / No | `""` | 画像 ID；为空使用全局画像。（Portrait identifier.) |
| `browserKey` | string | 否 / No | `""` → `"user"` | 浏览器配置键。（Browser profile key.) |
| `behaviorProfile` | string | 否 / No | `""` → `"default"` | 行为档案键。（Behavior profile key.) |

##### `xhs_keyword_browse`

| 参数 (Parameter) | 类型 (Type) | 必填 (Required) | 默认值 (Default) | 说明 (Description) |
|------------------|-------------|-----------------|------------------|--------------------|
| `keywords` | string[] | 否 / No | `null` | 关键词数组；为空时回退到画像或默认关键词。（Keyword list; falls back when empty.) |
| `portraitId` | string | 否 / No | `""` | 画像 ID；用于关键词兜底。（Portrait fallback.) |
| `browserKey` | string | 否 / No | `""` → `"user"` | 浏览器配置键。（Browser profile key.) |
| `behaviorProfile` | string | 否 / No | `""` → `"default"` | 行为档案键。（Behavior profile key.) |

##### 交互步骤工具（Interaction step tools）

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

##### 数据采集工具（Data capture tools）

| 工具 (Tool) | 参数 (Parameter) | 类型 (Type) | 必填 (Required) | 默认值 (Default) | 说明 (Description) |
|---------------|------------------|-------------|-----------------|------------------|--------------------|
| `xhs_note_capture` | `keywords` | string[] | 否 / No | `null` | 关键词候选；为空时随机或画像推荐。（Keyword candidates.) |
| | `portraitId` | string | 否 / No | `""` | 画像 ID。（Portrait id.) |
| | `targetCount` | int | 否 / No | `20` | 采集数量上限（1-200）。（Collection limit.) |
| | `browserKey` | string | 否 / No | `""` → `"user"` | 浏览器配置键。（Browser profile key.) |
| | `behaviorProfile` | string | 否 / No | `""` → `"default"` | 行为档案。（Behavior profile.) |
| `xhs_capture_page_notes` | `targetCount` | int | 否 / No | `20` | 当前页面采集数量上限（1-200）。（Current page collection limit.) |
| | `browserKey` | string | 否 / No | `""` → `"user"` | 浏览器配置键。（Browser profile key.) |

##### 内容创作工具（`xhs_publish_note`）

| 参数 (Parameter) | 类型 (Type) | 必填 (Required) | 默认值 (Default) | 说明 (Description) |
|------------------|-------------|-----------------|------------------|--------------------|
| `imagePath` | string | 是 / Yes | — | 必须提供的图片路径（相对或绝对）。(Image path to upload.) |
| `noteTitle` | string | 否 / No | `""` | 空值使用默认标题模板。（Default title when empty.) |
| `noteContent` | string | 否 / No | `""` | 空值使用默认正文模板。（Default content when empty.) |
| `browserKey` | string | 否 / No | `""` → `"user"` | 浏览器配置键。（Browser profile key.) |
| `behaviorProfile` | string | 否 / No | `""` → `"default"` | 行为档案。（Behavior profile key.) |

##### 低级动作工具（`ll_execute`）

| 参数 (Parameter) | 类型 (Type) | 必填 (Required) | 默认值 (Default) | 说明 (Description) |
|------------------|-------------|-----------------|------------------|--------------------|
| `browserKey` | string | 否 / No | `""` → `"user"` | 浏览器配置键。（Browser profile key.) |
| `behaviorProfile` | string | 否 / No | `""` → `"default"` | 行为档案。（Behavior profile key.) |
| `actionType` | enum `HumanizedActionType` | 是 / Yes | — | 需要执行的动作类型。（Action type to execute.) |
| `target` | `ActionLocator` | 否 / No | `null` | 元素定位信息。（Element locator hints.) |
| `parameters` | `HumanizedActionParameters` | 否 / No | `null` | 附加参数（文本、滚动距离等）。（Additional parameters.) |
| `timing` | `HumanizedActionTiming` | 否 / No | `null` | 动作节奏控制。（Timing configuration.) |

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

## MCP 客户端配置（MCP Client Configuration）

### 配置文件位置与加载方式（Configuration locations & loading）

- **Claude Desktop**：
  - Windows：`%APPDATA%\Claude\claude_desktop_config.json`
  - macOS：`~/Library/Application Support/Claude/claude_desktop_config.json`
  - Linux：`~/.config/Claude/claude_desktop_config.json`
  - 保存即热加载；若未生效，可在 `Claude > Developer > Reload Config` 中手动刷新或重新启动应用。（Config reloads on save; use Developer > Reload Config or restart when in doubt.)

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
- 保存后在 Claude Desktop 底部状态栏应出现 "Connected to MCP server: xiao-hong-shu"。（Expect status message confirming connection.)
- 验证连接：使用 `dotnet run -- --tools-list` 确认服务器能够返回工具列表。（Ensure server responds with tool catalogue.)
- 查看连接日志：`View > Toggle Developer Tools` → Console 中应看到 `Connected to MCP server`。（Check devtools console for connection logs.)
- 若工具列表为空，确认命令路径、`dotnet` 是否在环境变量中，以及服务器是否已完成 Playwright 安装。（Validate command path, PATH, and Playwright installation state.)
- 完成配置后执行 `dotnet run -- --verification-run` 以验证浏览器、代理和指纹模块是否工作正常。（Verification run validates browser/proxy/fingerprint modules.)

## 开发者文档

- [贡献指南](./docs/CONTRIBUTING.md)：编码规范、测试策略、贡献流程
- [配置指南](./docs/configuration.md)：详细配置说明、高级配置场景
- [架构设计](./CLAUDE.md)：核心架构、服务层次、设计模式

## 配置系统

配置来源遵循“代码默认值 → appsettings.json → config/xiao-hong-shu.json → 环境变量（前缀 `HUSHOPS_XHS_SERVER_`）”的优先级，后者会覆盖前者；推荐在仓库中维护基础配置，再用环境变量覆盖部署差异。

以下示例给出 `xhs` 节的最小配置，便于快速验证：

```json
{
  "xhs": {
    "defaultKeyword": "旅行攻略",
    "humanized": {
      "minDelayMs": 800,
      "maxDelayMs": 2600,
      "jitter": 0.2
    }
  }
}
```

更多字段说明、环境变量映射与高级配置案例请查阅 [配置指南](./docs/configuration.md)。

## 常见问题（FAQ）

#### Q1: 工具列表为空怎么办？（Tools list is empty）
- 确认服务器正在运行：在项目目录执行 `dotnet run -- --tools-list`，终端应返回 JSON 工具列表。
- 校验客户端命令：确保配置中的 `command` 与 `args` 指向正确的 `.csproj` 或发布目录，并与操作系统路径分隔符一致。
- 检查客户端日志：Claude Desktop 的开发者工具、Cline 的 Output 面板或 Cursor 的 MCP 设置页若显示 `connection refused`，多为进程未启动或端口被防火墙拦截。
- 若问题持续，可删除 `storage/browser-profiles/` 下的缓存后重试，以排除损坏的会话目录。

#### Q2: 参数类型错误（string? vs string）怎么办？（Handling string vs string? mismatches）
- 自 v1.1.0 起所有字符串参数均为非空 `string`，客户端不应再发送 `null`。
- 可选字段留空时应传递 `""`，服务器会根据归一化规则自动填入默认值（如 `browserKey` → `user`）。
- 若已有旧版配置，可运行 `dotnet run -- --tools-list` 观察警告：日志会指出哪个参数被解析为 `null`。请在配置文件中将 `null` 替换为 `""`。

#### Q3: Playwright 浏览器未安装或反复下载？（Playwright missing or re-installing）
- 首次运行会自动触发 `Microsoft.Playwright.Program.Main("install")`；若日志显示下载失败，请检查代理、防火墙或镜像源。
- 手动安装：
  - Windows：`pwsh Tools/install-playwright.ps1 -SkipIfPresent`
  - Linux/macOS：`bash Tools/install-playwright.sh --skip-if-present`
- 可在 `config/xiao-hong-shu.json` 指定 `playwrightInstallation.browsersPath` 指向共享缓存目录，或设置 `playwrightInstallation.downloadHost` 使用内网镜像。
- 若需清理残留下载，删除 `%LOCALAPPDATA%/ms-playwright`（Windows）或 `~/.cache/ms-playwright` 后重新执行安装脚本。

#### Q4: FingerprintBrowser 依赖报错怎么办？（FingerprintBrowser dependency issues）
- 确认 `libs/FingerprintBrowser.dll` 及配套 `FingerprintBrowser.pdb` / `FingerprintBrowser.xml` 是否存在，且未被安全工具隔离或误删；若缺失请重新解压官方交付包覆盖。（Ensure the DLL/PDB/XML bundle under `libs/` exists and is not quarantined; re-extract the official package if anything is missing.）
- 打开 `HushOps.Servers.XiaoHongShu.csproj`，确认 `<Reference Include="FingerprintBrowser">` 仍包含 `<HintPath>libs\\FingerprintBrowser.dll</HintPath>`；如被修改请还原。（Check the project file keeps the reference with the `libs\\FingerprintBrowser.dll` hint path; restore it if it was altered.）
- 若运行日志提示版本或加载失败，替换 `libs/` 目录中的 DLL 为最新交付版本并重启服务；可通过 `dotnet run -- --tools-list` 验证加载是否恢复。（Replace the DLL with the latest delivery and restart the service if runtime logs report load failures; use `dotnet run -- --tools-list` to confirm recovery.）

#### Q5: 如何调试 MCP 通信？（Debugging MCP communication）
- 服务器侧：设置环境变量 `DOTNET_ENVIRONMENT=Development` 后运行，可获得更详细的日志；同时关注 `logs/` 和控制台输出。
- 客户端侧：
  - Claude Desktop：`View > Toggle Developer Tools`，筛选 `mcp` 关键字查看连接与请求日志。
  - Cline：执行 `Cline: Manage MCP Servers`，在 `Installed` 标签查看状态；如需抓包，可开启 VS Code Output → Cline。
  - Cursor：运行 `cursor-agent mcp list`，确认 `status` 为 `connected`。
- 双向验证：使用 `dotnet run -- --tools-list` 比对服务器返回的工具数与客户端面板展示是否一致。

#### Q6: 如何使用自定义配置文件？（Using custom configuration files）
- 推荐在项目根目录创建 `config/xiao-hong-shu.json`，该文件会覆盖默认值；可按需拆分为多个 JSON 并使用 `jq`/脚本在部署时合并。
- 若需要环境区分，可设置环境变量 `DOTNET_ENVIRONMENT=Production|Staging` 并提供 `appsettings.{ENV}.json`；默认 `Host.CreateDefaultBuilder` 会自动加载匹配文件。
- 运行时临时覆盖使用环境变量前缀 `HUSHOPS_XHS_SERVER_`，例如 `HUSHOPS_XHS_SERVER_XHS__DefaultKeyword=球鞋`。
- 修改配置后无需重启客户端，但需重新启动服务器以载入最新设置（或在托管环境中使用热重载机制）。

### 许可证

项目采用 [Apache-2.0](./LICENSE) 许可证。欢迎在遵循许可证与平台条款的前提下复用与扩展。

### 支持

- 🐛 问题反馈：提交 Issue 至仓库所属团队
- 💡 功能建议：通过讨论区或 PR 附议
- 📧 联系方式：1317578863@qq.com

> 如果本项目对你有帮助，欢迎 star 支持！
