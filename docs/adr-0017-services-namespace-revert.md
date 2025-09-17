# ADR-0017：服务层与工具命名空间回归 XiaoHongShuMCP（破坏性变更）

日期：2025-09-14

## 背景
此前（ADR-0016）将服务层命名空间迁移为 `HushOps.Services.*`，与核心装配 `HushOps.Core` 同步。但根据名称边界收敛要求：

- 应用层维持 XiaoHongShuMCP 品牌与命名空间；
- 仅核心装配保持 `HushOps.Core`；
- 服务与工具归属于应用层命名空间，便于日志覆盖、配置与对外呈现一致。

## 决策
- 将所有服务层命名空间从 `HushOps.Services.*` 回归为 `XiaoHongShuMCP.Services.*`；
- 将 MCP 工具命名空间从 `HushOps.Tools` 回归为 `XiaoHongShuMCP.Tools`；
- 应用装配名与根命名空间恢复为 `XiaoHongShuMCP`；
- 调整测试与文档中对命名空间的引用与架构守卫规则；
- 不保留兼容别名（破坏性、不向后兼容）。

## 影响
- 需要更新 DI 注册、MCP 工具类型反射、NetArchTest 规则与示例文档；
- 外部依赖应用命名空间的调用方需同步修正；
- Core 维持 `HushOps.Core` 不变。

## 迁移步骤（已落地）
1. 批量替换 `namespace HushOps.Services` → `namespace XiaoHongShuMCP.Services`；
2. 批量替换 `namespace HushOps.Tools` → `namespace XiaoHongShuMCP.Tools`；
3. 修正 `Program.cs` 的 using 与 DI 注册；
4. 更新测试：`typeof(XiaoHongShuMCP.Program).Assembly`、`XiaoHongShuMCP.Services.*`、`XiaoHongShuMCP.Tools.*`；
5. 更新示例与 README/CLAUDE 中的日志覆盖样例；
6. 全量运行测试，确保通过。

## 备选方案
- 增加全局 using 别名维持双命名空间：被拒绝（不可见的双品牌足迹，且违背“破坏性演进”）。

## 回滚
- 以该 ADR 补丁的反向修改即可回滚（不建议）。

