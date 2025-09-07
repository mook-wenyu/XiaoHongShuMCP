# 代码规范和设计模式

## 编码规范

### 命名约定
- **类名**: PascalCase (例: `XiaoHongShuService`)
- **接口名**: 以 I 开头的 PascalCase (例: `IXiaoHongShuService`)
- **方法名**: PascalCase (例: `GetNoteDetailAsync`)
- **属性名**: PascalCase (例: `IsLoggedIn`)
- **字段名**: camelCase，私有字段以下划线开头 (例: `_logger`)
- **参数名**: camelCase (例: `noteId`)
- **常量名**: PascalCase (例: `MaxRetryCount`)

### 类型和空值处理
- 启用 nullable reference types
- 使用 `string?` 表示可空字符串
- 使用 `List<T>` 而不是 `IList<T>` 作为方法参数
- 优先使用 `record` 类型定义数据传输对象

### 异步编程
- 所有 I/O 操作使用异步方法
- 异步方法以 `Async` 结尾
- 使用 `Task<T>` 和 `Task` 返回类型
- 避免使用 `.Result` 或 `.Wait()`

### 错误处理
- 使用统一的 `OperationResult<T>` 模式
- 不要吞没异常，记录详细错误信息
- 使用结构化日志记录 (Serilog)

## 设计模式

### 核心设计原则
1. **SOLID 原则**: 单一职责、开闭原则、里氏替换、接口隔离、依赖倒置
2. **依赖注入**: 使用 Microsoft.Extensions.DependencyInjection
3. **接口隔离**: 每个服务都有对应的接口定义
4. **门面模式**: HumanizedInteractionService 协调各个专门服务

### 架构模式
- **服务层架构**: Services 目录包含所有业务逻辑
- **数据传输对象**: 使用 record 类型定义 DTO
- **工厂模式**: BrowserManager 管理浏览器实例
- **策略模式**: TextInputStrategies 提供不同输入策略
- **模板方法**: 拟人化交互的统一流程

### 拟人化交互系统
- **HumanizedInteractionService**: 门面服务协调各模块
- **DelayManager**: 智能延时管理
- **ElementFinder**: 高级元素查找，支持多选择器容错
- **SmartTextSplitter**: 智能文本分割，模拟真人输入
- **TextInputStrategies**: 多种自然文本输入策略

## XML 文档注释
所有公共类、接口、方法都必须有完整的 XML 文档：
```csharp
/// <summary>
/// 获取笔记详情的异步方法
/// </summary>
/// <param name="noteId">笔记ID</param>
/// <param name="includeComments">是否包含评论</param>
/// <returns>包含笔记详情的操作结果</returns>
```