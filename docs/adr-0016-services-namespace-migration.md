# ADR-0016：Services 命名空间迁移至 HushOps.Services（破坏性变更）

日期：2025-09-14

## 背景

在 ADR-0015 中我们已完成 Core/Adapters/Observability/Persistence 的装配与命名空间统一到 `HushOps.*`，但编排/服务层仍以 `XiaoHongShuMCP.Services` 暴露，导致品牌与分层不一致，且在日志覆盖与架构守卫中需要双份规则。

## 决策

- 将所有服务层命名空间从 `XiaoHongShuMCP.Services*` 迁移为 `HushOps.Services*`（含子命名空间：`HumanizedInteraction`、`LocatorPolicy`、`Resumable`、`Utilities`、`Concurrency`）。
- 同步更新：
  - 应用入口 `Program.cs` 的依赖注册与 `HostedService` 泛型参数。
  - MCP 工具与内部工具中的引用（`XiaoHongShuTools`、`SelectorMaintenanceTools`、`AntiDetectionTools`）。
  - 单元测试与示例文档中对旧命名空间的使用。
  - 保持目录结构暂不物理迁移（下一阶段执行），以降低一次性风险；CI 守卫白名单仍指向旧的物理路径，后续目录迁移时一并更新。

## 影响

- 破坏性：对外 API/类型名全部改为 `HushOps.Services.*`，不再兼容旧命名空间。
- 文档与日志覆盖示例中的命名空间字符串需同步替换。
- 架构规则（NetArchTest）已基于 `HushOps.Services`，无需额外调整。

## 迁移步骤摘要

1. 批量替换命名空间声明与 `using` 引用；修正少量全限定名（例如 `Resumable*Operation` 的别名 `using`）。
2. 更新 `Program.cs` 依赖注入注册中的类型。
3. 更新 Tools 与 Tests 中使用的命名空间与类型名。
4. 更新文档（README、CLAUDE、AGENTS、migration-ops-config）。
5. 运行编译与测试，确保通过；使用 `rg` 确认仓库不再出现 `XiaoHongShuMCP.Services`。

## 备选方案与取舍

- A. 同时重命名物理目录（`XiaoHongShuMCP/Services` → `HushOps/Services`）与 `.sln` 路径。
  - 优点：品牌痕迹彻底清理；缺点：一次性改动大、风险与审阅成本高。
- B. 分两步：先命名空间迁移（当前），下个版本再做目录与解决方案路径重命名，并同步更新 CI 守卫路径。
  - 选择：B（渐进式、可控回滚）。

## 回滚方案

- 使用补丁撤销本次变更；或以 `sed/rg` 批量替换回 `XiaoHongShuMCP.Services` 并恢复文档。

## 兼容性与安全

- 不向后兼容：鼓励尽快在上下游代码中替换引用。
- 反检测策略不变：仍禁止业务层 JS 注入；`Utilities.IEvalGuard` 仅用于只读 Evaluate 的受控门控与计量。

---

实现者：HushOps 团队

