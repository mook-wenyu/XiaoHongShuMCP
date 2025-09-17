# 配置示例与环境拨盘（生产/预发）

> 本文提供生产（Production）与预发（Staging）的关键配置示例，统一从根节 `XHS` 读取；可通过环境变量（`:` 映射为 `__`）或 `.env` 文件加载。

## 评估门控（只读 Evaluate）

- 总开关：`XHS:InteractionPolicy:EnableJsReadEval = true|false`
- 路径白名单（可选）：`XHS:InteractionPolicy:EvalWhitelist:Paths = csv`
  - 建议白名单（最小必需）：`element.computedStyle,element.textProbe,element.probeVisibility,element.clickability,element.tagName,element.html.sample,page.eval.read,antidetect.snapshot`

## 运行期弱选择器重排计划

- 启用：`XHS:Selectors:ApplyOnStartup = true|false`（默认 true）
- 计划文件：`XHS:Selectors:PlanPath = docs/selector-plans/plan-YYYYMMDD.json`

## 指标聚合（InProcess）

- `XHS:Metrics:Enabled = true|false`
- `XHS:Metrics:MeterName = XHS.Metrics`
- `XHS:Metrics:AllowedLabels = endpoint,status,hint`

## 预发（Staging）示例（profiles/staging/.env）

```env
DOTNET_ENVIRONMENT=Staging
XHS:InteractionPolicy:EnableJsReadEval=true
XHS:InteractionPolicy:EvalWhitelist:Paths=element.computedStyle,element.textProbe,element.probeVisibility,element.clickability,element.tagName,element.html.sample,page.eval.read,antidetect.snapshot
XHS:Selectors:ApplyOnStartup=true
# XHS:Selectors:PlanPath=docs/selector-plans/plan-YYYYMMDD.json
# XHS:Metrics:AllowedLabels=endpoint,status,hint
```

## 生产（Production）示例（profiles/production/.env）

```env
DOTNET_ENVIRONMENT=Production
XHS:InteractionPolicy:EnableJsReadEval=false
# XHS:InteractionPolicy:EvalWhitelist:Paths=element.probeVisibility,element.clickability
XHS:Selectors:ApplyOnStartup=true
# XHS:Selectors:PlanPath=/etc/xhs/selector-plan.json
# XHS:Metrics:Enabled=false
# XHS:Metrics:AllowedLabels=endpoint,status
```

## 运行脚本

- 预发：`bash scripts/run-staging.sh`
- 生产（本地模拟）：`bash scripts/run-production.sh`

## 选择器治理工具（CLI）

- 导出计划：
  `dotnet run --project XiaoHongShuMCP -c Release -- selector-plan export --threshold 0.5 --minAttempts 10 --out docs/selector-plans`
- 运行期应用计划（内存重排）：
  `dotnet run --project XiaoHongShuMCP -c Release -- selector-plan apply --plan docs/selector-plans/plan-YYYYMMDD.json --dryRun true`
- 生成源码补丁（最小变更）：
  `dotnet run --project XiaoHongShuMCP -c Release -- selector-plan patch --plan docs/selector-plans/plan-YYYYMMDD.json --mode reorder`
- 直接应用到源文件（谨慎）：
  `dotnet run --project XiaoHongShuMCP -c Release -- selector-plan apply-source --plan docs/selector-plans/plan-YYYYMMDD.json --mode prune`
- 生成 ADR：
  `dotnet run --project XiaoHongShuMCP -c Release -- selector-adr --plan docs/selector-plans/plan-YYYYMMDD.json --threshold 0.5 --minAttempts 10`
