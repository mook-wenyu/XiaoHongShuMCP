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

## 工具清单

项目通过 MCP 协议暴露以下工具，可通过 `dotnet run -- --tools-list` 查看完整列表：

| 工具名称 | 功能描述 | 主要参数 |
|---------|---------|---------|
| `browser_open` | 打开或复用浏览器配置 | `profileKey`（默认 `user`）、`profilePath`（可选） |
| `xhs_random_browse` | 随机浏览小红书首页 | `keywords`、`portraitId`、`browserKey`、`behaviorProfile` |
| `xhs_keyword_browse` | 按关键词搜索并浏览 | 同上 |
| `xhs_discover_flow` | 发现页全链路流程（搜索+选笔记+点赞/收藏/评论） | `keywords`、`performLike`、`performFavorite`、`performComment`、`commentTexts` |
| `xhs_like` | 点赞当前笔记 | `keywords`、`portraitId`、`browserKey`、`behaviorProfile` |
| `xhs_favorite` | 收藏当前笔记 | 同上 |
| `xhs_comment` | 发表评论 | 同上 + `commentText`（必填） |
| `xhs_note_capture` | 批量捕获笔记数据并导出 CSV | `keywords`、`targetCount`、`sortBy`、`noteType`、`publishTime`、`includeAnalytics`、`includeRaw`、`runHumanizedNavigation` |
| `ll_execute` | 执行单个低级拟人化动作 | `actionType`、`target`、`timing`、`parameters`、`browserKey`、`behaviorProfile` |

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

- `xhs_navigate_explore`: 导航到发现页
- `xhs_search_keyword`: 搜索关键词
- `xhs_select_note`: 选择笔记
- `xhs_like_current`: 点赞当前笔记
- `xhs_favorite_current`: 收藏当前笔记
- `xhs_comment_current`: 评论当前笔记
- `xhs_scroll_browse`: 拟人化滚动浏览
- `xhs_flow_browse`: 完整浏览流程（发现页全链路）

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

## 快速开始

### 环境要求

