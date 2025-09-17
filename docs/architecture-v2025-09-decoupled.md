# 智能自动化运营系统（历史归档：解耦版）— 架构与实现方案

> 重要说明：本文件描述的“CDP/外部驱动可插拔、远端传输与监控栈扩展”等能力，现阶段已从项目基线中移除。
> 当前项目聚焦“本地-only、stdio-only、单驱动（Playwright）”，相关路线仅作为历史记录保留，不作为实现目标或承诺。

更新时间：2025-09-12

## 1. 项目目标（Goals）
- 驱动解耦（历史）：浏览器自动化能力对具体实现（Playwright/CDP/外部反检测浏览器）完全可替换。
- 反检测增强：建立“策略中心 + 指标度量 + 审计”闭环，持续进化而非一次性脚本。
- 拟人化交互：自然、可配置、可回放的人类化行为模型，支持 A/B 与离线回放。
- 可审计与可治理：策略、指纹、会话与操作全链路日志化；开关受策略面板与配置治理。
- 多平台扩展：XHS 只是一个 Platform 适配器；保留抖音/微博等平台的平滑接入路径。

## 2. 已实现能力（基于当前仓库）
- 多端口 API 监控与响应解析：`UniversalApiMonitor`、各 `ResponseProcessor`。
- 拟人化交互雏形：`HumanizedInteractionService`、点击策略/文本输入策略/DOM 预检等。
- 浏览器资源治理：`PlaywrightBrowserManager`、`BrowserContextPool`、`MultiContextBrowserPool`。
- 等价抽象存在但含耦合：`Interfaces.cs` 中公开 `Microsoft.Playwright` 类型（解耦阻碍）。
- MCP 工具封装：`Tools/XiaoHongShuTools.cs`。
- 统一配置：`XhsSettings`（根节 `XHS`，支持命名空间级日志覆盖与重试、速率限制等）。

## 3. 关键缺陷与风险（现状）
- 接口层对 Playwright 强耦合，导致：
  - 难以引入 CDP 直连或外部反检测浏览器；
  - 单测与桩替换成本高；
  - 领域模型与驱动语义混杂（例如直接暴露 `IPage`、`Locator`）。
- 反检测策略分散，缺少统一“策略中心”和可度量体系；TLS/JA3、网络画像、字体/媒体/硬件能力等维度无统一治理。
- 人类化交互模型初级：缺少轨迹/节律/视野/误触等组合策略与可回放机制。
- 配置耦合 XHS：难以直观表达“平台无关策略”与“平台特定策略”的边界。

## 4. 规避方案（破坏性变更，不向后兼容）
1) 抽象层重建（Automation Abstractions）：
   - 定义：`IAutomationRuntime`、`IBrowserDriver`、`IBrowserSession`、`IPageViewport`、`IElementHandle`、`IInput`、`INavigation`、`INetwork` 等。
   - 能力面：导航、选择器、脚本执行、截图、输入/滚动、网络拦截与代理、CDP 通道、下载/上传、存储管理。
   - 事件：`Console`, `Dialog`, `Request/Response`, `DomMutation`, `Visibility`, `Vitals`。
   - 不暴露第三方类型；统一错误模型与可重试分类。

2) 适配器层（Drivers）：
   - `Adapters/Playwright`：封装现有 Playwright 细节，映射到抽象接口。
   - `Adapters/ChromeCdp`：直连已启动的 Chrome（`--remote-debugging-port`），适配低级 CDP 能力。
   - `Adapters/ExternalStealth`：对接外部反检测浏览器/云端会话（仅留接口与契约）。

3) 反检测策略中心（AntiDetection Center）：
   - 指纹生成与注入：UA/UA-CH、时区/Locale/语言、屏幕/硬件并发/内存、字体/音视频编解码、WebGL/Canvas、设备传感器等。
   - JS/DOM 指示器治理：`navigator.webdriver`、`permissions`、`plugins`、`mediaDevices`、`chrome` 对象、堆栈特征等。
   - 网络与 TLS：代理池（住宅/数据中心/本地）、IP 粘性、会话亲和；TLS/JA3 透传（通过支持该能力的代理提供商实现）。
   - 会话画像（Persona Profiles）：职业、设备、语言、使用习惯；与历史行为/收藏/搜索语义一致。
   - 策略白名单与黑名单：按平台/路径/操作维度启用/禁用策略，审计可回放。

