# HushOps.Servers.XiaoHongShu

> 基于 .NET 8 的 Model Context Protocol（MCP）本地 stdio 服务器，实现针对小红书平台的人性化自动化工具集。

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![MCP](https://img.shields.io/badge/MCP-stdio--only-FF6B6B)](https://modelcontextprotocol.io/)
[![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](./LICENSE)

## 功能概览

- **Stdio-only MCP 服务器**：通过 `ModelContextProtocol.Server` 托管工具，默认暴露程序集内标记的工具方法。
- **人性化行动编排**：`HumanizedActionService` 根据画像、关键词与延迟配置执行浏览、点赞、收藏、评论等动作。
- **浏览器自动化封装**：`IBrowserAutomationService` 负责页面跳转与随机浏览，配合人性化延迟控制节奏。
- **笔记交互能力**：`INoteEngagementService`、`INoteCaptureService` 提供笔记收藏、点赞与批量捕获等接口。
- **浏览器配置模式切换**：通过 `xhs_browser_open` 工具选择用户或独立浏览器配置，支持路径自动发现与新建，并缓存已打开配置。
- **运行诊断**：`XiaoHongShuDiagnosticsService` 与简洁日志输出格式帮助排查 stdio 交互问题。

## 项目结构

| 目录/文件 | 说明 |
| --- | --- |
| `Program.cs` | 主入口，注册配置、日志与 MCP 服务器，支持 `--tools-list` 输出工具清单 |
| `Configuration/` | 包含 `XiaoHongShuOptions`，统一默认关键词、画像与人性化节奏设置 |
| `Services/Browser` | 浏览器自动化实现与接口 |
| `Services/Humanization` | 画像存储、关键词解析、人性化动作实现 |
| `Services/Notes` | 笔记仓储与互动服务 |
| `Infrastructure/` | 基础设施组件（文件系统、工具执行结果模型等） |
| `storage/` | 预留的本地缓存或导出目录 |
| `docs/` | 仓库文档与工作流档案 |

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

## 配置说明

- **JSON 配置**：
  - `appsettings.json`（可选）
  - `config/xiao-hong-shu.json`（可选）
- **环境变量**：统一以 `HUSHOPS_XHS_SERVER_` 作为前缀，例如：

```bash
set HUSHOPS_XHS_SERVER_XHS__DefaultKeyword=旅行攻略
set HUSHOPS_XHS_SERVER_XHS__Humanized__MinDelayMs=800
set HUSHOPS_XHS_SERVER_XHS__Humanized__MaxDelayMs=2600
```

- **选项要点**（`Configuration/XiaoHongShuOptions`）：
  - `Portraits`：提供画像标签与自定义元数据，供关键词解析回退使用。
  - `Humanized`：包含 `MinDelayMs`、`MaxDelayMs`、`Jitter`，控制行为节奏。
  - `DefaultKeyword`：在画像与请求均无关键词时的兜底值。

### 手工登录流程

1. 启动服务器后，调用 `xhs_browser_open`（或使用 MCP 客户端自动调用）打开浏览器。
2. 在弹出的浏览器窗口中手工登录小红书账号；服务不会保存 Cookie，进程结束后需重新登录。
3. 登录完成后即可调用 `xhs_random_browse`、`xhs_keyword_browse`、`xhs_like`、`xhs_favorite`、`xhs_comment`、`xhs_note_capture` 等工具执行后续操作。

## 浏览器配置模式

- 默认 `profileKey = user`。工具在执行前会检测缓存；若用户浏览器尚未打开，则自动探测常见路径或使用显式 `profilePath` 打开，返回结果会在元数据中标记 `autoOpened=true`。
- 通过 `profileKey` 区分配置：
  - `profileKey = user`：可选传入 `profilePath` 指定用户配置目录；为空时自动探测常见路径。如果提供了 `profilePath`，工具会直接使用该路径而不再探测。
  - 其他 `profileKey`：视为独立配置，统一映射到 `storage/browser-profiles/<profileKey>`；若目录不存在则自动创建；不允许提供额外的 `profilePath`。
- 会话缓存会返回 `isNewProfile`、`usedFallbackPath`、`alreadyOpen`、`autoOpened` 等标志，便于客户端决策；若尝试重复打开不同路径，会抛出异常阻止覆盖。
- 所有工具请求均需携带 `browserKey`（默认 `user`），用户模式会按需自动打开，独立模式仍需显式调用 `xhs_browser_open`。

示例调用（Claude Desktop JSON 请求体）：

```json
{
  "profileKey": "demo-session",
  "mode": "isolated",
  "requestId": "open-001"
}
```

## 测试与质量

- 使用 `dotnet test Tests/HushOps.Servers.XiaoHongShu.Tests/HushOps.Servers.XiaoHongShu.Tests.csproj -c Release` 可运行 13 项单元/集成测试，覆盖 MCP 日志能力、脱敏、通知与端到端日志流程。
- `dotnet build -c Release`、`dotnet test -c Release` 已作为基线命令，建议纳入 CI。
- `dotnet run -- --verification-run` 会执行示例浏览器流程并访问状态码端点，默认使用 `https://httpbin.org/status/429`；如网络策略禁止外网访问，可在配置文件中设置 `verification.statusUrl` 指向内网或本地接口，并可通过 `verification.mockStatusCode` 在本地拦截并返回指定状态码。端点不可达时，程序会记录警告且不会退出失败。

## 开发约定

- 代码风格遵循 .NET 默认规则（四空格缩进、PascalCase 公共成员、`_camelCase` 私有字段）。
- 提交信息推荐使用 Conventional Commits（如 `refactor(config): ...`）或简洁中文摘要。
- 所有贡献者应阅读并遵循 [`AGENTS.md`](./AGENTS.md) 中的治理规范。

## 贡献流程

1. Fork 本仓库并创建功能分支（示例：`git checkout -b feature/update-docs`）。
2. 按照贡献规范编写代码、补充测试与文档。
3. 提交前运行 `dotnet build`，确认无警告；若有测试项目需执行 `dotnet test`。
4. 在 Pull Request 中附上变更摘要、测试结果、关联 Issue 及必要截图，并请求熟悉模块的审阅者。

## 许可证

项目采用 [Apache-2.0](./LICENSE) 许可证。欢迎在遵循许可证与平台条款的前提下复用与扩展。

## 支持

- 🐛 问题反馈：提交 Issue 至仓库所属团队
- 💡 功能建议：通过讨论区或 PR 附议
- 📧 联系方式：1317578863@qq.com

> 如果本项目对你有帮助，欢迎 star 支持！
