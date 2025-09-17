# ADR-0006：输入链路“零抽象泄漏”与输入设备抽象补齐（破坏性变更）

日期：2025-09-13

## 背景与动机

- 现状：部分服务接口与策略仍直接暴露/依赖具体驱动类型（`IPage`/`IElementHandle`），导致：
  - （历史）驱动可替换性受限（无法平滑切换到 CDP/外部反检测浏览器）。
  - 上层服务绕过抽象（unwrap）使用底层句柄，形成技术债。
- 目标：统一以 `IAuto*` 抽象编程，彻底移除输入链路中的 `IPage/IElementHandle` 暴露；补齐输入相关能力抽象，避免上层触碰底层 API。

## 决策

1) 新增并落地以下抽象（Core）：
   - `IKeyboard`：`TypeAsync(text, delayMs?)`、`PressAsync(key, delayMs?)`
   - `IClipboard`：`WriteTextAsync/ReadTextAsync`
   - `IFilePicker`：`SetFilesAsync(selector|IAutoElement, filePaths)`
   - 在 `IAutoPage` 上新增只读属性：`Keyboard`、`Clipboard`、`FilePicker`

2) 适配器实现（Playwright）：
   - `PlaywrightKeyboard` → 映射到 `page.Keyboard.*`
   - `PlaywrightClipboard` → 通过 `evaluate(navigator.clipboard)` 读写（权限受限时抛出异常）
   - `PlaywrightFilePicker` → 基于 `Locator.SetInputFilesAsync`/`ElementHandle.SetInputFilesAsync`

3) 破坏性接口调整：
   - `ITextInputStrategy` 改为面向 `IAutoPage + IAutoElement`；不再接受 `IPage/IElementHandle`
   - `IHumanizedInteractionService`：
     - 删除 `HumanClickAsync(IElementHandle)`；保留 `HumanClickAsync(IAutoElement)`
     - `HumanHoverAsync(IElementHandle)` → `HumanHoverAsync(IAutoElement)`
     - `FindElementAsync(IPage, …)` → `FindElementAsync(IAutoPage, …)`
     - `HumanFavorite/Unlike/Unfavorite(IPage)` → `Human…(IAutoPage)`

4) 策略与服务落地：
   - 文本输入策略（Regular/ContentEditable）全面改为 `IAuto*`，移除对 `GetAttributeAsync` 等底层 API 的依赖，统一以 `EvaluateAsync` 读取属性。
   - `HumanizedInteractionService.HumanTypeAsync(IAutoPage, …)` 取消 unwrap，完全基于 `IAuto*` 与策略执行。
   - 交互动作中的快捷键（如 Enter/全选/删除）统一通过 `IAutoPage.Keyboard` 触发。

5) 调用方升级：
   - 所有传入 `IElementHandle` 的点击/悬停调用改为传入 `IAutoElement`（在 Playwright 场景使用 `PlaywrightAutoFactory.Wrap(handle)` 过渡）。
   - 所有 `FindElementAsync(IPage, …)` 改为 `FindElementAsync(IAutoPage, …)`；必要时以 `PlaywrightAutoFactory.Wrap(page)` 过渡。

## 取舍与影响

- 优点：
  - 驱动可替换性显著提升；上层不再感知底层句柄类型。
  - 输入链路语义更清晰（键盘/剪贴板/文件选择能力显式化）。
  - 测试更易模拟（为 `IAutoPage` 提供最小假实现）。
- 约束：
  - 需一次性迁移调用点（破坏性）；
  - 个别 DOM 相对查询改为通过页面级查询 + 遥测别名规约，避免在抽象元素上暴露 `QuerySelector` 能力。

## 兼容与迁移

- 本次为破坏性发布：删除/替换接口签名，不向后兼容。
- 已更新所有编译路径与测试；`dotnet test` 109/109 通过。

## 验收与度量

- 编译：0 警告（TreatWarningsAsErrors=true）。
- 单测：新增/调整策略与人机交互相关用例，全部通过。
- 抽象泄漏检查：`rg 'IElementHandle|IPage'` 在服务接口与策略中无残留（实现内部允许小范围使用，逐步替换）。

## 后续规划

- 在 `IAutoElement` 上提供相对查询（可选）：`QueryAsyncWithin`，减少页面级查询传参。
- 将点赞/收藏/取消收藏在 DOM 读取路径也统一为 `IAuto*`（当前已迁移点击与关键读取；余下路径逐步剥离）。
- 推进 M1 三分架构拆包（Core/Adapters/Observability/Hosts），以 NetArchTest 守卫依赖方向。
