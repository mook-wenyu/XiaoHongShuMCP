# selectors/card.ts — 深度分析

## 角色
- 卡片容器与可视卡信息收集：统一容器选择器，导出 `collectVisibleCards`（title/text/noteId/y 高度等）。
- 仅用于“封面/标题点击”策略，外部 JSON 选择器已移除。

## 要点
- 容器：默认 `'section.note-item, .note-item, .List-item, article, .Card'`，后续可按页面类型细分。
- 可视判断：rect.height>30、在窗口内、offsetParent!=null。
- noteId 提取：匹配 href 的 `/explore|discovery/item|search_result|question|p|zvideo/<id>`。

## 建议
- 不同页面构成下容器类名可能差异较大，可按 `PageType` 输出更精确映射。
- 若需要支持多站，抽象成站点适配器（当前仅 XHS）。

## 文件参考
- src/selectors/card.ts:1
