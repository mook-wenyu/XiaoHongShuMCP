# CLAUDE.md - XiaoHongShuMCP 项目指南

这是一个基于 .NET 8.0 和 Model Context Protocol (MCP) 的小红书智能自动化服务器实现。

## 🎯 项目概述

XiaoHongShuMCP 是一个专为小红书(XiaoHongShu)平台设计的 MCP 服务器，提供智能自动化功能：

### 核心特性
- **🔐 安全优先**: 所有内容操作仅保存为草稿，不支持自动发布
- **🚀 启动即用**: MCP服务器启动时自动连接浏览器并验证登录状态
- **🤖 智能搜索**: 支持多维度筛选的增强搜索功能
- **📊 数据分析**: 自动统计分析和 Excel 导出
- **👤 拟人化交互**: 模拟真人操作模式，防检测机制
- **🔧 完整测试**: 69+ 个测试用例，100% 通过率
- **🧩 通用API监听**: 全新的UniversalApiMonitor支持多端点监听
- **🔄 智能数据转换**: 专门的API数据转换器和模型系统

## 🏗️ 技术架构

### 核心技术栈
- **.NET 8.0**: LTS 版 .NET 框架，支持现代 C# 特性
- **Model Context Protocol 0.3.0-preview.4**: MCP 协议实现
- **Microsoft Playwright 1.54.0**: 浏览器自动化引擎
- **Serilog**: 结构化日志记录
- **NPOI 2.7.4**: Excel 文件操作
- **NUnit 4.4.0**: 单元测试框架

### 项目结构
```
HushOps/
├── HushOps.sln              # Visual Studio 解决方案
├── HushOps/                 # 主项目
│   ├── Program.cs                  # 程序入口（内置默认配置 + 覆盖机制）
│   ├── XiaoHongShuMCP.csproj      # 项目文件和依赖管理
│   ├── Services/                   # 服务层实现
│   │   ├── Interfaces.cs           # 接口定义和数据模型
│   │   ├── AccountManager.cs       # 账号管理服务
│   │   ├── PlaywrightBrowserManager.cs # 浏览器管理
│   │   ├── DomElementManager.cs    # DOM 选择器与元素管理
│   │   ├── HumanizedInteraction/   # 拟人化交互模块
│   │   │   ├── HumanizedInteractionService.cs # 主交互服务
│   │   │   ├── DelayManager.cs     # 智能延时管理
│   │   │   ├── ElementFinder.cs    # 高级元素查找
│   │   │   ├── SmartTextSplitter.cs # 智能文本分割
│   │   │   └── TextInputStrategies.cs # 文本输入策略
│   │   ├── BrowserConnectionHostedService.cs # 后台自动连接服务
│   │   └── XiaoHongShuService.cs   # 小红书核心服务
│   └── Tools/
│       └── XiaoHongShuTools.cs     # MCP 工具集定义
├── Tests/                          # 测试项目
│   ├── XiaoHongShuMCP.Tests.csproj # 测试项目配置
│   ├── Services/                   # 服务层测试
│   ├── Models/                     # 数据模型测试
│   ├── Tools/                      # MCP 工具测试
│   └── README.md                   # 测试文档
├── logs/                          # 日志文件目录
└── exports/                       # 数据导出目录
```

## 🔧 核心功能模块

### 1. 通用API监听系统 (UniversalApiMonitor)
- **多端点支持（版本无关）**: Homefeed(推荐) `/api/sns/web/v{N}/homefeed`、Feed(笔记详情) `/api/sns/web/v{N}/feed`、SearchNotes(搜索) `/api/sns/web/v{N}/search/notes`、Comments(评论) `/api/sns/web/v{N}/comment/page`
- **智能路由**: 根据URL模式自动识别API类型，路由到对应处理器
- **响应处理**: HomefeedResponseProcessor、FeedResponseProcessor、SearchNotesResponseProcessor、CommentsResponseProcessor
- **数据统一**: 将不同API格式统一转换为NoteDetail模型
- **实时监控**: 支持实时监控API响应和数据提取

- **API集成**: 完全集成UniversalApiMonitor，删除了内嵌简陋监听系统
- **依赖注入**: 使用现代DI模式，提高代码可测试性和维护性
- **收集策略**: 支持快速、标准、谨慎三种模式，适应不同场景
- **数据合并**: 智能合并API数据和页面数据，避免重复计数
- **性能监控**: 内置性能监控和效率评分系统

