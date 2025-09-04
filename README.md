# XiaoHongShuMCP

> 基于 .NET 9.0 和 Model Context Protocol (MCP) 的小红书智能自动化服务器

[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![MCP](https://img.shields.io/badge/MCP-0.3.0--preview.4-FF6B6B)](https://modelcontextprotocol.io/)
[![Tests](https://img.shields.io/badge/Tests-74%20✅-4CAF50)](./Tests/)
[![License](https://img.shields.io/badge/License-MIT-blue.svg)](./LICENSE)

XiaoHongShuMCP 是一个专为小红书(XiaoHongShu)平台设计的 MCP 服务器，通过智能自动化技术为用户提供安全、高效的小红书运营工具。

## ✨ 核心特性

- **🔐 安全优先** - 所有内容操作仅保存为草稿，确保用户完全控制发布时机
- **🚀 启动即用** - MCP服务器启动时自动连接浏览器并验证登录状态，无需手动操作
- **🤖 智能搜索** - 支持多维度筛选的增强搜索功能，自动统计分析
- **📊 数据分析** - 自动生成 Excel 报告，包含数据质量和互动统计
- **👤 拟人化交互** - 模拟真人操作模式，智能防检测机制
- **🧪 完整测试** - 74 个单元测试，100% 通过率，保证代码质量
- **⚡ 现代架构** - 基于最新 .NET 9.0，使用依赖注入和异步编程模式

## 🚀 快速开始

### 前置要求

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) 或更高版本
- Chrome/Edge 浏览器（支持远程调试）
- [Claude Desktop](https://claude.ai/) (MCP 客户端)

### 1. 克隆和构建

```bash
# 克隆项目
git clone https://github.com/your-repo/XiaoHongShuMCP.git
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

### 3. 配置 Claude Desktop

编辑 Claude Desktop 配置文件：

**Windows**: `%APPDATA%\Claude\claude_desktop_config.json`  
**macOS**: `~/Library/Application Support/Claude/claude_desktop_config.json`

```json
{
  "mcpServers": {
    "xiaohongshu-mcp": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "D:\\path\\to\\XiaoHongShuMCP\\XiaoHongShuMCP"
      ],
      "env": {
        "DOTNET_ENVIRONMENT": "Development"
      }
    }
  }
}
```

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

- **ConnectToBrowser** - 连接浏览器并验证登录状态
- **SearchNotesEnhanced** - 智能搜索小红书笔记
- **GetNoteDetail** - 获取笔记详细信息
- **PostComment** - 发布评论
- **TemporarySaveAndLeave** - 保存笔记为草稿

## 📋 主要功能

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
- 登录状态实时监控
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

## 🏗️ 项目架构

```
XiaoHongShuMCP/
├── XiaoHongShuMCP/           # 主项目
│   ├── Services/             # 核心服务层
│   │   ├── AccountManager.cs               # 账号管理
│   │   ├── SearchDataService.cs            # 搜索数据服务
│   │   ├── XiaoHongShuService.cs           # 小红书核心服务
│   │   ├── PlaywrightBrowserManager.cs     # 浏览器管理
│   │   ├── SelectorManager.cs              # 选择器管理
│   │   ├── BrowserConnectionHostedService.cs # 后台连接服务
│   │   ├── HumanizedInteraction/           # 拟人化交互模块
│   │   │   ├── HumanizedInteractionService.cs # 主交互服务
│   │   │   ├── DelayManager.cs             # 智能延时管理
│   │   │   ├── ElementFinder.cs            # 高级元素查找
│   │   │   ├── SmartTextSplitter.cs        # 智能文本分割
│   │   │   └── TextInputStrategies.cs      # 文本输入策略
│   │   └── Interfaces.cs                   # 接口定义
│   ├── Tools/               # MCP 工具集
│   │   └── XiaoHongShuTools.cs            # MCP 工具定义
│   ├── Program.cs           # 程序入口
│   └── appsettings.json     # 配置文件
├── Tests/                   # 单元测试 (74个测试)
│   ├── Services/           # 服务测试
│   ├── Models/             # 模型测试  
│   └── Tools/              # 工具测试
└── README.md               # 项目文档
```

### 核心技术栈

- **[.NET 9.0](https://dotnet.microsoft.com/)** - 现代 C# 开发框架
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
dotnet test Tests --filter "ClassName=SearchDataServiceTests"

# 生成测试覆盖报告
dotnet test Tests --collect:"XPlat Code Coverage"
```

### 配置选项

编辑 `appsettings.json` 文件：

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

### 构建和部署

```bash
# 发布 Windows 版本
dotnet publish -c Release -r win-x64 --self-contained

# 发布 macOS 版本  
dotnet publish -c Release -r osx-x64 --self-contained

# 发布 Linux 版本
dotnet publish -c Release -r linux-x64 --self-contained
```

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

- **总测试数**: 74 个测试用例
- **通过率**: 100%
- **测试覆盖**: 服务层、数据模型、MCP 工具集
- **测试框架**: NUnit + Moq + Playwright

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

### 基础搜索

```typescript
// 在 Claude Desktop 中调用
await callTool("SearchNotesEnhanced", {
  keyword: "美食推荐",
  limit: 20,
  sortBy: "most_liked",
  noteType: "image"
});
```

### 获取笔记详情

```typescript  
await callTool("GetNoteDetail", {
  noteId: "xxxxxxxxxxxxxx",
  includeComments: true
});
```

### 连接浏览器

```typescript
await callTool("ConnectToBrowser", {});
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

### 常见问题

**Q: 无法连接到浏览器**  
A: 确保浏览器以远程调试模式启动，端口 9222 可访问

**Q: 登录状态检查失败**  
A: 手动在浏览器中登录小红书，确保登录状态有效

**Q: MCP 工具无法调用**  
A: 检查 Claude Desktop 配置文件语法，重启 Claude Desktop

**Q: 测试失败**  
A: 确保已正确安装 .NET 9.0 SDK，运行 `dotnet restore`

### 日志查看

项目日志保存在 `logs/` 目录：

```bash
# 查看最新日志
tail -f logs/xiaohongshu-mcp-*.txt

# 查看错误日志  
grep -i "error" logs/xiaohongshu-mcp-*.txt
```

## 📄 许可证

本项目采用 [MIT 许可证](./LICENSE)。

## 🔗 相关链接

- [Model Context Protocol 官方文档](https://modelcontextprotocol.io/)
- [.NET 9.0 文档](https://docs.microsoft.com/dotnet/)
- [Microsoft Playwright 文档](https://playwright.dev/dotnet/)
- [Claude Desktop 下载](https://claude.ai/)

## 📞 支持

- 🐛 [报告问题](https://github.com/your-repo/XiaoHongShuMCP/issues)
- 💡 [功能请求](https://github.com/your-repo/XiaoHongShuMCP/discussions)
- 📧 [联系我们](mailto:your-email@example.com)

---

<p align="center">
  <strong>⭐ 如果这个项目对您有帮助，请给我们一个 Star！</strong>
</p>

<p align="center">
  Made with ❤️ by XiaoHongShuMCP Team
</p>