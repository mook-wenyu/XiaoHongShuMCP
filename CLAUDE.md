# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 项目概述

HushOps.Servers.XiaoHongShu 是一个基于 .NET 8 的 Model Context Protocol (MCP) stdio 服务器，为小红书平台提供人性化自动化工具集。项目使用 Playwright 进行浏览器自动化，实现笔记浏览、点赞、收藏、评论等交互功能。

## 构建与测试命令

```bash
# 恢复依赖（首次构建必须）
dotnet restore

# 编译项目
dotnet build HushOps.Servers.XiaoHongShu.csproj
dotnet build HushOps.Servers.XiaoHongShu.csproj -c Release

# 运行服务器
dotnet run --project HushOps.Servers.XiaoHongShu.csproj

# 列出可用工具
dotnet run --project HushOps.Servers.XiaoHongShu.csproj -- --tools-list

# 运行测试
dotnet test Tests/HushOps.Servers.XiaoHongShu.Tests/HushOps.Servers.XiaoHongShu.Tests.csproj
dotnet test -c Release

# 运行验证场景（检查浏览器流程）
dotnet run -- --verification-run

# 单独运行特定测试
dotnet test --filter "FullyQualifiedName~HumanizedActionServiceTests"
```
> 注意：FingerprintBrowser 依赖通过仓库内的 `libs/FingerprintBrowser.dll` 等预编译 DLL 提供，构建或调试时不要尝试 `dotnet pack` 或配置 LocalFeed；如缺少 DLL，请提醒用户从交付包重新覆盖 `libs/` 目录。（FingerprintBrowser distribution relies on the prebuilt DLLs under `libs/`; do not attempt `dotnet pack` or LocalFeed. Re-copy the delivered `libs/` bundle if the DLL is missing.）


## 核心架构

### 服务层次结构

项目采用分层架构，主要模块包括：

1. **MCP 工具层 (Tools/)**：暴露给 MCP 客户端的工具接口
   - `BrowserTool`: 浏览器会话管理（工具：`browser_open`）
   - `BehaviorFlowTool`: 执行完整的人性化行为流程
   - `InteractionStepTool`: 执行单个业务级交互步骤（点赞、收藏、搜索、导航等业务工具）
   - `LowLevelInteractionTool`: 执行单个低级拟人化动作（工具：`ll_execute`，支持 Hover、Click、Wheel 等通用交互）
   - `NoteCaptureTool`: 批量捕获笔记数据
   - `NotePublishTool`: 发布笔记（工具：`xhs_publish_note`，支持上传图片、填写标题正文、暂存离开）

   **工具命名规范**：
   - **通用工具**：不使用 `xhs_` 前缀（如 `browser_open`、`ll_execute`），适用于任何网站的浏览器自动化
   - **业务工具**：使用 `xhs_` 前缀（如 `xhs_like_current`、`xhs_search_keyword`），专门用于小红书平台业务逻辑

   **工具分层原则**：
   - **通用工具层** (`BrowserTool`, `LowLevelInteractionTool`): 提供通用浏览器能力，不包含业务逻辑
   - **业务工具层** (`InteractionStepTool`): 面向小红书场景，简化参数，自动编排动作
   - **流程工具层** (`BehaviorFlowTool`): 编排完整业务流程，组合多个业务工具

   **工具选择指导**：
   - 优先使用业务工具：大多数小红书交互场景已封装为业务工具
   - 特殊场景使用低级工具：需要精确控制元素定位、时间参数、动作序列时使用 `ll_execute`

2. **服务层 (Services/)**：业务逻辑实现
   - `Browser/`: 浏览器自动化服务
     - `BrowserAutomationService`: 页面导航、随机浏览
     - `PlaywrightSessionManager`: Playwright 会话管理
     - `Fingerprint/ProfileFingerprintManager`: 浏览器指纹管理
     - `Network/NetworkStrategyManager`: 网络策略管理
   - `Humanization/`: 人性化行为编排
     - `HumanizedActionService`: 核心编排服务
     - `KeywordResolver`: 关键词解析
     - `HumanDelayProvider`: 延迟时间生成
     - `Behavior/DefaultBehaviorController`: 行为控制器
     - `Interactions/`: 交互执行器、脚本构建器、动作定位器
   - `Notes/`: 笔记相关服务
     - `NoteEngagementService`: 笔记互动（点赞、收藏等）
     - `NoteCaptureService`: 笔记数据捕获
     - `NoteRepository`: 笔记数据存储
   - `Logging/`: MCP 日志桥接