4) 拟人化交互引擎（Humanization Engine）：
   - 轨迹库：贝塞尔曲线混合抖动、加速度/减速度分布、边界吸附与修正。
   - 节律模型：基于任务上下文的等待分布（Log-Normal/Weibull 混合）、纠错打字、视线游移（滚动-停顿-回看）。
   - 错误注入：偶发失手与回撤、短暂遮挡导致重试、微小偏移点击二次修正。
   - 观测：曝光时间、点击热力、关键路径可视化；支持“离线回放 + 快照比对”。

5) 配置重构：
   - 根节从 `XHS` 改为 `Ops`；结构：
     - `Ops:Automation:*`（驱动与资源治理）
     - `Ops:AntiDetection:*`（指纹/代理/策略开关）
     - `Ops:Humanize:*`（轨迹/节律参数）
     - `Ops:Platforms:XHS:*`（平台特定）
     - `Ops:Logging:*`（命名空间覆盖沿用）

6) 日志与审计：
   - 统一结构化日志（Serilog），关键事件专用 Sink（如 JSONL + Size/Rolling）。
   - 审计上下文（CorrelationId、PersonaId、SessionId、ProxyId、FingerprintId）。
   - MCP 工具入/出参数快照（脱敏）。

## 5. 目录结构（建议）
```
XiaoHongShuMCP/
  Core/
    Automation/            # 抽象接口与错误模型
      Abstractions/
      Errors/
      Contracts/
    AntiDetection/
      Fingerprints/
      Strategies/
      Network/
    Humanize/
      Trajectories/
      Rhythms/
      Replay/
  Adapters/
    Playwright/
    ChromeCdp/
    ExternalStealth/
  Platforms/
    XHS/                   # 仅保留平台流程与选择器，不含驱动细节
  Tools/
  Tests/
  docs/
```

## 6. 实现路线（Milestones）
M1（重构骨架，1-2 周）
- 新建抽象接口与错误模型；
- 建立 Playwright 适配器最小闭环（启动、导航、定位、点击、输入、滚动、等待、请求拦截）；
- 将 `Interfaces.cs` 中暴露的 Playwright 类型全部替换为抽象；删除耦合 using；
- 修复编译并通过最小回归用例（首页/搜索/详情）。

M2（反检测策略中心，1-2 周）
- 接入 Persona/Profile 与 Fingerprint 管线（生成→注入→验证→度量）；
- 增加代理池与粘性会话；
- 实现 JS 指示器治理（`webdriver`/语言/时区/媒体能力等）；
- 策略面板：YAML/JSON + 热加载 + 审计日志。

M3（拟人化交互引擎，1-2 周）
- 轨迹与节律组合策略；
- 行为回放（录像/事件流/快照）；
- 人类化策略与反检测策略的联动（例如首屏停留/滚动层级）。

M4（CDP/外部适配器可插拔，2 周）
- `ChromeCdpAdapter` 雏形（连接已开启 `--remote-debugging-port` 的 Chrome）；
- `ExternalStealthAdapter` 契约与最小演示；
- 平台扩展样例（保留 XHS，添加一处“伪平台”以验证抽象）。

## 7. 测试与度量（Testing & Metrics）
- 单测：
  - 抽象层：契约测试 + Property-based 测试（人类化时间分布/轨迹约束）。
  - 适配器：基于 TestServer/CDP 录制的集成测试。
  - 反检测：快照/白名单验证（指纹 JSON 与注入前后对比）。
- 覆盖率：新增代码≥80%，核心路径≥90%。
- 指标：
  - 技术：异常率、重试率、首开时间、请求失败分类、指纹一致性、代理有效率。
  - 业务：操作成功率、会话存续、封禁告警、采集吞吐。

## 8. 迁移与回滚
- 一次性重构，标记旧接口与配置为“已删除”（不向后兼容）。
- 发布说明提供“环境变量映射表”和“失败快速回滚脚本/步骤”。

## 9. 风险与应对
- 抽象过度：以 MVP 能力面切入，按平台用例反推补齐；评审门槛：90% 的现有用例无需回退到驱动细节。
- 反检测有效性不稳：以审计与指标衡量，按平台与流量拨测逐步放量。
- 吞吐受拟人化影响：引入“速率治理 + 并发调度 + 旁路直连”组合，按任务类型选择策略强度。

## 10. 审计与合规
- 严禁记录敏感凭据；日志统一脱敏；
- 控制台/文件日志与“策略快照”绑定；
- 变更与策略通过 ADR 管理并可溯源。
