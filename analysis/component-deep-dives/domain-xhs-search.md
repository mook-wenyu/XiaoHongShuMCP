# domain/xhs/search.ts — 深度分析

## 角色与边界
- 负责：搜索流程（关闭模态→定位输入与提交→输入关键词→提交→等待 URL/API 侧证）。
- 工具依赖：`resolveLocatorResilient`（选择器弹性解析）、`XhsSelectors`（站点选择器提示）、`netwatch.waitSearchNotes`（接口侧证）。

## 定位策略
- 输入框候选：`XhsSelectors.searchInput()` → role=textbox → `#search-input.search-input` → `.input-box input.search-input`。
- 提交按钮候选：`XhsSelectors.searchSubmit()` → role=button → `.input-box .input-button` → `.input-box .search-icon`。
- 健康度记录：对 input/submit 各自只记录一次（避免噪音）。

## 提交流程
- 清空：`clearInputHuman` → `typeHuman`（220 wpm）输入关键词。
- 并行等待：URL 变化 `waitForURL(/\/search_result\?keyword=/)` 与 `waitSearchNotes`（API），任一成功即返回。
- 重试：submit 点击与回车交替尝试，最多 3 次；返回 `verified` 与 `matchedCount`（来自 API）。

## 易错点与防护
- 初次定位失败时自动回到发现页再定位；仍失败则直达首页再试。
- 测试态下缩短 `retryAttempts/verifyTimeoutMs`，快速失败以稳定单测时长。

## 改进建议
- XhsSelectors 的输入/提交候选可在文档列出具体 CSS 与 role，便于回归核对。
- `matchedCount` 字段已补齐；可在 artifacts 写入一次搜索前后截图以加速远程排障。

## 文件参考
- src/domain/xhs/search.ts:1
