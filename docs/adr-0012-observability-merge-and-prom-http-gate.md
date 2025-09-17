# ADR-0012（已归档）：观测源码合流到 Core 与 PROM_HTTP 编译门控

日期：2025-09-14

## 背景

- 早期为降低耦合，`Observability` 作为独立工程提供 `OtelMetrics` 与 `AddObservability` 装配扩展（见 ADR-0007）。
- 随着“禁注入 + 唯一监听 + 可恢复 + 可观测 + 架构守卫”的基座成型，保持装配统一、删除多余工程成为目标（降低维护面、统一版本）。
- （历史）开发/验证阶段打开 Prometheus HttpListener 的路线已终止；当前仅保留本地 Console 指标导出。

## 决策

1. 以“源码并入”的方式将 `XiaoHongShuMCP.Observability` 编译进 `HushOps.Core`（`Core.csproj` 使用 `<Compile Include="..\\XiaoHongShuMCP.Observability\\**\\*.cs" />`）。
   - 应用与测试仅引用 `Core`，避免多工程散落。
2. 引入 `PROM_HTTP` 编译门控：
   - `#if PROM_HTTP` 下直接调用 `AddPrometheusHttpListener`；
   - 未定义时走“反射尝试”，失败则回退 Console 导出器；
   - 生产默认导出器为 OTLP。
3. 提供 CI 作业样例以编译验证 `PROM_HTTP`（仅 Dev/POC），同时保留常规构建与测试作业。

## 影响

- 优点：装配收敛，统一发布面；避免跨项目版本漂移；降低安全与依赖升级成本。
- 风险：短期内命名空间仍保留 `XiaoHongShuMCP.Observability`（源文件未物理搬移），但已由 `Core` 编译产出；后续可择机进行物理合并与命名空间迁移。

## 迁移

- 应用侧仅需继续调用 `services.AddObservability(configuration)`；无需额外引用独立 Observability 工程。
- CI 新增 `.github/workflows/ci.yml`，含 `build-prom-http-gate` 作业做门控编译验证。

## 取代关系

- 部分取代 ADR-0007（将“独立工程”策略调整为“源码合流到 Core 编译”）。
