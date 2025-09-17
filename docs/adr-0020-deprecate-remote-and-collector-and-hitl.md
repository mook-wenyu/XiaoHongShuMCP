# ADR-0020：删除远端传输/Collector/插件与人在环（HITL），收敛为本地-only 基线

日期：2025-09-15  | 状态：通过

## 背景

在多轮演进后，项目面向“本地 LLM 智能体 + MCP/stdio”的核心场景。为降低维护面与风险，决定删除：
1) 远端传输（HTTP/SSE/Streamable HTTP）相关实现与文档；
2) Puppeteer/CDP 可插驱动相关实现与脚本；
3) OTel Collector/Prometheus/Grafana 相关脚本与对外文档；
4) 人在环（HITL）确认机制与协议；
5) Evaluate 违规报警脚本与 CI 守卫联动。

## 决策

- 基线收敛为“本地-only / stdio-only / 单驱动（Playwright）”，指标仅 Console 导出；
- 禁注入（仅 AntiDetectionPipeline 白名单、默认关闭），只读 Evaluate 白名单持续收紧；
- 入口级韧性（TokenBucket + Polly 断路 + 超时 + 幂等）与策略治理工具链（health→patch→merge→adr）保留；
- 删除上述五项能力的脚本与对外文档，相关 ADR/架构文档标注“历史归档”。

## 影响

- 优点：删除自研与外围维护面，聚焦核心；本地调试体验更轻；策略治理与审计链路保留。
- 风险：无 HITL 与无 Evaluate 报警/CI 守卫后，误操作与回归发现滞后；通过“更保守默认速率、写前稳定性预检、API 成功监听（自动）、干跑/黑名单开关（可选）与结构硬约束（公共 API 不暴露写入 Evaluate）”来补偿。

## 迁移

- 文档：README、metrics 文档改为本地-only；Prom/Collector/远端传输说明删除；涉及 CDP/远端的文档加“历史归档”标注。
- 代码与脚本：删除 scan-eval 与 metrics 阈值脚本；无 HTTP/Streamable 实现；无 Puppeteer/CDP 实现。

## 回滚策略

- 若后续需要远端或 Collector：以新 ADR 提议与评审，按“标准化优先”与审计可追溯原则重启；不保留兼容层。

