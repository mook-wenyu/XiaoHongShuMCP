# domain/xhs/netwatch.ts — 深度分析

## 角色
- 统一 API 监听：先挂监听，再触发动作；集中解析成功语义与元数据（url/status/ttfb）。

## 设计要点
- `waitApi`：通用等待器，支持 `okOnly`（仅 2xx）、超时、map 自定义解析，带 ttfb 统计。
- `safeMeta`：在 mock/变体 Response 时宽容提取 ok/url/status，避免因未实现方法抛错。
- 具体封装：
  - feed：`/api/sns/web/v1/feed|homefeed`（items、type）
  - 搜索：`/api/sns/web/v1/search/notes`（items:{id,title}）
  - 互动：like/dislike、collect/uncollect、follow/unfollow、comment/post（解析 code/success 与特定字段）

## 成功语义
- 基本：`data.code===0 || data.success===true`
- follow/unfollow：附加 `data.data.fstatus==='follows'|'none'`

## 改进建议
- 关注动作回执偶发失败：结合 UI 文案变化做降级确认（在调用处实现，netwatch 保持协议单纯）。
- 监控：可将 ttfb/状态码/路径计数输出至 artifacts 以便基准对比。

## 文件参考
- src/domain/xhs/netwatch.ts:1