### 3. Feed API数据处理系统
- **FeedApiConverter**: 专门的API数据转换器，处理时间戳、图片、交互数据转换
- **FeedApiModels**: 完整的API响应数据模型，强类型和JSON映射支持
- （已由 UniversalApiMonitor 统一接管专用监听逻辑）
- **数据验证**: 内置数据有效性检查和错误处理机制

### 4. 推荐服务系统 (RecommendService)（已合并至 XiaoHongShuService）
- **智能推荐**: 基于用户行为和内容质量的智能推荐算法
- **多渠道数据**: 综合首页推荐、热门内容等多渠道数据
- **实时更新**: 实时获取最新推荐内容和趋势分析

### 5. 入口页守护 (PageStateGuard)
- **智能导航**: 自动识别和导航到发现页面的不同版块
- **页面验证**: 验证导航结果和页面加载状态
- **容错处理**: 处理页面结构变化和网络异常情况

### 6. 账号管理 (AccountManager)
- 浏览器会话连接和验证
- **Cookie 登录检测**: 基于 `web_session` cookie 的可靠登录状态检测
- 用户信息提取和验证
- 支持个人页面完整数据获取

### 7. 智能搜索 (SearchDataService)
- **多维度筛选**: 支持排序、类型、时间、范围等筛选
- **统计分析**: 自动计算数据质量、平均互动等指标
- **批量处理**: 智能批量获取笔记详情
- **数据导出**: 自动生成 Excel 报告

### 8. 内容管理 (XiaoHongShuService)
- **草稿保存**: 仅支持草稿模式，确保用户控制
- **笔记详情**: 获取完整笔记信息（图片、视频、评论）
- **评论功能**: 支持发布评论互动
- **类型识别**: 自动识别图文、视频、长文类型

### 9. 浏览器自动化 (PlaywrightBrowserManager)
- 连接现有浏览器会话 (端口 9222)
- 无头/有头模式支持
- 会话管理和资源释放
- **Cookie 登录检测**: 通过 `web_session` cookie 验证登录状态

### 10. 后台自动连接服务 (BrowserConnectionHostedService)
- **启动时自动连接**: MCP服务器启动后自动尝试连接浏览器
- **状态验证**: 自动检查浏览器连接和小红书登录状态
- **友好反馈**: 提供详细的连接状态日志信息
- **非阻塞启动**: 连接过程不影响MCP服务器正常启动
- **自动导航**: 连接浏览器成功后，自动导航到 `BaseUrl`（默认 `https://www.xiaohongshu.com/explore`）

### 11. 拟人化交互系统 (HumanizedInteraction)
全新重构的拟人化交互系统，采用模块化设计：
- **HumanizedInteractionService**: 主交互服务协调者
- **DelayManager**: 智能延时管理，提供多种延时策略
- **ElementFinder**: 高级元素查找，支持多级容错选择器
- **SmartTextSplitter**: 智能文本分割，模拟真人输入模式
- **TextInputStrategies**: 多种自然文本输入策略

### 12. 选择器管理 (SelectorManager)
- 多级容错 CSS 选择器
- 动态选择器更新
- 基于真实 HTML 结构优化
- 别名映射系统

## ⚙️ 端点监听与重试策略

统一的端点等待-重试机制适用于以下操作：

- 搜索：`GetSearchNotes`
- 推荐：`GetRecommendedNotes`
- 详情：`GetNoteDetail`
- 批量：`BatchGetNoteDetails`

配置项（`EndpointRetry`）：
- `AttemptTimeoutMs`：单次等待端点命中的超时（默认 120000 毫秒）
- `MaxRetries`：超时后最大重试次数，不含首次（默认 3）

关键行为（Last Retry → Go Home）：
- 在“最后一次重试”之前，服务会先强制跳转到主页（发现页），再执行对应操作或直接等待端点命中，以刷新 SPA 上下文并减少脏状态影响。
- 搜索/批量在最后一轮会跳过二次导航（避免重复），直接在主页执行输入与提交；推荐在最后一轮直接等待 Homefeed 命中。

覆盖方式（根节 XHS）：
- 环境变量：`XHS__EndpointRetry__AttemptTimeoutMs`、`XHS__EndpointRetry__MaxRetries`
- 命令行：`XHS:EndpointRetry:AttemptTimeoutMs=... XHS:EndpointRetry:MaxRetries=...`

与 `McpSettings:WaitTimeoutMs` 的关系：
- `McpSettings:WaitTimeoutMs` 是整体兜底等待；`EndpointRetry` 控制每轮端点等待与重试次数，两者互补。

## 🛠️ 开发环境配置

