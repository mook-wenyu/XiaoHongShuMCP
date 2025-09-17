# 定位策略栈（LocatorPolicyStack）设计与用法

日期：2025-09-13

## 目标

- 在不使用 JS 注入的前提下，以可访问/语义优先的方式完成元素定位，增强鲁棒性与可解释性。
- 统一“意图 → 策略序列”模型，减少业务层散落的选择器逻辑。
- 提供可观测：定位阶段耗时与尝试计数指标，便于面板优化与告警。

## 接口

```csharp
public interface ILocatorPolicyStack
{
    Task<LocatorAcquireResult> AcquireAsync(IAutoPage page, LocatorHint hint, CancellationToken ct = default);
}

public sealed class LocatorHint
{
    public IReadOnlyList<string> Aliases { get; init; } = Array.Empty<string>();
    public string? Role { get; init; }          // button/link/textbox
    public string? NameOrText { get; init; }    // 可访问名称/可见文本/占位符关键词
    public IReadOnlyList<string> ContainerAliases { get; init; } = Array.Empty<string>();
    public bool VisibleOnly { get; init; } = true;
    public int StepTimeoutMs { get; init; } = 3000;
}
```

## 策略顺序（默认实现）

1. A11y/语义：`role + :has-text()`（容器范围优先）
2. 别名：`alias` → `alias:has-text()`（如提供 `NameOrText`）
3. 容器+别名组合：`container alias`（可带 `:has-text()`）
4. 文本引擎：`text=...`（容器范围优先）
5. 放弃（返回 null）

> 不使用 JS 注入；全部依赖 Playwright 选择器/文本引擎与自动等待能力。

## 可观测指标

- `locate_stage_duration_ms{stage=locate,role,name}`：定位阶段耗时
- `locate_attempts_total{strategy,role,name}`：定位尝试计数
- 允许标签白名单：`role,name,strategy`（已在 Observability 默认白名单中启用）

## 接入点

- ResumableSearchOperation：搜索框定位（role=textbox + alias=SearchInput + name≈“搜”）
- ResumableInteractOperation：搜索输入与结果卡片定位（alias+has-text≈keyword）
- ResumableHomefeedOperation：主滚动容器定位（alias=MainScrollContainer）

## 最佳实践

- 优先传入 `Role/NameOrText`，让策略首先走 A11y/语义路径；
- 提供 `ContainerAliases` 缩小范围；
- `Aliases` 仅做稳定 CSS 的补充（DomElementManager 维护多候选）；
- 避免在业务层直接写死 CSS/XPath；统一通过策略栈与别名管理；
- 观察 `locate_*` 指标，识别高失败策略并优化别名或容器范围。

## 遥测重排（SelectorTelemetry）

为提升鲁棒性，定位候选会结合历史遥测进行动态重排（低基数）：

- 接口：`ISelectorTelemetry`（实现：`SelectorTelemetryService`）。
- 采样：`RecordAttempt(alias, selector, success, elapsedMs, attemptOrder)`。
- 重排：`OrderByTelemetry(alias, original)` 按以下优先级排序：
  - 成功率（Successes/Attempts）降序；
  - 平均耗时（SuccessElapsedMsSum/Successes）升序；
  - 首次命中尝试次序最小者优先。
- 接入场景：
  - A11y/Role 候选（别名键采用 `role:<role>`）
  - 别名 + `:has-text(...)`
  - 容器 + 别名（可叠加 `:has-text(...)`）
- 低基数纪律：`alias/selector` 使用稳定键；`:has-text(...)` 文本不纳入键名；指标标签使用白名单。
