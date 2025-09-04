# CLAUDE.md - XiaoHongShuMCP 项目指南

这是一个基于 .NET 9.0 和 Model Context Protocol (MCP) 的小红书智能自动化服务器实现。

## 🎯 项目概述

XiaoHongShuMCP 是一个专为小红书(XiaoHongShu)平台设计的 MCP 服务器，提供智能自动化功能：

### 核心特性
- **🔐 安全优先**: 所有内容操作仅保存为草稿，不支持自动发布
- **🚀 启动即用**: MCP服务器启动时自动连接浏览器并验证登录状态
- **🤖 智能搜索**: 支持多维度筛选的增强搜索功能
- **📊 数据分析**: 自动统计分析和 Excel 导出
- **👤 拟人化交互**: 模拟真人操作模式，防检测机制
- **🔧 完整测试**: 74 个单元测试，100% 通过率

## 🏗️ 技术架构

### 核心技术栈
- **.NET 9.0**: 最新 .NET 框架，支持现代 C# 特性
- **Model Context Protocol 0.3.0-preview.4**: MCP 协议实现
- **Microsoft Playwright 1.54.0**: 浏览器自动化引擎
- **Serilog**: 结构化日志记录
- **NPOI 2.7.4**: Excel 文件操作
- **NUnit 4.4.0**: 单元测试框架

### 项目结构
```
XiaoHongShuMCP/
├── XiaoHongShuMCP.sln              # Visual Studio 解决方案
├── XiaoHongShuMCP/                 # 主项目
│   ├── Program.cs                  # 程序入口，MCP 服务器配置
│   ├── XiaoHongShuMCP.csproj      # 项目文件和依赖管理
│   ├── appsettings.json           # 应用配置文件
│   ├── Services/                   # 服务层实现
│   │   ├── Interfaces.cs           # 接口定义和数据模型
│   │   ├── AccountManager.cs       # 账号管理服务
│   │   ├── PlaywrightBrowserManager.cs # 浏览器管理
│   │   ├── SelectorManager.cs      # CSS 选择器管理
│   │   ├── HumanizedInteraction/   # 拟人化交互模块
│   │   │   ├── HumanizedInteractionService.cs # 主交互服务
│   │   │   ├── DelayManager.cs     # 智能延时管理
│   │   │   ├── ElementFinder.cs    # 高级元素查找
│   │   │   ├── SmartTextSplitter.cs # 智能文本分割
│   │   │   └── TextInputStrategies.cs # 文本输入策略
│   │   ├── SearchDataService.cs    # 搜索和数据服务
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

### 1. 账号管理 (AccountManager)
- 浏览器会话连接和验证
- 登录状态检查和监控
- 用户信息提取和验证
- 支持个人页面完整数据获取

### 2. 智能搜索 (SearchDataService)
- **多维度筛选**: 支持排序、类型、时间、范围等筛选
- **统计分析**: 自动计算数据质量、平均互动等指标
- **批量处理**: 智能批量获取笔记详情
- **数据导出**: 自动生成 Excel 报告

### 3. 内容管理 (XiaoHongShuService)
- **草稿保存**: 仅支持草稿模式，确保用户控制
- **笔记详情**: 获取完整笔记信息（图片、视频、评论）
- **评论功能**: 支持发布评论互动
- **类型识别**: 自动识别图文、视频、长文类型

### 4. 浏览器自动化 (PlaywrightBrowserManager)
- 连接现有浏览器会话 (端口 9222)
- 无头/有头模式支持
- 会话管理和资源释放
- 登录状态持久化

### 5. 后台自动连接服务 (BrowserConnectionHostedService)
- **启动时自动连接**: MCP服务器启动后自动尝试连接浏览器
- **状态验证**: 自动检查浏览器连接和小红书登录状态
- **友好反馈**: 提供详细的连接状态日志信息
- **非阻塞启动**: 连接过程不影响MCP服务器正常启动

### 6. 拟人化交互系统 (HumanizedInteraction)
全新重构的拟人化交互系统，采用模块化设计：
- **HumanizedInteractionService**: 主交互服务协调者
- **DelayManager**: 智能延时管理，提供多种延时策略
- **ElementFinder**: 高级元素查找，支持多级容错选择器
- **SmartTextSplitter**: 智能文本分割，模拟真人输入模式
- **TextInputStrategies**: 多种自然文本输入策略

### 7. 选择器管理 (SelectorManager)
- 多级容错 CSS 选择器
- 动态选择器更新
- 基于真实 HTML 结构优化
- 别名映射系统

## 🛠️ 开发环境配置

### 系统要求
- **.NET 9.0 SDK** 或更高版本
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
dotnet test Tests --filter "ClassName=SelectorManagerTests"

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

### 账号管理工具
- **ConnectToBrowser**: 连接浏览器并验证登录状态

### 搜索工具  
- **SearchNotesEnhanced**: 智能搜索，支持完整筛选参数
  - 排序: comprehensive(综合), latest(最新), most_liked(最多点赞)
  - 类型: all(不限), video(视频), image(图文)  
  - 时间: all(不限), day(一天内), week(一周内), half_year(半年内)
  - 范围: all(不限), viewed(已看过), unviewed(未看过)
  - 位置: all(不限), same_city(同城), nearby(附近)

### 内容工具
- **GetNoteDetail**: 获取笔记详情
- **PostComment**: 发布评论
- **TemporarySaveAndLeave**: 暂存笔记为草稿
- **BatchGetNoteDetailsOptimized**: 批量获取笔记详情

## ⚙️ 配置管理

### appsettings.json 主要配置
```json
{
  "XiaoHongShu": {
    "BaseUrl": "https://www.xiaohongshu.com",
    "DefaultTimeout": 30000,
    "MaxRetries": 3,
    "BrowserSettings": {
      "Headless": false,
      "RemoteDebuggingPort": 9222
    }
  }
}
```

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
      "command": "D:\\RiderProjects\\XiaoHongShuMCP\\XiaoHongShuMCP\\bin\\Release\\net9.0\\XiaoHongShuMCP.exe",
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
- **总测试数**: 74 个测试用例
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
- **依赖注入**: 使用 Microsoft.Extensions.DependencyInjection
- **接口隔离**: 清晰的接口定义和实现分离
- **单一职责**: 每个服务类职责明确
- **错误处理**: 统一的 OperationResult<T> 模式

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
// MCP 客户端调用
await callTool("ConnectToBrowser", {});
```

### 智能搜索
```typescript
await callTool("SearchNotesEnhanced", {
  keyword: "美食推荐",
  limit: 20,
  sortBy: "most_liked",
  noteType: "image",
  publishTime: "week"
});
```

### 获取笔记详情
```typescript
await callTool("GetNoteDetail", {
  noteId: "xxxxxx",
  includeComments: true
});
```

---

**项目状态**: ✅ 生产就绪  
**最后更新**: 2025年9月4日  
**版本**: 1.0.0  
**维护者**: XiaoHongShuMCP Team