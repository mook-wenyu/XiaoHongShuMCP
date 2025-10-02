# 贡献指南

> 更新日期：2025-10-02 15:16 (UTC+8)  
> 执行者：Codex

## 目录
- [编码规范](#编码规范)
  - [代码风格](#代码风格)
  - [开发约定](#开发约定)
  - [架构原则](#架构原则)
  - [实现标准](#实现标准)
- [测试策略](#测试策略)
  - [测试框架](#测试框架)
  - [运行测试](#运行测试)
  - [测试规范](#测试规范)
  - [质量标准](#质量标准)
- [贡献流程](#贡献流程)
  - [1. Fork 并创建分支](#1-fork-并创建分支)
  - [2. 编写代码和测试](#2-编写代码和测试)
  - [3. 本地验证](#3-本地验证)
  - [4. 提交 Pull Request](#4-提交-pull-request)
- [项目结构说明](#项目结构说明)
- [相关文档](#相关文档)
- [联系方式](#联系方式)

## 编码规范

### 代码风格
- 项目遵循 .NET 默认风格：四空格缩进、公共成员使用 PascalCase、私有字段使用 `_camelCase`。
- 配置 `Nullable` 以提升空引用安全性，所有可空类型需显式标注。
- 统一使用 UTF-8（无 BOM）编码保存源码、脚本与文档。

### 开发约定
- 提交信息建议遵循 Conventional Commits（如 `refactor(config): ...`）或提供清晰中文摘要，便于审计。
- 任何功能变更都必须同步更新相关文档，优先参考 `CLAUDE.md` 中的发布与文档规范。
- 坚持测试先行：在提交代码前确保新增或修改功能具备足够的单元测试覆盖。

### 架构原则
- 保持目录分层职责清晰，核心模块包括：`Configuration/`（配置项定义）、`Infrastructure/`（底层设施与外部依赖封装）、`Services/`（业务服务）与 `Tools/`（MCP 工具暴露层）。
- `Services/` 按领域进一步分组：`Browser/` 聚焦浏览器与指纹策略、`Humanization/` 管理人性化动作、`Notes/` 负责笔记交互、`Logging/` 处理 MCP 日志桥接。
- 共享资源存放在 `storage/`，所有持久化文件与导出内容需要经过目录约束管理，确保可追踪性。

### 实现标准
- 所有构建必须无警告，通过 `TreatWarningsAsErrors=true` 强制落实；提交前请确认 `dotnet build` 无告警输出。
- 重构或新增模块时优先复用现有基础设施，避免重复造轮子，并遵循单一职责与 SOLID 原则。
- 引入第三方依赖前需确认与现有配置体系兼容，并在 `docs/configuration.md` 中补充说明。

## 测试策略

### 测试框架
- 单元测试项目基于 `xUnit` 与 `Microsoft.NET.Test.Sdk`，目标框架为 `net8.0`。
- `Tests/HushOps.Servers.XiaoHongShu.Tests/` 会引用 `FingerprintBrowser.dll`，确保本地构建前已准备好该依赖。

### 运行测试

```bash
# 运行所有测试
dotnet test Tests/HushOps.Servers.XiaoHongShu.Tests/HushOps.Servers.XiaoHongShu.Tests.csproj

# 运行 Release 模式测试
dotnet test -c Release

# 运行特定测试
dotnet test --filter "FullyQualifiedName~HumanizedActionServiceTests"
```

### 测试规范
- 日常开发建议在 Debug 构建下快速回归，在发布前使用 Release 模式复核性能相关断言。
- 使用 `--filter` 精准定位特定用例时，请将筛选条件同步记录在 Pull Request 描述中，便于复现。
- 对浏览器相关测试，应提前执行 `Tools/install-playwright.ps1 --SkipIfPresent`（或相应脚本）以避免缺失依赖导致浏览器相关测试失败。

### 质量标准
- **测试覆盖率**：目标维持 70% 以上，对关键路径需补充集成测试。
- **代码风格**：保留 .NET 默认规则（四空格缩进、PascalCase 公共成员、`_camelCase` 私有字段）。
- **编译警告**：`TreatWarningsAsErrors` 为 `true`，任何警告都会导致构建失败。
- **可空引用**：启用 `Nullable` 引用类型，确保所有可空语义均显式处理。

## 贡献流程

### 1. Fork 并创建分支
- Fork 仓库后同步更新主干，避免落后于最新变更。
- 使用语义化分支名：`feature/`、`fix/`、`docs/` 等，示例命令如下：

```bash
git checkout -b feature/update-docs
```

### 2. 编写代码和测试
- 按照“编码规范”章节的约束实现功能，并保持最小可验证提交。
- 为新增功能补充单元测试，确保目标覆盖率 70%，必要时添加集成或端到端用例。
- 同步更新相关文档（含配置、操作指南），确保主 AI 可快速审阅。

### 3. 本地验证
- 在提交前执行完整构建与测试流程，推荐命令如下：

```bash
# 构建项目
dotnet build

# 运行测试
dotnet test

```

- 针对浏览器或网络相关变更，请附上关键日志或截图。

### 4. 提交 Pull Request
- 提供精炼的变更摘要，列出主要影响范围与验证结果。
- 附上测试执行证据（命令输出、截图或附件），并关联相关 Issue。
- 主动请求熟悉模块的审阅者参与评审，必要时邀请多位审阅者并标注优先级。

## 项目结构说明

```text
HushOps.Servers.XiaoHongShu/
├── Configuration/           # 配置选项类
├── Infrastructure/          # 基础设施（文件系统、工具执行封装）
├── Services/
│   ├── Browser/            # 浏览器自动化、指纹管理、网络策略
│   ├── Humanization/       # 人性化动作编排、行为控制、关键词解析
│   ├── Notes/              # 笔记互动、数据捕获、仓储
│   └── Logging/            # MCP 日志桥接
├── Tools/                  # MCP 工具暴露层
├── storage/                # 本地存储（浏览器配置、笔记数据、导出文件）
├── Tests/                  # 单元测试和集成测试
└── docs/                   # 项目文档（架构、设计决策、实现日志）
```

## 相关文档
- README.md
- CLAUDE.md
- docs/configuration.md

## 联系方式
- 问题反馈：通过 Issue 与维护团队沟通。
- 功能建议：提交讨论帖或 Pull Request 说明提案价值。
- 直接联系：317578863@qq.com。

