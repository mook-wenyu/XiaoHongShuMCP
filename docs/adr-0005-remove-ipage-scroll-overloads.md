# ADR-0005（已归档）：彻底移除 IPage 滚动重载，统一 IAutoPage 滚动

- 状态：Accepted（已合并）
- 日期：2025-09-13
- 变更类型：破坏性变更（不向后兼容）

## 背景与动机

为彻底解耦平台层与具体驱动（Playwright/CDP/外部反检测浏览器），我们已将核心交互统一抽象到 `IAuto*`。
此前滚动链路仍保留了 `IPage` 直连重载，导致：
- 门面接口/实现存在双轨，增加心智负担与维护成本；
- 业务层偶有误用具体驱动 API，破坏抽象边界；
- 后续替换驱动/引入反检测浏览器时，滚动路径成为迁移阻力。

## 决策

- 在 `IHumanizedInteractionService` 中移除以下签名：
  - `Task HumanScrollAsync(IPage page, CancellationToken ct = default)`
  - `Task HumanScrollAsync(IPage page, int targetDistance, bool waitForLoad = true, CancellationToken ct = default)`
- 在实现类 `HumanizedInteractionService` 中删除对应实现与仅用于该路径的私有探针方法：
  - `ExecuteSingleScrollAsync(IPage, ...)`
  - `ExecuteBasicScrollAsync(IPage, ...)`
  - `WaitForContentLoadAsync(...)`
  - `RefreshPageForNewContentAsync(IPage, ...)`
  - `PerformNaturalScrollAsync(IPage, ...)`
- 统一保留并强化 `IAutoPage` 版本：
  - `Task HumanScrollAsync(IAutoPage page, CancellationToken ct = default)`
  - `Task HumanScrollAsync(IAutoPage page, int targetDistance, bool waitForLoad = true, CancellationToken ct = default)`
  - 内部私有方法：`ExecuteScrollWithAutoAsync(IAutoPage, ...)`（`window.scrollBy`）

## 取舍

- 优点
  - 抽象边界清晰、驱动无关；
  - 业务代码统一使用 `IAutoPage`，降低封禁风险与心智成本；
  - 测试更聚焦于抽象契约，减少对白盒私有细节的耦合。
- 代价
  - 对少量仍调用 `IPage` 版本滚动的业务代码进行迁移；
  - 清理相关单元测试中对已删除私有方法的反射断言。

## 迁移指南

- 将原调用：
  ```csharp
  await _humanizedInteraction.HumanScrollAsync(page, targetDistance: 0, cancellationToken: ct);
  ```
  替换为：
  ```csharp
  var autoPage = PlaywrightAutoFactory.Wrap(page);
  await _humanizedInteraction.HumanScrollAsync(autoPage, targetDistance: 0, cancellationToken: ct);
  ```
- 若存在自定义滚动封装，统一改为传入 `IAutoPage`。

## 验收与验证

- 构建 `dotnet build`：0 警告（TreatWarningsAsErrors）。
- 单测 `dotnet test`：全部通过（107/107）。
- 定位并替换仓库中唯一遗留调用点（`XiaoHongShuService.cs:1416`）。
- 更新测试 `HumanizedInteractionVirtualScrollImprovementsTests`：不再断言 IPage 私有探针存在，改为断言 IAutoPage 路径存在且 IPage 重载已删除。

## 备注（归档说明）

本 ADR 中涉及的“OTel/Prometheus 拨盘与面板”路线已在 ADR‑0020 删除，当前仅保留本地 Console 指标导出。

## 后续工作（关联 ADR-0004，历史）

- 推进抽象滚动的策略扩展（虚拟化列表加载判定、边界/锚点滚动、可视区域采样）。
- 将 `UniversalApiMonitor` 指标接入 OTel/Prometheus，完善拨盘与面板。
- 提升关键链路测试覆盖率门槛（新增≥80%，核心≥90%）。
