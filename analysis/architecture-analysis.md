# Architecture Analysis

最近更新：2025-10-25

## 总览
本项目采用“容器化服务 + 分层域模型”的结构：
- Core：`ServiceContainer` 构建服务实例（RoxyClient、PlaywrightConnector、ConnectionManager、Policy、Logger）。
- Services：封装浏览器上下文、页面管理、连接生命周期、限流/熔断策略与制品（artifacts）。
- Domain(XHS)：站点语义操作（导航、搜索、模态动作、网络监听）。
- Humanization：拟人化交互（路径曲线、概率分布、分段滚动、微抖动等）。
- Selectors：弹性选择器解析与健康度上报，支持并发、重试与熔断。
- MCP Tools：将原子/语义操作以 MCP 工具形式对外暴露（action.*、xhs.*）。

## 关键数据流
1) CLI/MCP → ServiceContainer → ConnectionManager → PlaywrightConnector → 上下文/页面
2) 选择器解析：`resolveLocatorResilient(target)` → policy.use（熔断/重试）→ 成功/失败上报（health）
3) 拟人化输入：`humanization/actions.{mouse,scroll,keyboard}` → plans（生成路径/节律）→ 执行层驱动 Playwright
4) XHS 模态动作：
   - 定位：以 `.note-detail-mask` 外壳 + `engage-bar` 容器严格作用域；
   - 交互：拟人化点击（必要时 hover 激活/软等待/微偏移兜底）；
   - 回执：`netwatch.ts` 监听 like/collect/comment/follow 接口，code=0 或 success=true 视为成功。

## 主要组件关系
- `src/mcp/tools/actions.ts` 与 `src/mcp/tools/xhsShortcuts.ts` 依赖 Services+Selectors+Humanization 暴露工具
- `src/domain/xhs/noteActions.ts` 依赖 Humanization+Netwatch 实现模态内原子动作
- `src/domain/xhs/navigation.ts` 负责页内滚动与命中后点击“封面/标题”（避免其它干扰点击）
- `src/selectors/resilient.ts` 统一入口，整合重试/熔断/健康度；`selectors/report.ts` 汇总报表

## 关键约束&约定
- Windows 终端：PowerShell 参数转义与 JSON 传参需谨慎（示例脚本已兼容）
- MCP 输出：工具层错误带截图路径（可由 `ACTION_SNAP_ON_ERROR` 控制）
- 默认策略：
  - 鼠标微抖动启用；滚动停顿+easing 启用
  - 模态动作仅在 engage-bar 容器范围，避免误触评论区元素

## 风险与待改进（抽样）
- 关注流程：接口回执偶发失败（可能为态校验/AB 弹窗）；可增设“文本变更为已关注则判成功”的降级路径
- 选择器健康度：长时运行的 NDJSON 写入可增加批量 flush/退避策略
- 文档：MCP 工具用法与 Windows 引号/文件传参示例可补充

## 参考文件
- 入口：`src/cli.ts`, `src/mcp/server.ts`
- 域：`src/domain/xhs/navigation.ts`, `src/domain/xhs/noteActions.ts`, `src/domain/xhs/netwatch.ts`
- 服务：`src/services/*`
- 选择器：`src/selectors/*`
- 人性化：`src/humanization/*`
