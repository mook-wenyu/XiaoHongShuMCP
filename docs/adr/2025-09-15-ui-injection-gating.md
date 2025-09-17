# ADR: UI 注入兜底统一门控（基于 AntiDetectionPipeline）

日期：2025-09-15

## 背景

业务层历史上存在少量 JS 注入兜底（例如 `dispatchEvent('click')`），虽默认禁用，但仍分散在服务代码中，缺乏统一审计与策略放行点，不利于反检测治理与可追溯性。

## 决策

1. 引入受控注入接口：在 Core 的 `IPlaywrightAntiDetectionPipeline` 中新增 `TryUiInjectionAsync`（元素级与页面级）。
2. 策略来源：扩展 `IAntiDetectionPolicy`，新增 `AllowUiInjectionFallback`（环境变量 `XHS__AntiDetection__EnableJsInjectionFallback`）。
3. 业务层改造：`HumanizedClickPolicy` 的注入兜底路径不再直接调用 `EvaluateAsync`，统一通过 AntiDetectionPipeline 执行并写审计。
4. 双重门控：注入兜底需同时满足
   - 业务交互策略开关：`XHS__InteractionPolicy__EnableJsInjectionFallback=true`
   - 反检测策略放行：`XHS__AntiDetection__EnableJsInjectionFallback=true`
5. 只读 Evaluate 仍由 `PlaywrightAdapterTelemetry` 门控与计量，目标是逐步清零并替换为适配器探针 API。

## 影响

- 破坏性变更：
  - Core 接口 `IAntiDetectionPolicy` 新增属性；
  - `IPlaywrightAntiDetectionPipeline` 新增方法；
  - `HumanizedClickPolicy` 构造函数新增可选依赖 `IPlaywrightAntiDetectionPipeline`。
- 正向收益：统一注入审计（`.audit`）、门控与指标（`ui_injection_total`），杜绝“静默注入”。
- 风险：极端兜底路径在默认策略下不再生效（安全默认），需通过配置显式启用。

## 测试

- 新增 `HumanizedClickPolicyTests`：验证门控关闭不注入、双开关开启则受控注入；
- 新增 `PostCommentPolicyTests`：验证 PostComment 使用入口级策略（写类：限流+熔断）。

## 迁移指南

- 若业务确需短期启用注入兜底（不推荐），请同时打开：
  - `XHS__InteractionPolicy__EnableJsInjectionFallback=true`
  - `XHS__AntiDetection__EnableJsInjectionFallback=true`
- 观察 `.audit` 与指标 `ui_injection_total{type=dispatchEvent}`，确保为零或尽快清零。

## 后续工作

- 扫描并移除/改造其他潜在注入兜底点；
- 将路径级曝光守卫替换为程序集特性扫描；
- 逐步收敛只读 Evaluate 白名单，推进到默认禁用。

