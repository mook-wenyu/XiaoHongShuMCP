# XiaoHongShuMCP

> 基于 .NET 8.0 和 Model Context Protocol (MCP) 的小红书智能自动化服务器

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![MCP](https://img.shields.io/badge/MCP-0.3.0--preview.4-FF6B6B)](https://modelcontextprotocol.io/)
[![Tests](https://img.shields.io/badge/Tests-69%2B%20✅-4CAF50)](./Tests/)
[![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](./LICENSE)

XiaoHongShuMCP 是一个专为小红书(XiaoHongShu)平台设计的 MCP 服务器，通过智能自动化技术为用户提供安全、高效的小红书运营工具。

## ✨ 核心特性

- **🔐 安全优先** - 所有内容操作仅保存为草稿，确保用户完全控制发布时机
- **🚀 启动即用** - MCP服务器启动时自动连接浏览器并验证登录状态，无需手动操作
- **🤖 智能搜索** - 支持多维度筛选的增强搜索功能，自动统计分析
- **📊 数据分析** - 自动生成 Excel 报告，包含数据质量和互动统计
- **👤 拟人化交互** - 模拟真人操作模式，智能防检测机制
- **🧪 完整测试** - 69+ 个测试用例，100% 通过率，保证代码质量
- **🔧 模块化架构** - 全新的UniversalApiMonitor和重构的SmartCollectionController
- **📡 多端点监听** - 支持推荐、笔记详情、搜索、评论等多个API端点监听
- **⚡ 现代架构** - 基于稳定的 .NET 8.0，使用依赖注入和异步编程模式
 - **自动导航** - 连接浏览器成功后自动跳转到 `BaseUrl`（默认探索页），不中断主流程

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
        "/Users/yourname/Projects/XiaoHongShuMCP/XiaoHongShuMCP"
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

项目将所有长耗时等待统一为单一配置键：

- 键名：`McpSettings:WaitTimeoutMs`
- 默认：`600000`（10 分钟）
- 覆盖方式：
  - 环境变量：`XHS__McpSettings__WaitTimeoutMs=600000`
  - 命令行：`McpSettings:WaitTimeoutMs=600000`

说明：默认值为 10 分钟；如需更长/更短，请直接设置毫秒值；不再限制上限。

#### 端点监听与重试策略（重要）

对需要“监听 API 端点”的操作，已引入统一的“单次等待 + 最大重试”机制，并在最后一次重试前强制回到主页以刷新上下文。

- 配置键：
  - `EndpointRetry:AttemptTimeoutMs`（默认 `120000` 毫秒）
  - `EndpointRetry:MaxRetries`（默认 `3` 次；不含首次尝试）
- 覆盖方式：
  - 环境变量：`XHS__EndpointRetry__AttemptTimeoutMs=90000`、`XHS__EndpointRetry__MaxRetries=2`
  - 命令行：`--EndpointRetry:AttemptTimeoutMs=90000 --EndpointRetry:MaxRetries=2`
- 适用范围：
  - 搜索：`GetSearchNotes`（最后一轮先跳主页→直接搜索，避免重复导航）
  - 推荐：`GetRecommendedNotes`（最后一轮强制回主页后直接等待 Homefeed 命中）
  - 详情：`GetNoteDetail`（最后一轮先跳主页→重新定位笔记并点击）
  - 批量：`BatchGetNoteDetails`（最后一轮先跳主页→触发 SearchNotes）
  - 收集：`SmartCollectionController`（最后一轮强制主页导航后再等待 Homefeed）

说明：上述行为提升了端点未命中时的“自愈”能力，减少 SPA 场景下的死等待与脏状态影响。

#### 验证配置

配置完成后，重启 Claude Desktop 并检查：
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
- `LikeNote`：基于单个关键词定位并点赞
- `FavoriteNote`：基于单个关键词定位并收藏
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

### 6. 端到端演示脚本（E2E）

我们提供了两个演示脚本，覆盖三种起点场景（错误详情页 / 个人页 / 首页），并演示阈值、模糊、拼音等参数对匹配与入口页守护的影响：

- Bash: `scripts/e2e_entry_match_demo.sh`
- PowerShell: `scripts/e2e_entry_match_demo.ps1`

使用示例：

```
chmod +x scripts/e2e_entry_match_demo.sh
scripts/e2e_entry_match_demo.sh wrong-detail '["iPhone 15","苹果"]'

powershell -ExecutionPolicy Bypass -File scripts/e2e_entry_match_demo.ps1 -Scenario profile -KeywordsJson '["美食","杭州"]'
```

脚本会分三轮执行（严格/模糊/拼音），每轮分别调用 LikeNote / FavoriteNote / PostComment，并打印当前参数与结果。


## 📋 主要功能

### 🚀 全新架构特性

#### 通用API监听系统 (UniversalApiMonitor)

全新设计的通用API监听器，支持多端点智能监听：

- **多端点支持（版本无关）**:
  - Homefeed (推荐) - `/api/sns/web/v{N}/homefeed`
  - Feed (笔记详情) - `/api/sns/web/v{N}/feed`
  - SearchNotes (搜索) - `/api/sns/web/v{N}/search/notes`
  - Comments (评论列表) - `/api/sns/web/v{N}/comment/page`
- **智能路由**: 根据API端点类型自动路由到对应的响应处理器
- **响应处理器**: HomefeedResponseProcessor、FeedResponseProcessor、SearchNotesResponseProcessor、CommentsResponseProcessor
- **数据转换**: 自动将API响应转换为统一的NoteDetail格式
- **性能监控**: 内置性能监控和错误处理机制

#### 入口页守护 (PageStateGuard)

- 退出详情自愈：检测到详情页依次尝试关闭按钮→遮罩→ESC。
- 入口就绪保障：不在发现/搜索入口时，点击侧栏“发现”；失败回退直达URL。
- 统一接入：搜索/推荐/详情/批量流程操作前统一确保入口页就绪，减少 SPA 脏状态影响。

#### 重构智能收集系统 (SmartCollectionController)

集成通用API监听器的智能收集控制器：

- **API集成**: 完全集成UniversalApiMonitor，删除了内嵌的简陋监听系统
- **纯监听化**: 移除了滚动策略、性能监控器与数据合并等 DOM 相关遗留代码
- **依赖注入**: 使用现代依赖注入模式，提高代码可测试性
- **收集策略**: 支持快速、标准、谨慎三种收集策略
- **实时监控**: 实时监控API响应和数据收集进度
- **数据合并**: 智能合并API数据与页面数据，避免重复

### 🔍 智能搜索系统

支持多维度筛选的增强搜索功能：

- **排序方式**: 综合、最新、最多点赞、最多评论、最多收藏
- **内容类型**: 不限、视频、图文  
- **发布时间**: 不限、一天内、一周内、半年内
- **搜索范围**: 不限、已看过、未看过、已关注
- **位置距离**: 不限、同城、附近

自动生成统计报告和 Excel 导出文件。

### 👤 账号管理系统

- 浏览器会话连接和验证
- **Cookie 检测登录** - 基于 `web_session` cookie 的可靠登录状态检测
- 用户信息自动提取
- 支持个人页面完整数据获取

### 📝 内容管理系统

- **仅草稿模式**: 所有内容操作仅保存为草稿
- **笔记详情**: 获取完整笔记信息（图片、视频、评论）
- **评论互动**: 支持发布评论功能
- **智能识别**: 自动识别图文、视频、长文类型

### 🤖 拟人化交互系统

全新重构的拟人化交互系统，采用模块化设计：

- **智能延时管理** - `DelayManager` 提供多种延时策略
- **高级元素查找** - `ElementFinder` 支持多级容错选择器
- **智能文本分割** - `SmartTextSplitter` 模拟真人输入模式
- **多种输入策略** - `TextInputStrategies` 提供自然文本输入
- **防检测机制** - 随机延时和行为模式，模拟真实用户操作

### 📊 数据处理和转换系统

#### Feed API数据转换器 (FeedApiConverter)

专门处理Feed API响应数据的转换器：

- **数据转换**: 将原始API数据转换为标准NoteDetail格式
- **时间处理**: 自动处理Unix时间戳转换
- **图片处理**: 提取和处理图片URL列表
- **交互数据**: 处理点赞、评论、收藏等交互信息
- **用户信息**: 处理作者信息和头像数据

#### API数据模型 (FeedApiModels)

完整的API响应数据模型定义：

- **类型安全**: 强类型的API响应模型
- **JSON映射**: 自动JSON序列化和反序列化
- **数据验证**: 内置数据有效性验证
- **扩展性**: 支持未来API结构变化

## 🏗️ 项目架构

```
XiaoHongShuMCP/
├── XiaoHongShuMCP/           # 主项目
│   ├── Services/             # 核心服务层
│   │   ├── AccountManager.cs               # 账号管理
│   │   ├── XiaoHongShuService.cs           # 小红书核心服务
│   │   ├── PlaywrightBrowserManager.cs     # 浏览器管理
│   │   ├── DomElementManager.cs            # DOM 元素与选择器管理
│   │   ├── BrowserConnectionHostedService.cs # 后台连接服务
│   │   ├── UniversalApiMonitor.cs          # 通用API监听器
│   │   ├── SmartCollectionController.cs    # 智能收集控制器
│   │   ├── FeedApiConverter.cs             # Feed API数据转换器
│   │   ├── FeedApiModels.cs                # Feed API数据模型
│   │   ├── SearchTimeoutsConfig.cs         # 搜索等待与收敛超时配置
│   │   ├── HumanizedInteraction/           # 拟人化交互模块
│   │   │   ├── HumanizedInteractionService.cs # 主交互服务
│   │   │   ├── DelayManager.cs             # 智能延时管理
│   │   │   ├── ElementFinder.cs            # 高级元素查找
│   │   │   ├── SmartTextSplitter.cs        # 智能文本分割
│   │   │   └── TextInputStrategies.cs      # 文本输入策略
│   │   └── Interfaces.cs                   # 接口定义
│   ├── Tools/               # MCP 工具集
│   │   └── XiaoHongShuTools.cs            # MCP 工具定义
│   └── Program.cs           # 程序入口（内置默认配置 + 覆盖机制）
├── Tests/                   # 单元测试（约 51 个）
│   ├── Services/           # 服务测试
│   ├── Models/             # 模型测试  
│   └── Tools/              # 工具测试
└── README.md               # 项目文档
```

### 核心技术栈

- **[.NET 8.0](https://dotnet.microsoft.com/)** - 现代 C# 开发框架
- **[Model Context Protocol](https://modelcontextprotocol.io/)** - AI 助手工具协议
- **[Microsoft Playwright](https://playwright.dev/dotnet/)** - 浏览器自动化
- **[Serilog](https://serilog.net/)** - 结构化日志记录
- **[NPOI](https://github.com/nissl-lab/npoi)** - Excel 文件操作
- **[NUnit](https://nunit.org/)** - 单元测试框架

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

项目不再使用 `appsettings.json`。默认配置在 `Program.cs` 内部定义（`CreateDefaultSettings()`）。如需调整，推荐通过以下两种方式覆盖：

- 环境变量（推荐，前缀 `XHS__`，双下划线映射冒号）
  - Windows/跨平台示例：
    - `XHS__Serilog__MinimumLevel=Debug`
    - `XHS__BrowserSettings__Headless=true`
    - `XHS__PageLoadWaitConfig__NetworkIdleTimeout=300000`
  - 说明：`XHS__Section__Key` 对应配置键 `Section:Key`。

- 命令行参数（覆盖优先级最高）
  - 示例：
    - `dotnet run --project XiaoHongShuMCP -- Serilog:MinimumLevel=Debug BrowserSettings:Headless=true`
    - `XiaoHongShuMCP.exe Serilog:MinimumLevel=Debug PageLoadWaitConfig:MaxRetries=5`

常用键位于以下节：`Serilog`, `UniversalApiMonitor`, `BrowserSettings`, `McpSettings`, `PageLoadWaitConfig`, `SearchTimeoutsConfig`。

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
COPY ["XiaoHongShuMCP/XiaoHongShuMCP.csproj", "XiaoHongShuMCP/"]
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

- **总测试数**: 51 个测试用例
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

### CLI 自测试（callTool 封装）

无需 MCP 客户端也可本地自测：

```bash
# 例：搜索
dotnet run --project XiaoHongShuMCP -- callTool GetSearchNotes --json '{
  "keyword": "健身餐",
  "maxResults": 5,
  "includeAnalytics": false,
  "autoExport": false
}'

# 例：批量（纯监听）
dotnet run --project XiaoHongShuMCP -- callTool BatchGetNoteDetails --json '{
  "keyword": "健身餐",
  "maxCount": 5,
  "includeComments": false,
  "autoExport": false
}'

# 例：保存草稿（创作平台）
dotnet run --project XiaoHongShuMCP -- callTool SaveContentDraft --json '{
  "title": "我的美食分享",
  "content": "今天尝试了一道新菜...",
  "noteType": "Image",
  "imagePaths": ["C:/pics/a.jpg"]
}'
```

说明：`callTool` 模式会直接在本地构建依赖注入容器并调用对应工具的方法，输出 JSON 结果，便于脚本化自测。

### 文档一致性核对（docs-verify）

快速比对 README 中的 `callTool("ConnectToBrowser")` 用例与代码中的工具清单：

```bash
dotnet run --project XiaoHongShuMCP -- docs-verify
dotnet run --project XiaoHongShuMCP -- tools-list   # 打印工具签名（名称/参数）

# 也可用脚本
scripts/callTool.sh GetSearchNotes --json '{"keyword":"测试"}'
```

### 基础连接

首先连接浏览器并验证登录状态：

```typescript
// 在 Claude Desktop 中调用
await callTool("ConnectToBrowser", {});
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
await callTool("GetRecommendedNotes", {
  limit: 20,
  timeoutMinutes: 5
});
```

### 搜索功能（纯监听）

**基础关键词搜索**：
```typescript
await callTool("GetSearchNotes", {
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
await callTool("GetSearchNotes", {
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
await callTool("GetNoteDetail", {
  keyword: "健身餐",
  includeComments: false
});
```

**批量笔记详情（纯监听，无 DOM 依赖）**：
```typescript
// 按关键词组触发 SearchNotes API，仅通过网络监听收集数据
const result = await callTool("BatchGetNoteDetails", {
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
await callTool("PostComment", {
  keyword: "健身餐",
  content: "很棒的分享！学到了很多实用技巧 👍"
});
```

**点赞笔记**：
```typescript
await callTool("LikeNote", {
  keyword: "健身餐",
  forceAction: false // 如已点赞则跳过；设为 true 将强制尝试
});
```

**收藏笔记**：
```typescript
await callTool("FavoriteNote", {
  keyword: "健身餐",
  forceAction: false // 如已收藏则跳过；设为 true 将强制尝试
});
```

**保存为草稿（创作平台）**：
```typescript
await callTool("SaveContentDraft", {
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
const connection = await callTool("ConnectToBrowser", {});

if (connection.IsConnected && connection.IsLoggedIn) {
  // 2. 搜索相关笔记
  const searchResult = await callTool("GetSearchNotes", {
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
    const detailsResult = await callTool("BatchGetNoteDetails", {
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
const result = await callTool("GetSearchNotes", {
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

## 🐛 故障排除

### 常见问题及解决方案

#### 浏览器连接问题

**Q1: 无法连接到浏览器 (端口 9222)**
```
A: 解决步骤：
1. 确认浏览器启动参数正确：--remote-debugging-port=9222
2. 检查端口是否被占用：netstat -an | findstr 9222
3. 确保防火墙允许端口 9222
4. 尝试重新启动浏览器
5. Windows 用户检查快捷方式目标路径是否正确
```

**Q2: 连接成功但登录检测失败**
```
A: 解决步骤：
1. 手动在浏览器中访问 https://www.xiaohongshu.com
2. 完成登录流程（包括手机验证码等）
3. 确认能正常浏览小红书内容
4. 在 Claude 中重新调用 ConnectToBrowser 工具
5. 如仍失败，清除浏览器 Cookie 后重新登录
```

#### MCP 配置问题

**Q3: MCP 服务器无法启动**
```
A: 诊断步骤：
1. 检查 .NET 8.0 SDK 是否正确安装：dotnet --version
2. 验证项目路径是否正确
3. 运行 dotnet restore 恢复依赖
4. 检查 claude_desktop_config.json 语法是否正确
5. 查看 Claude Desktop 错误日志
```

**Q4: MCP 工具调用失败**
```
A: 解决方案：
1. 重启 Claude Desktop 应用
2. 检查配置文件中的路径分隔符（Windows 使用 \\\\）
3. 确认环境变量配置正确
4. 手动测试命令：dotnet run --project <项目路径>
5. 查看服务器启动日志确认无错误
```

#### 功能使用问题

**Q5: 搜索结果为空**
```
A: 可能原因：
1. 关键词可能被限制或敏感
2. 网络连接不稳定
3. 小红书接口响应超时
4. 登录状态已过期，需重新登录
```

**Q6: 笔记详情获取失败**
```
A: 检查要点：
1. 笔记 ID 是否正确
2. 笔记是否已被删除或设为私密
3. 是否存在地区访问限制
4. 浏览器是否被检测到异常操作
```

### 高级故障排除

#### 开发环境调试

1. **启用详细日志**：
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "XiaoHongShuMCP": "Trace"
    }
  }
}
```

2. **使用开发模式运行**：
```bash
dotnet run --project XiaoHongShuMCP --environment Development
```

3. **查看实时日志**：
```bash
# Windows PowerShell
Get-Content -Path "logs\xiaohongshu-mcp-*.txt" -Wait -Tail 10

# Git Bash / WSL
tail -f logs/xiaohongshu-mcp-*.txt
```

#### 性能优化建议

1. **浏览器优化**：
   - 关闭不必要的扩展程序
   - 清理浏览器缓存和 Cookie
   - 使用隐私模式避免干扰

2. **系统优化**：
   - 确保有足够内存（建议 4GB+）
   - 关闭杀毒软件实时保护（临时）
   - 使用有线网络连接提高稳定性

#### 错误代码对照表

| 错误代码 | 说明 | 解决方法 |
|---------|------|---------|
| `CONNECTION_TIMEOUT` | 连接超时 | 检查网络连接，增加超时时间 |
| `LOGIN_REQUIRED` | 需要登录 | 在浏览器中完成登录 |
| `RATE_LIMIT_EXCEEDED` | 请求频率过高 | 减少请求频率，等待后重试 |
| `INVALID_SELECTOR` | 选择器失效 | 可能页面结构变化，需更新选择器 |
| `ELEMENT_NOT_FOUND` | 元素未找到 | 页面加载未完成或结构变化 |

### 日志分析

#### 日志文件位置
- **开发环境**: `logs/xiaohongshu-mcp-{date}.txt`
- **生产环境**: 根据部署配置确定

#### 常用日志分析命令

```bash
# 查看最新日志
tail -n 50 logs/xiaohongshu-mcp-*.txt

# 搜索错误信息
grep -i "error\|exception" logs/xiaohongshu-mcp-*.txt

# 搜索特定功能日志
grep -i "search\|connect" logs/xiaohongshu-mcp-*.txt

# 按时间范围查看日志
grep "2025-09-06" logs/xiaohongshu-mcp-*.txt
```

#### 日志级别说明
- **Trace**: 最详细的调试信息
- **Debug**: 调试信息
- **Information**: 一般信息
- **Warning**: 警告信息
- **Error**: 错误信息
- **Critical**: 严重错误

## 📄 许可证

本项目采用 [Apache-2.0 许可证](./LICENSE)。

## 🔗 相关链接

- [Model Context Protocol 官方文档](https://modelcontextprotocol.io/)
- [.NET 8.0 文档](https://learn.microsoft.com/dotnet/)
- [Microsoft Playwright 文档](https://playwright.dev/dotnet/)
- [Claude Desktop 下载](https://claude.ai/)

## 📞 支持

- 🐛 [报告问题](https://github.com/mook-wenyu/XiaoHongShuMCP/issues)
- 💡 [功能请求](https://github.com/mook-wenyu/XiaoHongShuMCP/discussions)
- 👤 维护者：文聿
- 📧 联系我们：<mailto:1317578863@qq.com>

---

<p align="center">
  <strong>⭐ 如果这个项目对您有帮助，请给我们一个 Star！</strong>
</p>

<p align="center">
  Made with ❤️ by XiaoHongShuMCP Team
</p>