### 系统要求
- **.NET 8.0 SDK** 或更高版本
- **Visual Studio 2022** 或 **JetBrains Rider**
- **Chrome/Edge 浏览器** (远程调试支持)
- **Windows 10/11** 或 **macOS/Linux**

### 浏览器配置
用户需要启用浏览器远程调试：

```bash
# Chrome 启动参数
chrome.exe --remote-debugging-port=9222

# Edge 启动参数  
msedge.exe --remote-debugging-port=9222
```

### 开发命令

#### 构建和运行
```bash
# 恢复依赖
dotnet restore

# 构建项目
dotnet build

# 运行开发模式
dotnet run --project XiaoHongShuMCP

# 运行生产模式
dotnet run --project XiaoHongShuMCP --configuration Release
```

#### 测试相关
```bash
# 运行所有测试
dotnet test Tests

# 运行特定测试
dotnet test Tests --filter "ClassName=DomElementManagerTests"

# 详细测试输出
dotnet test Tests --verbosity normal
```

#### 发布部署
```bash
# 发布 Windows 版本
dotnet publish -c Release -r win-x64 --self-contained

# 发布 macOS 版本
dotnet publish -c Release -r osx-x64 --self-contained
```

## 📋 MCP 工具集

项目通过 `XiaoHongShuTools` 类暴露以下 MCP 工具：

### 浏览器连接工具
- **ConnectToBrowser**: 连接浏览器并验证登录状态

### 推荐与搜索
- **GetRecommendedNotes**: 获取推荐笔记流（集成UniversalApiMonitor）

### 搜索工具  
- **GetSearchNotes**: 基础搜索笔记功能，支持API监听和拟人化操作结合
  - 高级筛选：排序、类型、时间范围、是否导出等参数
  - 排序: comprehensive(综合), latest(最新), most_liked(最多点赞)
  - 类型: all(不限), video(视频), image(图文)  
  - 时间: all(不限), day(一天内), week(一周内), half_year(半年内)
  - 自动分析和Excel导出功能

### 用户资料工具（已废弃）
- 相关工具已移除；如需导航或进入发现页，由服务内部自动处理

### 内容详情工具
- **GetNoteDetail**: 基于单个关键词定位并获取详细信息（集成Feed/Comments API监听）
- **BatchGetNoteDetails**: 批量获取笔记详情（纯监听与拟人化操作结合）

### 互动工具
- **PostComment**: 发布评论到指定笔记
- **TemporarySaveAndLeave**: 保存内容为草稿（安全模式）

> ✨ **架构升级亮点**: 所有数据获取工具现均集成了UniversalApiMonitor，提供更稳定、高效的API数据获取能力。

## ⚙️ 配置管理

项目使用“代码内默认 + 外部覆盖”的方式，不再读取 `appsettings.json`；
并且仅注册一个配置类：`XhsSettings`（根节 `XHS`）。已移除 `AddEnvironmentVariables("XHS__")` 的前缀过滤，统一在根节 `XHS` 下读取与覆盖。

覆盖方式（优先级：命令行 > 环境变量 > 代码默认）：
- 环境变量：根节 `XHS`（双下划线 `__` 映射为冒号 `:`）。
  - 示例：`XHS__Serilog__MinimumLevel=Debug`、`XHS__BrowserSettings__Headless=true`
- 命令行参数：`XHS:Section:Key=Value`
  - 示例：`XHS:Serilog:MinimumLevel=Debug XHS:PageLoadWaitConfig:MaxRetries=5`

默认键见 `Program.cs` 的 `CreateDefaultSettings()`：`XHS:Serilog`, `XHS:UniversalApiMonitor`, `XHS:BrowserSettings`, `XHS:McpSettings`, `XHS:PageLoadWaitConfig`, `XHS:SearchTimeoutsConfig`, `XHS:EndpointRetry`, `XHS:DetailMatchConfig`, `XHS:InteractionCache`。
DI 绑定：`services.Configure<XhsSettings>(configuration.GetSection("XHS"))`。

### 统一等待超时配置（MCP）

- 键名：`XHS:McpSettings:WaitTimeoutMs`
- 默认：`600000`（10 分钟）
- 覆盖：环境变量 `XHS__McpSettings__WaitTimeoutMs` 或命令行 `XHS:McpSettings:WaitTimeoutMs`
- 说明：作为所有长耗时操作的统一等待时长，不限制上限。

### 详情页匹配参数（权重/模糊/拼音）

- `DetailMatchConfig:WeightedThreshold`（默认 0.5）
- `DetailMatchConfig:UseFuzzy`（默认 true）
- `DetailMatchConfig:MaxDistanceCap`（默认 3）
- `DetailMatchConfig:UsePinyin`（默认 true，首字母启发式）


