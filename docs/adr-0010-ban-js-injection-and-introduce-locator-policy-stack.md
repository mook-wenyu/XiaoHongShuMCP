# ADR-0010：禁用非必要 JS 注入 + 引入定位策略栈（ILocatorPolicyStack）

日期：2025-09-13

## 背景

历史上在个别场景使用了 `window.scrollBy` 与 `dispatchEvent('click')` 作为滚动/点击兜底。这些路径会形成“脚本注入指纹”，不利于反检测与可观测治理。

## 决策

1. 破坏性更改：
   - 删除/替换所有滚动注入：`window.scrollBy` → `Mouse.Wheel` 分段滚动 + `ScrollIntoViewIfNeeded`。
   - 点击兜底仅在“策略门控”下允许 `dispatchEvent('click')`，默认禁用；使用即计量 `ui_injection_total{type=dispatchEvent}`，并配置告警阈值（>0 告警）。
2. 新指标：
   - `ui_injection_total`（Counter）：记录 UI 注入兜底的使用计数；标签白名单新增 `type/path/stage`。
   - `locate_stage_duration_ms`（Histogram）：定位阶段耗时；标签：`role/name`。
   - `locate_attempts_total`（Counter）：定位尝试计数；标签：`strategy/role/name`。
3. 定位策略栈（ILocatorPolicyStack，接口分阶段提交）：
   - 优先级：A11y 语义（getByRole/Label/Text/Placeholder）→ 组合/相对（has/hasText/Visible）→ 键盘/滚轮 → 网络监听确认；视觉兜底方案已移除，统一依赖 Locator 与 DOM 选择器。
4. CI 守卫：新增 `InjectionUsageGuardTests` 扫描禁止的注入模式（业务层）。
5. 标签白名单扩展：在 Observability 增加 `strategy`，用于区分定位策略路径，基数可控。

## 影响

- HumanizedInteractionService：滚动改为分段滚轮；不再注入 `window.scrollBy`。
- HumanizedClickPolicy：增加门控与指标；默认禁用 JS 注入兜底。
- TextInputStrategies：移除 `dispatchEvent('input'/'change')`，统一使用键盘序列。
- Observability：指标标签白名单扩展为 `endpoint,status,hint,accounttier,region,stage,type,path,role,name,personaid`。

## 迁移

- 配置新增节：`XHS:InteractionPolicy:*`
  - `EnableJsInjectionFallback`（默认 false）
  - `EnableJsScrollInjection`（默认 false）
  迁移路径：无需配置变更即可获得更安全的默认行为。

## 验证

- 单测：`InjectionUsageGuardTests` 保证禁止模式不再出现于业务层。
- 行为：滚动/点击/输入路径均通过拟人化输入实现；必要时可开启门控验证极端场景。
