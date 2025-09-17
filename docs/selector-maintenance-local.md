# 本地“选择器治理”离线流程（无 GitHub Action）

> 目标：按周自动产出“弱选择器重排/清理”计划与工件（JSON/ADR/补丁），默认不改源；审阅后再决定是否物理落地。

## 一、一次性准备

- .NET 8 与 bash/PowerShell 可用；首次运行 Playwright 需安装浏览器：
  ```bash
  dotnet build -c Release
  # 若首次在本机使用 Playwright：
  # pwsh ./XiaoHongShuMCP/bin/Release/net8.0/playwright.ps1 install
  ```

## 二、手工执行（单次）

```bash
# 常规阈值（默认 0.5/10，模式 reorder）
bash scripts/selector-maintenance/run-local.sh --threshold 0.5 --min 10 --mode reorder

# 加压检测（可选）
bash scripts/selector-maintenance/run-local.sh --sensitive

# 产物：
# - docs/selector-plans/plan-YYYYMMDD.json
# - docs/selector-plans/diff-YYYYMMDD.patch（若有变更）
# - docs/adr-0013-selector-pruning-and-reorder-YYYYMMDD.md
```

Windows（PowerShell）：
```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/selector-maintenance/run-local.ps1 -Threshold 0.5 -MinAttempts 10 -Mode reorder -Sensitive:$false
```

## 三、自动化调度（无 GitHub Action）

### Linux：cron

```bash
bash scripts/selector-maintenance/setup-cron.sh --threshold 0.5 --min 10 --mode reorder --sensitive
# 默认每周一 03:00 本地时间执行；日志输出 logs/selector-maintenance.log
crontab -l
```

### Windows：计划任务

以管理员 PowerShell 运行：
```powershell
$Action = New-ScheduledTaskAction -Execute "pwsh" -Argument "-NoProfile -ExecutionPolicy Bypass -File `"$(Get-Item .\scripts\selector-maintenance\run-local.ps1).FullName`" -Threshold 0.5 -MinAttempts 10 -Mode reorder -Sensitive:$true"
$Trigger = New-ScheduledTaskTrigger -Weekly -DaysOfWeek Monday -At 3am
Register-ScheduledTask -TaskName "XHS-SelectorMaintenance" -Action $Action -Trigger $Trigger -Description "XHS selector maintenance weekly"
```

## 四、审阅与“物理落地”（谨慎）

1) 审阅工件：
- 计划：`docs/selector-plans/plan-YYYYMMDD.json`
- 补丁：`docs/selector-plans/diff-YYYYMMDD.patch`
- 文档：`docs/adr-0013-*.md`

2) 确认后，如需直接改源（建议在新分支）：
```bash
PLAN=docs/selector-plans/plan-YYYYMMDD.json \
bash scripts/selector-maintenance/apply_source.sh --mode reorder --source XiaoHongShuMCP/Services/DomElementManager.cs --approve I_KNOW

dotnet test Tests -c Release
```

## 五、FAQ

- 为什么默认不修改源码？
  - 保守、可审计、可回滚；避免将偶发抖动变更持久化。
- 计划为空（items=0）意味着什么？
  - 当前弱选择器不足以触发阈值；说明库较稳定。可考虑在预发提高阈值/降低样本进行“加压检测”。