3. **配置层 (Configuration/)**：所有配置选项类
   - `XiaoHongShuOptions`: 默认关键词、画像、人性化节奏
   - `HumanBehaviorOptions`: 行为配置
   - `FingerprintOptions`: 指纹配置
   - `NetworkStrategyOptions`: 网络策略配置
   - `PlaywrightInstallationOptions`: Playwright 安装配置
   - `VerificationOptions`: 验证运行配置

4. **基础设施层 (Infrastructure/)**：横切关注点
   - `ToolExecution/`: 工具执行结果封装
   - `FileSystem/`: 文件系统抽象

### 依赖注入

所有服务在 `ServiceCollectionExtensions.AddXiaoHongShuServer()` 中注册为单例。工具类由 MCP 框架通过 `WithToolsFromAssembly()` 自动发现和注册（需要 `[McpServerToolType]` 和 `[McpServerTool]` 标记）。

### 配置系统

配置按优先级加载：
1. `appsettings.json`（可选）
2. `config/xiao-hong-shu.json`（可选）
3. 环境变量（前缀 `HUSHOPS_XHS_SERVER_`）

配置节名称：
- `xhs`: `XiaoHongShuOptions`
- `humanBehavior`: `HumanBehaviorOptions`
- `fingerprint`: `FingerprintOptions`
- `networkStrategy`: `NetworkStrategyOptions`
- `verification`: `VerificationOptions`
- `playwrightInstallation`: `PlaywrightInstallationOptions`

## 人性化行为系统

### 执行流程

#### 业务工具调用链

1. **关键词解析** (`KeywordResolver`): 从画像和请求参数中解析搜索关键词
2. **行为控制** (`BehaviorController`): 根据行为配置生成动作序列
3. **脚本构建** (`DefaultHumanizedActionScriptBuilder`): 将动作序列转换为可执行脚本
4. **交互执行** (`HumanizedInteractionExecutor`): 执行脚本中的每个动作
5. **一致性检查** (`SessionConsistencyInspector`): 记录执行指标

#### 低级工具调用链（绕过业务编排）

1. **参数验证**: 检查 `ActionLocator`、`Parameters`、`Timing`
2. **页面获取**: `BrowserAutomationService.EnsurePageContextAsync()` 获取浏览器上下文
3. **动作创建**: `HumanizedAction.Create()` 创建单个动作对象
4. **直接执行**: `HumanizedInteractionExecutor.ExecuteAsync()` 执行底层动作
5. **结果返回**: 返回 `OperationResult<InteractionStepResult>` 包含元数据

### 动作类型 (HumanizedActionType)

#### 业务工具动作类型（通过 HumanizedActionKind）

- `RandomBrowse`: 随机浏览
- `KeywordBrowse`: 关键词浏览
- `NavigateExplore`: 导航到发现页
- `SearchKeyword`: 搜索关键词
- `SelectNote`: 选择笔记
- `LikeCurrentNote`: 点赞当前笔记
- `FavoriteCurrentNote`: 收藏当前笔记
- `CommentCurrentNote`: 评论当前笔记
- `ScrollBrowse`: 拟人化滚动浏览
- `PublishNote`: 发布笔记（上传图片、填写标题正文、暂存离开）

#### 低级工具动作类型（HumanizedActionType）

通过 `xhs_ll_execute` 支持以下底层动作：

- `Hover`: 鼠标悬停到元素
- `Click`: 点击元素
- `MoveRandom`: 随机移动鼠标（模拟自然行为）
- `Wheel`: 滚轮滚动（指定距离）
- `ScrollTo`: 滚动到目标位置
- `InputText`: 输入文本（支持拟人化输入间隔）
- `PressKey`: 按下单个键
- `Hotkey`: 按下组合键（如 Ctrl+C）
- `WaitFor`: 等待元素出现
- `UploadFile`: 上传文件（用于文件输入框）
- `Delay`: 延迟等待（指定时长）
- `MoveToElement`: 移动鼠标到元素中心

