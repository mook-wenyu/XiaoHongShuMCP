标题（已归档）：浏览器操作与平台逻辑解耦 + 反检测体系（破坏性重构）
状态：提议中
背景：
- 现有接口层（Services/Interfaces.cs）直接暴露 `Microsoft.Playwright` 类型，导致对具体驱动强耦合，（历史）限制未来替换与扩展（如 CDP 直连、外部反检测浏览器）。
- 业务目标要求“持续增强反检测能力+自然拟人化交互”，需要独立的策略中心与度量闭环，而非散落在各服务内的点状实现。
- 配置以 `XHS` 为根，平台耦合明显，不利于多平台扩展与策略复用。

方案：
- A（推荐）：引入独立的“自动化抽象层（Automation Abstractions）”，定义 `IBrowserDriver/IBrowserContext/IPageViewport/IInput` 等领域友好的接口；
  - 提供 `PlaywrightAdapter` 作为首个实现；预留 `ChromeCdpAdapter` 与 `ExternalStealthAdapter`（连接反检测浏览器/云端会话）。
  - 新增“反检测策略中心（AntiDetection Center）”：指纹与指示器（Navigator/WebGL/时区/字体/媒体）、网络代理/住宅 IP、TLS/JA3 透传、会话画像（Persona Profiles）。
  - 新增“拟人化交互引擎（Humanization Engine）”：鼠标轨迹库（贝塞尔/抖动）、节律模型（停顿/微延迟/纠错打字）、视野与滚动决策、误触与回撤。
- B：仅以接口适配 Playwright 的现状（最小改动）。
- C：直接迁移到外部反检测浏览器（破坏性更强，短期风险高）。

决策：
- 选择 A。一次性破坏性重构，删除对 `Microsoft.Playwright` 的公共接口暴露，将其收敛到 `Adapters/Playwright`，其余服务仅依赖抽象；
- 配置根从 `XHS` 重命名为 `Ops`，平台配置放入 `Ops:Platforms:XHS:*`，与 `Ops:Automation:*`、`Ops:AntiDetection:*` 并列；
- 引入“策略面板 + 度量仪表盘”，所有反检测与交互策略通过配置与特性开关显式化并可审计；
- MCP Tools 仅调用领域服务，不直接触达具体驱动实现。

影响：
- 代码：跨服务签名变更（大量编译错误需一次性修复）；目录重构；新增 `Core.Automation`、`Core.AntiDetection`、`Core.Humanize` 命名空间；删除过时耦合层。
- 测试：现有引用 Playwright 的单测需替换为基于抽象的测试 + 适配器端的集成测试；关键路径覆盖率≥90%。
- 配置与部署：环境变量前缀从 `XHS__*` 迁移为 `Ops__*`；需提供兼容期（1 版内）临时映射器与废弃告警，最终移除（不向后兼容）。
- 文档：更新 README/项目结构/运行说明；新增反检测威胁模型与策略白名单。

回滚：
- 触发条件：引入抽象后关键业务路径（登录/首页/搜索/详情）故障率>5% 且 24 小时内无法修复；或反检测策略导致大面积封禁风险上升。
- 步骤：
 1) 使用发布产物回滚到上一个稳定 tag（pre-decoupling）；
 2) 暂时禁用新“策略中心”与“人性化引擎”特性开关；
 3) 保留新目录结构但恢复旧接口（临时分支），完成根因分析后再推进迭代。

审计点：
- 版本化策略与指纹配置；
- MCP 调用链路日志与工具输入输出快照（脱敏）；
- 关键指标：会话平均存活时长、指纹稳定性、异常率、操作成功率、封禁告警数。