### 按命名空间覆盖日志等级
- 键格式：`XHS:Logging:Overrides:<Namespace>=<Level>`（如 `Debug`/`Information`/`Warning`/`Error`）
- 环境变量：`XHS__Logging__Overrides__XiaoHongShuMCP.Services.UniversalApiMonitor=Debug`
- 命令行：`XHS:Logging:Overrides:XiaoHongShuMCP.Services.PlaywrightBrowserManager=Information`

### MCP 客户端配置 (Claude Desktop)

**配置文件位置**:
- Windows: `%APPDATA%\Claude\claude_desktop_config.json`
- macOS: `~/Library/Application Support/Claude/claude_desktop_config.json`

**开发环境配置**:
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
        "DOTNET_ENVIRONMENT": "Development"
      }
    }
  }
}
```

**生产环境配置**:
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

## 🧪 测试体系

### 测试覆盖
- **总测试数**: 67+ 个测试用例
- **通过率**: 100%
- **覆盖模块**: 服务层、数据模型、MCP 工具

### 测试框架
- **NUnit 4.4.0**: 主测试框架
- **Moq 4.20.72**: Mock 对象框架
- **Microsoft.Playwright 1.54.0**: 浏览器测试支持

### 运行测试
```bash
# 运行所有测试
dotnet test Tests --verbosity minimal

# 生成测试报告
dotnet test Tests --logger trx --results-directory TestResults
```

## 🔒 安全特性

### 内容安全
- **仅草稿模式**: 所有内容发布操作仅保存为草稿
- **用户控制**: 用户完全控制内容发布时机
- **数据脱敏**: 日志中敏感信息脱敏处理

### 防检测机制  
- **拟人化操作**: 模拟真人的点击、输入、滚动行为
- **随机延时**: 动态调整操作间隔时间
- **多选择器容错**: 应对页面结构变化

### 数据保护
- **本地处理**: 所有数据在本地处理，不上传第三方
- **会话管理**: 安全的浏览器会话管理
- **错误处理**: 优雅的错误处理和恢复机制

## 📝 开发最佳实践

### 代码规范
- 遵循 .NET 编码规范和命名约定
- 使用 C# 9.0+ 新特性 (records, pattern matching 等)
- 启用 nullable reference types
- 完整的 XML 文档注释

### 架构原则
- **通用API监听**: 使用UniversalApiMonitor统一管理多端点API监听
- **依赖注入**: 使用 Microsoft.Extensions.DependencyInjection
- **智能路由**: API响应根据URL模式自动路由到对应处理器
- **接口隔离**: 清晰的接口定义和实现分离
- **单一职责**: 每个服务类职责明确
- **错误处理**: 统一的 OperationResult<T> 模式
- **数据转换**: 专门的转换器处理API数据格式化

### 性能优化
- **异步编程**: 全面使用 async/await 模式
- **资源管理**: 及时释放浏览器和文件资源
- **批量处理**: 智能的批量操作模式
- **缓存策略**: 合理的选择器和数据缓存

## 🚀 部署说明

### 开发部署
1. 配置浏览器远程调试端口 (9222)
2. 运行 MCP 服务器: `dotnet run --project XiaoHongShuMCP`
3. 配置 Claude Desktop 的 mcpServers
4. 重启 Claude Desktop 使配置生效

### 生产部署
1. 构建发布版本: `dotnet publish -c Release`
2. 部署到目标服务器
3. 配置生产环境的 mcpServers 设置
4. 设置自动启动和监控

### 故障排除
- 检查浏览器远程调试端口是否可访问
- 确认小红书登录状态有效  
- 查看 `logs/` 目录中的详细日志
- 验证 MCP 客户端配置文件语法正确

## 📚 使用示例

### 连接浏览器
```typescript
// MCP 客户端调用（伪代码，按实际 SDK 调整）
await mcp.call("ConnectToBrowser", {});
```

### 智能搜索
```typescript
await mcp.call("GetSearchNotes", {
  keyword: "美食推荐",
  maxResults: 20,
  sortBy: "most_liked",
  noteType: "image",
  publishTime: "week"
});
```

### 获取笔记详情
```typescript
await mcp.call("GetNoteDetail", {
  keyword: "健身餐",
  includeComments: true
});
```

---

**项目状态**: ✅ 生产就绪  
**最后更新**: 2025年9月11日  
**版本**: 1.0.0  
**维护者**: XiaoHongShuMCP Team
