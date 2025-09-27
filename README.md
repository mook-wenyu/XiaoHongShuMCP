# HushOps.Servers.XiaoHongShu

> 基于 .NET 8 的 Model Context Protocol（MCP）本地 stdio 服务器，实现针对小红书平台的人性化自动化工具集。

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![MCP](https://img.shields.io/badge/MCP-stdio--only-FF6B6B)](https://modelcontextprotocol.io/)
[![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](./LICENSE)

## 功能概览

- **Stdio-only MCP 服务器**：通过 `ModelContextProtocol.Server` 托管工具，默认暴露程序集内标记的工具方法。
- **人性化行动编排**：`HumanizedActionService` 根据账号画像、关键词与延迟配置执行浏览、点赞、收藏、评论等动作。
- **浏览器自动化封装**：`IBrowserAutomationService` 负责页面跳转与随机浏览，配合人性化延迟控制节奏。
- **笔记交互能力**：`INoteEngagementService`、`INoteCaptureService` 提供笔记收藏、点赞与批量捕获等接口。
- **浏览器配置模式切换**：通过 `xhs_browser_open` 工具选择用户或独立浏览器配置，支持路径自动发现与新建，并缓存已打开配置。
- **运行诊断**：`XiaoHongShuDiagnosticsService` 与简洁日志输出格式帮助排查 stdio 交互问题。

## 项目结构

| 目录/文件 | 说明 |
| --- | --- |
| `Program.cs` | 主入口，注册配置、日志与 MCP 服务器，支持 `--tools-list` 输出工具清单 |
| `Configuration/` | 包含 `XiaoHongShuOptions`，统一账号、画像、人性化节奏设置 |
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

## 配置说明

- **JSON 配置**：
  - `appsettings.json`（可选）
  - `config/xiao-hong-shu.json`（可选）
- **环境变量**：统一以 `HUSHOPS_XHS_SERVER_` 作为前缀，例如：

```bash
set HUSHOPS_XHS_SERVER_XHS__Accounts__0__Id=demo-account
set HUSHOPS_XHS_SERVER_XHS__Accounts__0__Cookies="COOKIE=..."
set HUSHOPS_XHS_SERVER_XHS__Humanized__MinDelayMs=800
```

- **选项要点**（`Configuration/XiaoHongShuOptions`）：
  - `Accounts`：至少配置一个账号（Id、Cookies）。
  - `Portraits`：提供画像标签与自定义元数据，供关键词解析回退使用。
  - `Humanized`：包含 `MinDelayMs`、`MaxDelayMs`、`Jitter`，控制行为节奏。
  - `DefaultKeyword`：在画像与请求均无关键词时的兜底值。

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

- 当前仓库尚未纳入自动化测试，`Tests/` 目录计划在后续迭代补充。
- 建议在新增测试项目后，将 `dotnet test` 纳入 CI 与 PR 检查。
- 针对关键服务（配置绑定、人性化延迟、工具枚举、浏览器缓存）优先补齐单元测试。
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
