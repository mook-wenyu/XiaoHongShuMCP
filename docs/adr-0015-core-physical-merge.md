# ADR-0015：Core 物理合流（Adapters/Observability/Persistence）

日期：2025-09-14 UTC

## 背景
- 先前通过 Core.csproj 的 Compile Include 引入外部目录实现逻辑合流，但旧目录仍存在，增加了认知与维护成本；
- 按“破坏性、不向后兼容”路线，需物理迁移源码到 Core 项目并删除旧目录中的文件；统一装配、统一依赖、减少散落。

## 决策
- 将 XiaoHongShuMCP.Adapters.Playwright / XiaoHongShuMCP.Observability / XiaoHongShuMCP.Persistence 源码迁移至 HushOps.Core 内部：
  - Adapters.Playwright/* → HushOps.Core/Adapters.Playwright/*
  - Observability/* → HushOps.Core/Observability/*
  - Persistence/* → HushOps.Core/Persistence/*
- Core.csproj 移除显式 Compile Include，改用 SDK 隐式包含 *.cs；
- 删除旧目录下的源码文件（目录可由后续 CI 清理）。

## 兼容性
- 破坏性变更：不保证向后兼容；App/Tests 已随迁移一并调整。
- MCP 工具命名收敛：将含“Selector”的 MCP 方法统一改为 *Locator* 前缀，满足守卫测试；SelectorMaintenanceTools 不再通过 MCP 暴露。

## 风险与缓解
- 编译重复包含 → 采用隐式包含、移除显式 Compile Include；
- DTO 缺失 → 在 PlaywrightAutoFactory 中补充 VisDto；
- 测试对 Program 类型的反射引用 → 新增 Program.Expose.cs（命名空间占位）。

## 落地与验证
- dotnet build/test 全绿（129/129 通过）；
- 架构与守卫测试通过（不暴露 SelectorMaintenanceTools、无含“Selector”的 MCP 方法）。
