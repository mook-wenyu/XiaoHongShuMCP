# ADR-0001：将 Adapters 合并入 Core（Core.Runtime.Playwright.*）

日期：2025-09-14（PT）

状态：Accepted（已落地）

## 背景

历史版本通过独立 `HushOps.Adapters.Playwright` 层承载运行时（Playwright）实现，Core 保持“纯抽象”。随着反检测、拟人化节律与韧性策略收敛到核心域，跨项目/跨层同步与版本治理成本上升，且适配层暴露了不必要的运行时细节。

## 决策

- 删除独立 Adapters 项，合并入 Core 的子命名空间：`HushOps.Core.Runtime.Playwright.*`；
- Core 内部实行“策略/运行时分舱”：`Core.Policy.*`、`Core.Runtime.*`、`Core.Observability.*`、`Core.Resilience.*`；
- 引入 `HushOps.Core.Browser.IBrowserRuntime` 门面（Playwright 实现为 `PlaywrightBrowserDriver`）；
- 仅允许 `Core.Runtime.Playwright.*` 依赖 `Microsoft.Playwright`；通过 NetArchTest 守卫其他命名空间禁止引用；
- MCP 层仅编排与审计，引用 Core 私有包的运行时门面与抽象接口，禁止直接 Evaluate/注入。

## 影响

- 命名空间变更：`HushOps.Adapters.Playwright.* → HushOps.Core.Runtime.Playwright.*`；
- 反检测模型：统一 `HushOps.Core.AntiDetection.AntiDetectionSnapshot` 扩展属性集；
- 观察性：`AdapterTelemetryBootstrapperHostedService` 反射目标更新为 `HushOps.Core.Runtime.Playwright.PlaywrightAdapterTelemetry`；
- 架构守卫：新增/更新两套 NetArchTest 规则（Core 非 Runtime 禁 Playwright；Runtime 禁依赖服务层命名空间）。

## 备选方案

- 继续维持独立 Adapters 项：放弃，带来双处变更与版本漂移；
- 拆分为多个包：暂不需要，内部命名空间分舱 + 守卫测试足以控制依赖边界。

## 迁移与验证

- 代码迁移与全局替换完成；编译通过；
- 单测 153/153 全绿；新增 `ArchitectureGuards` 测试确保边界不回归；
- Directory.Packages.props 集中治理 Playwright/OTel/Polly/RateLimiting 与测试依赖版本；
- `.audit` 与 OTel 指标路径保持不变。

## 风险与缓解

- Core 变“胖”与可替换性下降：通过 `IBrowserRuntime` 门面与内部命名空间隔离保留切换空间；
- 注入滥用风险：仍坚持“只读 Evaluate 白名单”，注入仅在 AntiDetectionPipeline；指标 `ui_injection_total` 告警。

