# XiaoHongShuMCP 正在结合自研智能体，当前版本暂不可用（不通用），修复后会撤掉提示。

> 基于 .NET 8.0 和 Model Context Protocol (MCP) 的小红书智能自动化服务器

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![MCP](https://img.shields.io/badge/MCP-0.3.0--preview.4-FF6B6B)](https://modelcontextprotocol.io/)
[![Tests](https://img.shields.io/badge/Tests-90%2B%20✅-4CAF50)](./Tests/)
[![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](./LICENSE)

XiaoHongShuMCP 是一个专为小红书(XiaoHongShu)平台设计的“本地 MCP 服务器（stdio-only）”，通过智能自动化技术为用户提供安全、高效的小红书运营工具。项目不包含任何 HTTP/SSE/Streamable HTTP 传输、Collector/Prometheus 出口或 Puppeteer/CDP 插件能力。

## ✨ 核心特性（本地-only / stdio-only）

- **🔐 安全优先** - 所有内容操作仅保存为草稿，确保用户完全控制发布时机
- **🚀 启动即用** - MCP服务器启动时自动连接浏览器并验证登录状态，无需手动操作
- **🤖 智能搜索** - 支持多维度筛选的增强搜索功能，自动统计分析
- **📊 数据分析** - 自动生成 Excel 报告，包含数据质量和互动统计
- **👤 拟人化交互** - 模拟真人操作模式，智能防检测机制
- **🧪 完整测试** - 90+ 个测试用例，100% 通过率，保证代码质量
- **🔧 智能监听** - 通用API监听器支持多端点实时数据获取
- **⚡ 现代架构** - 基于稳定的 .NET 8.0，使用依赖注入和异步编程模式
- **🧠 工具经纪** - 内置 Tool Broker 动态编排工具，仅暴露拟人化核心能力，支持按 `XHS__McpSettings__EnabledToolNames`/`DisabledToolNames` 白黑名单控制。
  
> 说明：本项目仅作为“本地 MCP 服务器（stdio 传输）”供 LLM 客户端使用；不包含任何 HTTP/SSE/Streamable HTTP 网络传输、Collector/Prometheus 出口或 Puppeteer/CDP 插件。
 - **自动导航** - 连接浏览器成功后自动跳转到 `BaseUrl`（默认探索页），不中断主流程
  - **多上下文池化** - 每账户独立持久化上下文（UserDataDir），池化页面租约，配合并发/速率/熔断治理。

## 🚀 快速开始

### 前置要求

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) 或更高版本
- Chrome/Edge 浏览器（支持远程调试）
- [Claude Desktop](https://claude.ai/) (MCP 客户端)

### 1. 克隆和构建

```bash
# 克隆项目
git clone https://github.com/mook-wenyu/XiaoHongShuMCP.git
cd XiaoHongShuMCP

# 恢复依赖
dotnet restore

# 构建项目
dotnet build

# 运行测试
dotnet test Tests
```

### 2. 配置浏览器

启用浏览器远程调试模式：

#### Windows 用户

1. **找到浏览器快捷方式**: 在桌面或开始菜单中，找到 Chrome 或 Edge 的快捷方式，右键选择 **属性**

2. **修改目标字段**: 在 **目标** 输入框末尾添加参数：
   ```
   --remote-debugging-port=9222
   ```
   
   **修改前**: `"C:\Program Files\Google\Chrome\Application\chrome.exe"`  
   **修改后**: `"C:\Program Files\Google\Chrome\Application\chrome.exe" --remote-debugging-port=9222`

3. **保存设置**: 点击 **应用** 然后 **确定**

#### macOS 用户

在终端中执行：

```bash
# Chrome
/Applications/Google\ Chrome.app/Contents/MacOS/Google\ Chrome --remote-debugging-port=9222

# Edge
/Applications/Microsoft\ Edge.app/Contents/MacOS/Microsoft\ Edge --remote-debugging-port=9222
```

### 3. 配置 Claude Desktop (MCP 客户端)

#### 配置文件位置

- **Windows**: `%APPDATA%\Claude\claude_desktop_config.json`
- **macOS**: `~/Library/Application Support/Claude/claude_desktop_config.json`
- **Linux**: `~/.config/claude/claude_desktop_config.json`

#### 开发环境配置

推荐在开发时使用此配置，便于调试和日志查看：

```json
{
  "mcpServers": {
    "xiaohongshu-mcp": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "D:\\RiderProjects\\XiaoHongShuMCP\\XiaoHongShuMCP"
      ],
      "env": {
        "DOTNET_ENVIRONMENT": "Development",
        "DOTNET_CLI_TELEMETRY_OPTOUT": "1"
      }
    }
  }
}
```

#### 生产环境配置

当项目编译发布后，使用可执行文件方式运行：

```json
{
  "mcpServers": {
    "xiaohongshu-mcp": {
      "command": "D:\\RiderProjects\\XiaoHongShuMCP\\XiaoHongShuMCP\\bin\\Release\\net8.0\\XiaoHongShuMCP.exe",
      "args": [],
      "env": {
        "DOTNET_ENVIRONMENT": "Production"
      }
    }
  }
}
```

#### macOS/Linux 配置示例

```json
{
  "mcpServers": {
    "xiaohongshu-mcp": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "/Users/yourname/Projects/HushOps/XiaoHongShuMCP"
      ],
      "env": {
        "DOTNET_ENVIRONMENT": "Development"
      }
    }
  }
}
```

#### 配置参数说明

- **command**: 执行命令，开发环境用 `dotnet`，生产环境用可执行文件路径
- **args**: 命令参数，开发环境需指定项目路径
- **env**: 环境变量
  - `DOTNET_ENVIRONMENT`: 运行环境（Development/Production）
  - `DOTNET_CLI_TELEMETRY_OPTOUT`: 禁用.NET遥测（可选）

#### 统一等待超时配置（MCP）

项目将所有长耗时等待统一为单一配置键（根节 `XHS`；仅注册一个配置类 `XhsSettings`）：

- 键名：`XHS:McpSettings:WaitTimeoutMs`
- 默认：`600000`（10 分钟）
- 覆盖方式：
  - 环境变量：`XHS__McpSettings__WaitTimeoutMs=600000`
  - 命令行：`XHS:McpSettings:WaitTimeoutMs=600000`

说明：默认值为 10 分钟；如需更长/更短，请直接设置毫秒值；不再限制上限。仅注册一个配置类 `XhsSettings`，所有键都在根节 `XHS` 下。

#### 端点监听与重试策略（重要）

对需要“监听 API 端点”的操作，已引入统一的“单次等待 + 最大重试”机制，并在最后一次重试前强制回到主页以刷新上下文。

- 配置键（根节 `XHS`）：
  - `XHS:EndpointRetry:AttemptTimeoutMs`（默认 `120000` 毫秒）
  - `XHS:EndpointRetry:MaxRetries`（默认 `3` 次；不含首次尝试）
- 覆盖方式：
  - 环境变量：`XHS__EndpointRetry__AttemptTimeoutMs=90000`、`XHS__EndpointRetry__MaxRetries=2`
  - 命令行：`XHS:EndpointRetry:AttemptTimeoutMs=90000 XHS:EndpointRetry:MaxRetries=2`
- 适用范围：
  - 搜索：`GetSearchNotes`（最后一轮先跳主页→直接搜索，避免重复导航）
  - 推荐：`GetRecommendedNotes`（最后一轮强制回主页后直接等待 Homefeed 命中）
  - 详情：`GetNoteDetail`（最后一轮先跳主页→重新定位笔记并点击）
  - 批量：`BatchGetNoteDetails`（最后一轮先跳主页→触发 SearchNotes）

说明：上述行为提升了端点未命中时的“自愈”能力，减少 SPA 场景下的死等待与脏状态影响。

#### Playwright 受管浏览器

本项目仅使用 Playwright 直接启动并管理浏览器（持久化上下文），不依赖连接已运行浏览器。

配置键（根节 `XHS`）：

- `XHS:BrowserSettings:Headless`（默认 `false`）
- `XHS:BrowserSettings:UserDataDir`（默认 `UserDataDir`，位于程序根目录）
- `XHS:BrowserSettings:Channel`（可选，如 `chrome` / `msedge` / `chromium`）
- `XHS:BrowserSettings:ExecutablePath`（可选，显式指定浏览器可执行文件）

覆盖方式（示例）：

```bash
# 环境变量（跨平台）
XHS__BrowserSettings__Headless=true \
XHS__BrowserSettings__UserDataDir=profiles/xhs-automation \
XHS__BrowserSettings__Channel=chrome

# 命令行覆盖
dotnet run --project XiaoHongShuMCP -- \
  XHS:BrowserSettings:Headless=true \
  XHS:BrowserSettings:UserDataDir=profiles/xhs-automation \
  XHS:BrowserSettings:Channel=chrome
```

如首次使用 Playwright 内置浏览器，请先执行浏览器安装脚本（任选其一）：

```bash
# PowerShell（Windows）
pwsh .\bin\Debug\net8.0\playwright.ps1 install

# Bash（macOS/Linux）
bash ./bin/Debug/net8.0/playwright.sh install
```

#### 验证配置

配置完成后（Program.cs 仅注册一个配置类：`services.Configure<XhsSettings>("XHS")`），重启 Claude Desktop 并检查：
1. 打开 Claude Desktop
2. 查看是否显示 MCP 服务器连接状态
3. 如有问题，查看 Claude Desktop 的错误日志

### 4. 启动服务

```bash
# 开发模式启动
dotnet run --project XiaoHongShuMCP

# 或者生产模式启动
dotnet run --project XiaoHongShuMCP --configuration Release
```

### 5. 在 Claude Desktop 中使用

1. 使用修改后的快捷方式启动浏览器
2. 在浏览器中登录小红书
3. 重启 Claude Desktop
4. **启动服务器时会自动连接** - 查看控制台日志确认连接状态
5. 现在可以使用以下 MCP 工具：

- `ConnectToBrowser`：连接浏览器并验证登录状态
- `GetRecommendedNotes`：获取推荐笔记流
- `GetSearchNotes`：搜索指定关键词笔记
- `GetNoteDetail`：基于单个关键词获取笔记详情
- `PostComment`：基于单个关键词定位并发布评论
- `InteractNote`：基于单个关键词定位并执行点赞/收藏（可组合）
- `SaveContentDraft`：保存笔记为草稿（创作平台）
- `BatchGetNoteDetails`：批量获取笔记详情（基于 SearchNotes 端点的纯监听实现，无 DOM 依赖）
  
#### 详情页关键词匹配增强

详情页匹配采用“字段加权 + 模糊 +（可选）拼音首字母”的综合策略：

- 权重默认：标题(4)、作者(3)、正文(2)、话题(2)、图片alt(1)
- 阈值：`DetailMatchConfig:WeightedThreshold`（默认 0.5）
- 模糊：`DetailMatchConfig:UseFuzzy`（默认 true），最大编辑距离上限 `DetailMatchConfig:MaxDistanceCap`（默认 3）
- 拼音：`DetailMatchConfig:UsePinyin`（默认 true，首字母启发式）

环境变量示例：

```
XHS__DetailMatchConfig__WeightedThreshold=0.6
XHS__DetailMatchConfig__UseFuzzy=true
XHS__DetailMatchConfig__MaxDistanceCap=2
XHS__DetailMatchConfig__UsePinyin=true
```

### 6. 端到端演示

建议直接在 MCP 客户端中脚本化“连接 → 搜索 → 详情 → 互动”的流程，不再提供本地 CLI 直调脚本。示例可参考下文各工具的调用片段（伪代码）。


## 📋 主要功能

### 🔍 核心功能模块

### 🔍 智能搜索系统

支持多维度筛选的搜索功能，包括排序方式、内容类型、发布时间等筛选条件，自动生成统计报告和 Excel 导出文件。

### 👤 账号管理系统

基于 `web_session` cookie 的登录状态检测，支持浏览器会话连接验证和用户信息提取。

### 📝 内容管理系统

仅支持草稿模式的安全内容管理，支持获取笔记详情、发布评论和自动类型识别。

### 🤖 拟人化交互系统

模块化的拟人化交互系统，提供智能延时管理、元素查找、文本输入等防检测机制。

## 🏗️ 项目架构

基于 .NET 8.0 的现代化 MCP 服务器架构，采用依赖注入和异步编程模式，提供稳定可靠的小红书自动化功能。

### 核心技术栈

- **.NET 8.0** - 现代 C# 开发框架
- **Model Context Protocol** - AI 助手工具协议
- **Microsoft Playwright** - 浏览器自动化
- **Serilog** - 结构化日志记录
- **NPOI** - Excel 文件操作
- **NUnit** - 单元测试框架

## 🛠️ 开发指南

### 本地开发

```bash
# 实时开发模式
dotnet watch --project XiaoHongShuMCP

# 运行特定测试
dotnet test Tests --filter "ClassName=DomElementManagerTests"

# 生成测试覆盖报告
dotnet test Tests --collect:"XPlat Code Coverage"
```

### 配置与覆盖

项目不再使用 `appsettings.json`。默认配置在 `Program.cs` 内部定义（`CreateDefaultSettings()`）。如需调整，推荐通过以下两种方式覆盖（已移除 `AddEnvironmentVariables("XHS__")` 前缀过滤，统一在根节 `XHS` 下读取）：

- 环境变量（推荐，根节 `XHS`；双下划线映射冒号）
  - Windows/跨平台示例：
    - `XHS__Serilog__MinimumLevel=Debug`
    - `XHS__BrowserSettings__Headless=true`
    - `XHS__PageLoadWaitConfig__NetworkIdleTimeout=300000`
    - `XHS__InteractionCache__TtlMinutes=5`   # 临时交互缓存 TTL（分钟，默认 3）
  - 说明：`XHS__Section__Key` 对应配置键 `Section:Key`。

- 命令行参数（覆盖优先级最高）
  - 示例：
    - `dotnet run --project XiaoHongShuMCP -- Serilog:MinimumLevel=Debug BrowserSettings:Headless=true`
    - `XiaoHongShuMCP.exe Serilog:MinimumLevel=Debug PageLoadWaitConfig:MaxRetries=5`

常用键位于以下节：`Serilog`, `UniversalApiMonitor`, `BrowserSettings`, `McpSettings`, `PageLoadWaitConfig`, `SearchTimeoutsConfig`, `InteractionCache`, `EndpointRetry`, `DetailMatchConfig`。

- 工具经纪相关键：
  - `XHS__McpSettings__EnabledToolNames__0=LikeNote` （仅暴露指定工具，可混用工具名或方法名）
  - `XHS__McpSettings__DisabledToolNames__0=TemporarySaveAndLeave` （强制下线敏感工具）
  - `XHS__McpSettings__ToolTitleOverrides__connect_to_browser=浏览器会话保活`（自定义展示标题）
  - `XHS__McpSettings__ToolDescriptionOverrides__like_note=拟人化点赞，自动遵循节律策略`（重写描述）
  - 未配置时默认开放所有拟人化核心工具，保持最低安全基线。

#### 按命名空间覆盖日志等级
- 任意命名空间/类名可单独调级：`Logging:Overrides:<Namespace>=<Level>`
- 环境变量示例：
  - `XHS__Logging__Overrides__XiaoHongShuMCP.Services.UniversalApiMonitor=Debug`
  - `XHS__Logging__Overrides__XiaoHongShuMCP.Services.PlaywrightBrowserManager=Information`
- 命令行示例：
  - `dotnet run --project XiaoHongShuMCP -- Logging:Overrides:XiaoHongShuMCP.Services.UniversalApiMonitor=Debug`
  - `Logging:Overrides:XiaoHongShuMCP.Services.PlaywrightBrowserManager=Information`

### 构建和部署

#### 本地开发部署

```bash
# 克隆项目
git clone https://github.com/mook-wenyu/XiaoHongShuMCP.git
cd XiaoHongShuMCP

# 恢复依赖
dotnet restore

# 构建项目
dotnet build

# 运行测试
dotnet test Tests

# 启动开发服务器
dotnet run --project XiaoHongShuMCP
```

#### 生产环境发布

1. **Windows 平台发布**：
```bash
# 独立部署（包含 .NET 运行时）
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true

# 框架依赖部署（需要目标机器安装 .NET）
dotnet publish -c Release -r win-x64 --self-contained false
```

2. **macOS 平台发布**：
```bash
dotnet publish -c Release -r osx-x64 --self-contained true -p:PublishSingleFile=true
dotnet publish -c Release -r osx-arm64 --self-contained true -p:PublishSingleFile=true
```

3. **Linux 平台发布**：
```bash
dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true
dotnet publish -c Release -r linux-arm64 --self-contained true -p:PublishSingleFile=true
```

#### Docker 部署（可选）

创建 `Dockerfile`：
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["XiaoHongShuMCP/XiaoHongShuMCP.csproj", "HushOps/"]
RUN dotnet restore "XiaoHongShuMCP/XiaoHongShuMCP.csproj"
COPY . .
WORKDIR "/src/XiaoHongShuMCP"
RUN dotnet build "XiaoHongShuMCP.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "XiaoHongShuMCP.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "XiaoHongShuMCP.dll"]
```

构建和运行：
```bash
docker build -t xiaohongshu-mcp .
docker run -d -p 9222:9222 --name xiaohongshu-mcp xiaohongshu-mcp
```

#### 生产环境配置

1. **系统服务配置（Linux）**：

创建 `/etc/systemd/system/xiaohongshu-mcp.service`：
```ini
[Unit]
Description=XiaoHongShu MCP Server
After=network.target

[Service]
Type=notify
WorkingDirectory=/opt/xiaohongshu-mcp
ExecStart=/opt/xiaohongshu-mcp/XiaoHongShuMCP
Restart=always
RestartSec=10
User=mcp
Environment=DOTNET_ENVIRONMENT=Production
Environment=DOTNET_URLS=http://localhost:5000

[Install]
WantedBy=multi-user.target
```

启动服务：
```bash
sudo systemctl daemon-reload
sudo systemctl enable xiaohongshu-mcp
sudo systemctl start xiaohongshu-mcp
sudo systemctl status xiaohongshu-mcp
```

2. **Windows 服务配置**：

使用 NSSM（Non-Sucking Service Manager）：
```cmd
# 下载并安装 NSSM
nssm install XiaoHongShuMCP "C:\path\to\XiaoHongShuMCP.exe"
nssm set XiaoHongShuMCP AppDirectory "C:\path\to\app\directory"
nssm set XiaoHongShuMCP AppEnvironmentExtra "DOTNET_ENVIRONMENT=Production"
nssm start XiaoHongShuMCP
```

#### 反向代理配置（可选）

如需要通过 Web 访问，可配置 Nginx：

```nginx
server {
    listen 80;
    server_name xiaohongshu-mcp.your-domain.com;

    location / {
        proxy_pass http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

#### 监控和日志

1. **日志配置**：
```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning"
      }
    },
    "WriteTo": [
      {
        "Name": "File",
        "Args": {
          "path": "logs/xiaohongshu-mcp-.txt",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 30,
          "outputTemplate": "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
        }
      }
    ]
  }
}
```

2. **性能监控**：
```bash
# 使用 htop 监控系统资源
htop

# 使用 journalctl 查看服务日志
journalctl -u xiaohongshu-mcp -f

# 检查端口占用
netstat -tlnp | grep :9222
```

#### 安全建议

1. **网络安全**：
   - 仅在必要时开放端口 9222
   - 使用防火墙限制访问来源
   - 考虑使用 VPN 或内网部署

2. **系统安全**：
   - 使用专用用户运行服务
   - 定期更新系统和依赖
   - 启用系统日志审计

3. **数据安全**：
   - 定期备份配置文件
   - 监控异常访问行为
   - 实施访问控制策略

## 🧪 测试

项目包含完整的单元测试套件：

```bash
# 运行所有测试
dotnet test Tests

# 运行测试并显示详细输出
dotnet test Tests --verbosity normal

# 生成测试报告
dotnet test Tests --logger trx --results-directory TestResults
```

### 测试覆盖

- **总测试数**: 70+ 个测试用例
- **通过率**: 100%
- **测试覆盖**: 服务层、数据模型、MCP 工具集
- **测试框架**: NUnit + Moq + Playwright
- **详细说明**: 查看 [Tests/README.md](./Tests/README.md)

## 🔒 安全和合规

### 安全特性

- **内容安全**: 所有发布操作仅保存为草稿，用户完全控制发布时机
- **数据保护**: 所有数据在本地处理，不上传第三方服务
- **防检测**: 智能拟人化操作，随机延时和行为模式
- **日志安全**: 敏感信息自动脱敏处理

### 合规使用

- 遵守小红书平台服务条款和使用协议
- 尊重用户隐私和数据保护法规
- 不支持大规模自动化操作和恶意行为
- 建议用户合理使用，避免频繁操作

## 📚 使用示例

### 遥测与选择器维护（内部化）

- 破坏性变更：弱选择器重排与快照导出不再暴露为 MCP 工具，仅通过 CLI/CI 内部使用。
- 导出弱选择器计划（默认 docs/selector-plans/plan-YYYYMMDD.json）：
```bash
dotnet run --project XiaoHongShuMCP -- selector-plan export --threshold 0.5 --minAttempts 10 --out docs/selector-plans
```

- 生成 ADR（docs/adr-0013-*.md）：
```bash
dotnet run --project XiaoHongShuMCP -- selector-adr --plan docs/selector-plans/plan-YYYYMMDD.json --threshold 0.5 --minAttempts 10
```

- 生成或应用最小源码补丁（reorder/prune）：
```bash
# 生成补丁（不直接改源码）
dotnet run --project XiaoHongShuMCP -- selector-plan patch --plan docs/selector-plans/plan-YYYYMMDD.json --mode reorder

# 谨慎：直接对源码应用（建议仅在专用分支）
dotnet run --project XiaoHongShuMCP -- selector-plan apply-source --plan docs/selector-plans/plan-YYYYMMDD.json --mode prune
```

配置项（默认）：
- `XHS:Telemetry:Enabled=true`
- `XHS:Telemetry:Directory=.telemetry`

说明：程序优雅退出后会尝试写入一次遥测快照；选择器治理的重排应用由 `SelectorPlanHostedService` 在启动期按 `XHS:Selectors:PlanPath` 自动应用，不对外暴露。

### 工具清单与文档核对

无需启动 MCP 客户端亦可在本地查看当前可用工具签名（名称/参数）：

```bash
dotnet run --project XiaoHongShuMCP -- tools-list
dotnet run --project XiaoHongShuMCP -- docs-verify  # 可选：核对 README 中示例与代码清单
```

### 基础连接

首先通过 MCP 客户端连接浏览器并验证登录状态：

```typescript
// 示例（在 MCP 客户端脚本中调用；伪代码，按实际 SDK 调整）
await mcp.call("ConnectToBrowser", {});
```

**预期输出**：
```json
{
  "IsConnected": true,
  "IsLoggedIn": true,
  "Message": "浏览器连接成功，已检测到小红书登录状态"
}
```

### 推荐笔记获取

获取小红书推荐流笔记：

```typescript
await mcp.call("GetRecommendedNotes", {
  limit: 20,
  timeoutMinutes: 5
});
```

### 搜索功能（纯监听）

**基础关键词搜索**：
```typescript
await mcp.call("GetSearchNotes", {
  keyword: "美食推荐",
  maxResults: 20,
  sortBy: "comprehensive",
  noteType: "all",
  publishTime: "all",
  includeAnalytics: true,
  autoExport: true
});
```

**高级筛选搜索**（同 `GetSearchNotes`，通过参数控制）：
```typescript
await mcp.call("GetSearchNotes", {
  keyword: "减脂餐",
  maxResults: 50,
  sortBy: "most_liked",
  noteType: "image",
  publishTime: "week",
  includeAnalytics: true,
  autoExport: true,
  exportFileName: "减脂餐搜索结果"
});
```

**可用搜索参数**：
- **sortBy**: `comprehensive` (综合), `latest` (最新), `most_liked` (最多点赞)
- **noteType**: `all` (不限), `video` (视频), `image` (图文)
- **publishTime**: `all` (不限), `day` (一天内), `week` (一周内), `half_year` (半年内)

> 注：旧版文档中的 GetUserProfile 工具已废弃。

### 笔记详情获取

**单个笔记详情（基于单一关键词）**：
```typescript
await mcp.call("GetNoteDetail", {
  keyword: "健身餐",
  includeComments: false
});
```

**批量笔记详情（纯监听，无 DOM 依赖）**：
```typescript
// 按关键词组触发 SearchNotes API，仅通过网络监听收集数据
const result = await mcp.call("BatchGetNoteDetails", {
  keyword: "健身餐",
  maxCount: 10,
  includeComments: false,   // 纯监听下建议关闭评论抓取
  autoExport: true,         // 自动导出为 Excel（/exports 目录）
  exportFileName: "批量详情示例"
});

if (result.SuccessfulNotes.length > 0) {
  // 仅来自 SearchNotes API 的结构化数据
}
```

### 互动功能

**发布评论**：
```typescript
await mcp.call("PostComment", {
  keyword: "健身餐",
  content: "很棒的分享！学到了很多实用技巧 👍"
});
```

**点赞/收藏笔记（可组合）**：
```typescript
// 点赞
await mcp.call("InteractNote", { keyword: "健身餐", like: true, favorite: false });
// 收藏
await mcp.call("InteractNote", { keyword: "健身餐", like: false, favorite: true });
// 同时点赞+收藏
await mcp.call("InteractNote", { keyword: "健身餐", like: true, favorite: true });
```

**保存为草稿（创作平台）**：
```typescript
await mcp.call("SaveContentDraft", {
  title: "我的美食分享",
  content: "今天尝试了一道新菜...",
  noteType: "Image", // 或 "Video"
  imagePaths: ["C:/pics/a.jpg", "C:/pics/b.jpg"],
  tags: ["美食", "家常菜", "分享"]
});
```

### 发现页/导航

> 旧版的 GetDiscoverPageNotes / NavigateToUser 已废弃。探索/导航由服务内部处理，无需单独工具。

 

### 完整工作流示例

一个完整的数据收集和分析工作流（示例）：

```typescript
// 1. 连接浏览器
const connection = await mcp.call("ConnectToBrowser", {});

if (connection.IsConnected && connection.IsLoggedIn) {
  // 2. 搜索相关笔记
  const searchResult = await mcp.call("GetSearchNotes", {
    keyword: "健身餐",
    maxResults: 100,
    sortBy: "most_liked",
    noteType: "image",
    publishTime: "week",
    includeAnalytics: true,
    autoExport: true,
    exportFileName: "健身餐分析报告"
  });

  // 3. 获取详细信息（如有需要）
  if (searchResult.Success && searchResult.SearchResult.Notes.length > 0) {
    const detailsResult = await mcp.call("BatchGetNoteDetails", {
      keyword: "健身餐",
      maxCount: 10,
      includeComments: false
    });
  }

  // 4. 用户资料相关工具已废弃
}
```

### 数据导出和分析

所有搜索工具都支持自动导出 Excel 报告，包含：

- **笔记基本信息**：标题、作者、发布时间
- **互动数据**：点赞、评论、收藏数
- **质量分析**：数据完整性评分
- **统计汇总**：平均互动数、热门时段等

导出文件保存在项目的 `exports/` 目录中。

### 错误处理

所有工具调用都遵循统一的错误处理模式：

```typescript
const result = await mcp.call("GetSearchNotes", {
  keyword: "测试关键词",
  maxResults: 20
});

if (result.Success) {
  console.log("操作成功:", result.Message);
  // 处理结果数据
} else {
  console.error("操作失败:", result.Message);
  console.error("错误代码:", result.ErrorCode);
  // 根据错误代码进行相应处理
}
```

## 🤝 贡献指南

欢迎贡献代码、报告问题或提出功能建议！

### 贡献流程

1. Fork 本仓库
2. 创建功能分支 (`git checkout -b feature/AmazingFeature`)
3. 提交更改 (`git commit -m 'Add some AmazingFeature'`)
4. 推送到分支 (`git push origin feature/AmazingFeature`)
5. 创建 Pull Request

### 开发规范

- 遵循 .NET 编码规范和命名约定
- 新功能必须包含单元测试
- 提交信息使用英文，格式清晰
- 代码必须通过所有现有测试

## 🐛 常见问题

### 浏览器连接
- 确保浏览器启动参数包含 `--remote-debugging-port=9222`
- 检查端口9222未被占用
- 在浏览器中完成小红书登录

### MCP 配置
- 验证 .NET 8.0 SDK 安装：`dotnet --version`
- 检查 `claude_desktop_config.json` 配置正确
- 重启 Claude Desktop 应用

### 使用问题
- 搜索结果为空：检查关键词或网络连接
- 详情获取失败：确认笔记存在且可访问
- 查看日志文件：`logs/xiaohongshu-mcp-*.txt`

## 📄 许可证

本项目采用 [Apache-2.0 许可证](./LICENSE)。

## 🔗 相关链接

- [Model Context Protocol 官方文档](https://modelcontextprotocol.io/)
- [.NET 8.0 文档](https://learn.microsoft.com/dotnet/)
- [Microsoft Playwright 文档](https://playwright.dev/dotnet/)
- [Claude Desktop 下载](https://claude.ai/)

## 📞 支持

- 🐛 [报告问题](https://github.com/mook-wenyu/HushOps/issues)
- 💡 [功能请求](https://github.com/mook-wenyu/HushOps/discussions)
- 👤 维护者：文聿
- 📧 联系我们：<mailto:1317578863@qq.com>

---

<p align="center">
  <strong>⭐ 如果这个项目对您有帮助，请给我们一个 Star！</strong>
</p>

<p align="center">
  Made with ❤️ by XiaoHongShuMCP Team
</p>
