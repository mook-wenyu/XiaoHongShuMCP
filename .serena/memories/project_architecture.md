# 项目架构和核心组件

## 项目结构
```
XiaoHongShuMCP/
├── XiaoHongShuMCP/                 # 主项目
│   ├── Services/                   # 核心服务层
│   │   ├── Interfaces.cs           # 接口定义和数据模型
│   │   ├── AccountManager.cs       # 账号管理服务
│   │   ├── PlaywrightBrowserManager.cs # 浏览器管理
│   │   ├── SelectorManager.cs      # CSS 选择器管理
│   │   ├── SearchDataService.cs    # 搜索和数据服务
│   │   ├── XiaoHongShuService.cs   # 小红书核心服务
│   │   ├── BrowserConnectionHostedService.cs # 后台连接服务
│   │   └── HumanizedInteraction/   # 拟人化交互模块
│   │       ├── HumanizedInteractionService.cs # 主交互服务
│   │       ├── DelayManager.cs     # 智能延时管理
│   │       ├── ElementFinder.cs    # 高级元素查找
│   │       ├── SmartTextSplitter.cs # 智能文本分割
│   │       └── TextInputStrategies.cs # 文本输入策略
│   ├── Tools/                      # MCP 工具集
│   │   └── XiaoHongShuTools.cs     # MCP 工具定义
│   ├── Program.cs                  # 程序入口
│   └── appsettings.json           # 配置文件
├── Tests/                          # 单元测试 (74个测试)
└── README.md                      # 项目文档
```

## 核心接口
- **IXiaoHongShuService**: 小红书核心功能接口
- **IAccountManager**: 账号管理接口
- **IBrowserManager**: 浏览器管理接口
- **ISearchDataService**: 搜索数据服务接口
- **IHumanizedInteractionService**: 拟人化交互服务接口
- **ISelectorManager**: 选择器管理接口

## 数据模型
- **NoteInfo**: 笔记基本信息
- **NoteDetail**: 笔记详细信息（继承自 NoteInfo）
- **UserInfo**: 用户信息
- **OperationResult<T>**: 统一操作结果
- **SearchRequest/SearchResult**: 搜索相关模型
- **SearchStatistics**: 搜索统计信息

## 核心功能模块

### 1. 浏览器自动化
- 使用 Microsoft Playwright 1.54.0
- 连接现有浏览器会话 (端口 9222)
- Cookie 登录检测机制
- 会话管理和资源释放

### 2. 拟人化交互系统
- 模块化设计，职责分离
- 智能延时管理
- 多选择器容错机制  
- 自然文本输入策略
- 防检测机制

### 3. 数据处理和导出
- 自动统计分析
- Excel 导出功能 (NPOI)
- 数据质量评估
- 批量处理优化

### 4. 安全机制
- 仅草稿模式操作
- 本地数据处理
- 日志脱敏处理
- 智能防检测