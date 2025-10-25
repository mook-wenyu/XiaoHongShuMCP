# 故障排查指南（Troubleshooting)

最近更新：2025-10-25

## 常见错误与处置

### MODAL_REQUIRED（需要笔记详情模态）
- 现象：`noteActions` 返回 `{ ok:false, code: 'MODAL_REQUIRED' }`
- 原因：未打开详情模态或模态不可见。
- 处理：
  - 调用 `xhs_select_note` 或 `findAndOpenNoteByKeywords` 打开任一笔记；
  - 检查页面是否被登录/风控页拦截；
  - 截图核对（artifacts 内最后截图）。

### LIKE_FAILED / COLLECT_FAILED / COMMENT_FAILED
- 现象：接口监听未返回 code=0/success=true。
- 处理：
  - 确认 `netwatch` 是否监听到对应接口（抓包/日志）；
  - 检查点击是否命中底部 engage-bar 主按钮（24px 图标）；
  - 若 UI 已更新但回执失败，可临时放宽（见技术建议中的降级判定）。

### FEED_TIMEOUT / SEARCH 相关失败
- 现象：点击卡片后未在期望时间内收到 feed/search 回执。
- 处理：
  - 提高 `XHS_FEED_WAIT_API_MS` 或 `XHS_SEARCH_WAIT_API_MS`；
  - 网络不稳定时可关闭“必须验证 feed”的强约束，先以模态可见为成功（调试阶段）。

### CLICK_FAILED / HOVER_FAILED（工具层）
- 现象：`action.click/hover` 失败。
- 处理：
  - 记录 `screenshotPath` 并查看 `lastLocator`；
  - 适当增大 `verifyTimeoutMs`，或检查 selectors 提示（role/text/selector）。

### 关注失败（FOLLOW_FAILED）
- 现象：按钮可见但回执失败。
- 处理：
  - 可能是态/弹窗/AB 变体；可在 `noteActions` 增加“文案变更为已关注则判成功”的降级判定；
  - 观察 `netwatch.waitFollow` 回执是否成功，若频繁失败，考虑加长超时或做重试。

## Windows 终端参数问题
- 典型报错：`Array index expression is missing or not valid.`（PowerShell 把 `[]` 识别为索引）
- 解决：
  - 使用单引号包裹：`--comment='[微笑R]'`
  - 或使用 `--payload=@file` 方式传入 JSON。

## 选择器健康度与熔断
- 健康度写入：`artifacts/selector-health.ndjson`
- 报表脚本：`npx tsx scripts/selector-health-report.ts`
- 断路器日志：`services/policy.ts` 会输出“熔断打开中/触发熔断”等告警。

## 收集诊断材料（建议）
- 最后一次 `result.json` 与 `final.png`
- `selector-health.ndjson` 近 1–2 分钟样本
- 环境变量（去敏）与 `XHS_CONF.*` 重要参数值