### 定位器系统

- `ActionLocator`: 封装 Playwright 定位器配置（CSS/XPath/文本等）
- `ActionLocatorFactory`: 工厂方法创建常用定位器
- `InteractionLocatorBuilder`: 为特定动作构建定位器链

## 浏览器配置模式

### 配置键 (profileKey)

- `user`: 用户浏览器配置（可指定 `profilePath` 或自动探测）
- 其他值: 独立配置，存储在 `storage/browser-profiles/<profileKey>`

### 会话缓存

- 每个 `profileKey` 对应一个 Playwright 浏览器上下文
- 返回元数据包括：`isNewProfile`、`usedFallbackPath`、`alreadyOpen`、`autoOpened`

## 测试策略

- 使用 xUnit 框架
- 测试命名：`方法_场景_结果`（中文描述）
- 测试覆盖率目标：70%
- 关键测试：
  - MCP 日志能力测试
  - 人性化动作服务测试
  - 行为控制器测试
  - 脚本构建器测试
  - 笔记捕获工具测试

## 编码规范

- 四空格缩进
- 公共成员使用 PascalCase
- 私有字段使用 `_camelCase`
- 启用 `Nullable` 引用类型
- `TreatWarningsAsErrors` 为 true
- 所有文档和注释使用中文
- 配置类命名使用 `Options` 后缀
- 服务接口以 `I` 开头

## 架构原则

### 工具层分层规则

1. **业务工具与低级工具隔离**：
   - 业务工具 (`InteractionStepTool`) 调用 `IHumanizedActionService`（高级业务服务）
   - 低级工具 (`LowLevelInteractionTool`) 调用 `IHumanizedInteractionExecutor`（底层执行器）
   - 禁止在同一工具类中混用两种抽象层次

2. **参数复杂度匹配抽象层次**：
   - 业务工具：简单参数（`browserKey`、`behaviorProfile`、`keyword`、`commentText`）
   - 低级工具：复杂参数（`ActionLocator`、`HumanizedActionParameters`、`HumanizedActionTiming`）

3. **工具命名规范**：
   - 业务工具：`xhs_<业务动作>` (如 `xhs_like_current`、`xhs_search_keyword`)
   - 低级工具：`xhs_ll_<技术动作>` (如 `xhs_ll_execute`)
   - 流程工具：`xhs_flow_<流程名>` (如 `xhs_flow_browse`)

4. **向后兼容策略**：
   - 工具迁移使用 `[Obsolete(false)]` 标记（允许编译但有警告）
   - 保留旧接口至少一个大版本周期（v1.x → v2.0）
   - 在工具描述中添加迁移指引（中英双语）
   - 使用转发模式实现向后兼容（旧工具转发到新工具）

5. **Claude 使用建议**：
   - 优先使用业务工具：覆盖 80% 的常见场景
   - 业务工具无法满足时使用低级工具：需要精确控制时
   - 避免使用已弃用工具：查看工具描述中的 ⚠️ 标记

## 文档规范

根据 `AGENTS.md` 要求：
- 重大变更必须在 `docs/` 目录下记录
- 每个任务在 `docs/workstreams/<TASK-ID>/` 下建立完整文档
- 顶层文档包括：`requirements.md`、`design.md`、`tasks.md`、`implementation.md`、`coding-log.md`、`testing.md`、`changelog.md`
- 任务级文档包括：`research.md`、`design.md`、`plan.md`、`implementation.md`、`verification.md`、`delivery.md`、`changelog.md`、`operations-log.md`

## 关键约束

1. **工具约束**：禁止使用 shell/python 等直接命令读写文件
2. **安全约束**：禁止 `rm -rf` 等破坏性操作
3. **提交规范**：使用 Conventional Commits 格式或精炼中文标题
4. **测试先行**：提交前必须通过所有测试
5. **文档同步**：代码变更必须同步更新文档
