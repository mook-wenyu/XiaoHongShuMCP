# ADR-0004（已归档）：监听器抽象（INetworkMonitor）与桥接删除

日期：2025-09-13

## 背景

- 业务层已大规模迁移至 `IAuto*` 抽象，但网络监听仍直接耦合 Playwright（`IBrowserContext.Response`）。
- 历史上为兼容 IPage 使用，存在 `Services/Extensions/PageAbstractionBridges.cs` 扩展桥接；这增加了心智负担与技术债。

## 决策

1. 引入驱动无关的监听抽象：`INetworkMonitor` 与 `INetworkResponse`（位于 `Core/Automation/Abstractions`）。
2. 新增 Playwright 适配器：`PlaywrightNetworkMonitor`（位于 `Adapters/Playwright`）。
3. 改造 `UniversalApiMonitor`：
   - 构造函数新增可选依赖 `INetworkMonitor`（为空时使用 Null 实现，便于单测）。
   - `SetupMonitor` 签名破坏性修改为接受 `IAutoPage`；内部只依赖抽象事件，不直接接触 Playwright 类型。
   - 响应处理回调从 `IResponse` 改为 `INetworkResponse`。
4. 破坏性删除历史桥接文件：`Services/Extensions/PageAbstractionBridges.cs`。
5. 统一输入抽象：接口 `IHumanizedInteractionService` 移除 `HumanTypeAsync(IPage,…)`，仅保留 `IAutoPage` 版本；实现中保留旧方法但抛出 `NotSupportedException` 以防误用（后续版本将彻底移除定义）。
6. 删除 XiaoHongShuService 中全部 `IElementHandle` 老路径方法（查找/过滤/文本/ID/可见区/虚拟化搜索），统一使用 `IAutoElement` 版本。

## 影响

- 业务调用需要在传入监听器时使用 `PlaywrightAutoFactory.Wrap(page)` 获得 `IAutoPage`。
- 依赖 `PageAbstractionBridges` 的调用点需显式改用 `IAuto*`。
- 单测保持通过（106/106），编译均成功；新增警告保持在可控范围，后续 CI 将收紧为“警告即失败”。

## 回滚策略

- 如监听抽象导致线上异常（24h 内失败率 > 5% 或封禁告警异常上升），回滚至上一个稳定 Tag；同时关闭新监听抽象拨盘。

## 备注（归档说明）

本 ADR 提及的“更多驱动适配（CDP/外部）与拨盘/告警”路线已在 ADR‑0020 中被删除，当前项目聚焦“本地‑only/stdio‑only/单驱动（Playwright）”。