- 安装 [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- 准备可用于自动化调试的浏览器实例（如需连接远程调试端口）
- 安装兼容 MCP 的客户端（例如 Claude Desktop）

### 构建与运行

```bash
# 恢复依赖
dotnet restore

# 编译项目（默认 Debug）
dotnet build HushOps.Servers.XiaoHongShu.csproj

# 启动本地 MCP 服务器
dotnet run --project HushOps.Servers.XiaoHongShu.csproj

# 列出当前可用工具（追加 CLI 参数）
dotnet run --project HushOps.Servers.XiaoHongShu.csproj -- --tools-list
```

### Playwright 浏览器安装

- **自动安装**：服务器在首次创建 Playwright 会话前会检测浏览器是否已安装，缺失时自动执行 `Microsoft.Playwright.Program.Main("install")`，默认仅下载 Chromium 与 FFMPEG，并在日志中输出安装进度；可通过配置 `playwrightInstallation:skipIfBrowsersPresent` 控制是否在检测到缓存时跳过安装。
- **手动安装脚本**：CI/CD 或受限环境可运行 `Tools/install-playwright.ps1` / `Tools/install-playwright.sh`；脚本默认使用现成的 `playwright.ps1`，若仓库尚未生成，可在维护者环境携带 `-BuildWhenMissing`（或 `--allow-build`）以触发一次 `dotnet build`。脚本支持 `--configuration`、`--framework`、`--cache-path`、`--browser`、`--force`，以及新增的 `--download-host` / `-DownloadHost`（自定义镜像）与 `--skip-if-present` / `-SkipIfBrowsersPresent` 配置。
- **缓存与镜像**：可以在配置文件设置 `playwrightInstallation:browsersPath` 指向共享缓存目录，减少重复下载；`playwrightInstallation:downloadHost` 支持自定义镜像源以应对受限网络环境。
- **故障排查**：若自动安装失败，请手动执行 `pwsh bin/<Configuration>/<TFM>/playwright.ps1 install` 并检查网络/代理设置。

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

### 1. 启动服务器

在 Claude Desktop 或其他 MCP 客户端中配置本服务器：

```json
{
  "mcpServers": {
    "xiao-hong-shu": {
      "command": "dotnet",
      "args": ["run", "--project", "D:/RiderProjects/HushOps.Servers/HushOps.Servers.XiaoHongShu/HushOps.Servers.XiaoHongShu.csproj"]
    }
  }
}
```

### 2. 打开浏览器并登录

首次使用需要手动登录小红书账号：

```json
// 使用 xhs_browser_open 工具
{
  "profileKey": "user",  // 使用用户浏览器配置（自动探测路径）
  "profilePath": null    // 可选，显式指定配置路径
}
```

**配置模式说明**：
- **用户模式**（`profileKey = user`）：
  - 自动探测常见浏览器配置路径（Chrome/Edge/Chromium）
  - 可通过 `profilePath` 显式指定配置目录
  - 适合在个人已登录账号的浏览器中操作
- **独立模式**（`profileKey = 其他值`）：
  - 创建隔离的浏览器配置，存储在 `storage/browser-profiles/<profileKey>`
  - 不允许指定 `profilePath`
  - 适合多账号管理或 CI/CD 环境

打开浏览器后，在弹出窗口中手动登录小红书账号。登录完成后，会话将保持，直到进程结束。

### 3. 执行自动化操作

#### 3.1 随机浏览

```json
// 使用 xhs_random_browse 工具
{
  "keywords": ["旅行", "美食"],
  "portraitId": "travel-lover",
  "browserKey": "user",
  "behaviorProfile": "default"
}
```

#### 3.2 关键词搜索并浏览

```json
// 使用 xhs_keyword_browse 工具
{
  "keywords": ["旅行攻略"],
  "portraitId": "travel-lover",
  "browserKey": "user",
  "behaviorProfile": "cautious"  // 使用谨慎档案
}
```

#### 3.3 发现页全链路流程

发现页全链路流程包含：搜索关键词 → 选择笔记 → 进入详情页 → 点赞/收藏/评论

```json
// 使用 xhs_discover_flow 工具
{
  "keywords": ["旅行攻略"],
  "portraitId": null,
  "noteSelection": "First",  // 或 "Random"
  "performLike": true,
  "performFavorite": true,
  "performComment": true,
  "commentTexts": ["很有帮助！", "感谢分享"],
  "browserKey": "user",
  "behaviorProfile": "default"
}
```

#### 3.4 批量捕获笔记数据

```json
// 使用 xhs_note_capture 工具
{
  "keywords": ["旅行攻略"],
  "portraitId": null,
  "targetCount": 50,
  "sortBy": "comprehensive",  // 综合排序，可选：comprehensive/time/popularity
  "noteType": "all",          // 笔记类型，可选：all/video/image
  "publishTime": "all",       // 发布时间，可选：all/day/week/month
  "includeAnalytics": true,   // 包含分析字段
  "includeRaw": true,         // 保存原始 JSON
  "outputDirectory": null,    // 默认输出到 storage/notes/
  "browserKey": "user",
  "behaviorProfile": "default",
  "runHumanizedNavigation": true  // 是否先执行拟人化导航
}
```

**输出结果**：
- CSV 文件：`storage/notes/xhs_notes_<keyword>_<timestamp>.csv`
- 原始 JSON（如启用）：`storage/notes/xhs_notes_<keyword>_<timestamp>_raw.json`

#### 3.5 单个交互动作

对于已在笔记详情页的场景，可以使用单个交互工具：

```json
// 点赞
{
  "keywords": ["旅行"],
  "portraitId": null,
  "browserKey": "user",
  "behaviorProfile": "default"
}

// 收藏（使用 xhs_favorite）
// 评论（使用 xhs_comment，需提供 commentText）
{
  "keywords": ["旅行"],
  "portraitId": null,
  "commentText": "很棒的分享！",
  "browserKey": "user",
  "behaviorProfile": "default"
}
```

### 4. 高级用法

#### 4.1 自定义行为档案

在配置文件中添加自定义档案：

```json
{
  "humanBehavior": {
    "profiles": {
      "my-custom-profile": {
        "preActionDelay": { "minMs": 300, "maxMs": 700 },
        "hesitationProbability": 0.15,
        "requireProxy": true,
        "allowedProxyPrefixes": ["socks5://"]
      }
    }
  }
}
```

#### 4.2 使用画像配置

画像用于关键词回退和用户行为模拟：

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

调用时指定 `portraitId`，系统将从对应画像的标签中随机选择关键词。

#### 4.3 多账号管理

使用独立配置模式管理多个账号：

```json
// 账号 A
{
  "profileKey": "account-a",
  "mode": "isolated"
}

// 账号 B
{
  "profileKey": "account-b",
  "mode": "isolated"
}
```

每个 `profileKey` 对应独立的浏览器配置和会话缓存，互不干扰。

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

## 许可证

项目采用 [Apache-2.0](./LICENSE) 许可证。欢迎在遵循许可证与平台条款的前提下复用与扩展。

## 支持

- 🐛 问题反馈：提交 Issue 至仓库所属团队
- 💡 功能建议：通过讨论区或 PR 附议
- 📧 联系方式：1317578863@qq.com

> 如果本项目对你有帮助，欢迎 star 支持！
