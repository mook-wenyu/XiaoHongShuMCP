# OTel Traces（本地 Console 导出）启用说明（已收敛）

本项目在 Resumable 三条链路（Search/Homefeed/Interact）中接入了最小化 Span：

- ensure_context / input / locate / bind / click / scroll / await_api / aggregate / verify
- Span 来源：`ActivitySource("XHS.Traces")`
- 标签：`endpoint=SearchNotes|Homefeed|Interact`（低基数）

## 启用（本地 Console）

默认关闭（避免无意义开销）。开启方法：

```bash
export XHS__Traces__Enabled=true
# 导出到控制台
export XHS__Traces__Exporter=console
```

## 最佳实践

- 与 Metrics 联动：将 `endpoint/stage` 作为最小必要标签，避免高基数；在 Traces 中补充更详细的属性仅在 Debug 环境打开。
- 关心瓶颈：优先观察 `await_api` 与 `locate/click/scroll` 的耗时，甄别前端渲染热点。
