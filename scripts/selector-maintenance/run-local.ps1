<#
  中文 PowerShell 脚本：Windows 本地离线“选择器治理”一键执行。
  用法（在仓库根）：
    pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/selector-maintenance/run-local.ps1 -Threshold 0.5 -MinAttempts 10 -Mode reorder -Sensitive:$false
#>
param(
  [double]$Threshold = 0.5,
  [int]$MinAttempts = 10,
  [ValidateSet('reorder','prune')][string]$Mode = 'reorder',
  [switch]$Sensitive
)

$ErrorActionPreference = 'Stop'
Set-Location (Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path))

Write-Host "[selector] build release..."
dotnet build HushOps.sln -c Release --nologo | Out-Null

function Run-Once([double]$th, [int]$min, [string]$mode) {
  Write-Host "[selector] export_plan threshold=$th min=$min"
  $env:THRESHOLD = $th.ToString(); $env:MIN_ATTEMPTS = $min.ToString()
  bash scripts/selector-maintenance/export_plan.sh | Out-Null
  $plan = "docs/selector-plans/plan-$(Get-Date -AsUTC -Format yyyyMMdd).json"
  Write-Host "[selector] generate_adr plan=$plan"
  $env:PLAN = $plan; $env:THRESHOLD = $th.ToString(); $env:MIN_ATTEMPTS = $min.ToString()
  bash scripts/selector-maintenance/generate_adr.sh | Out-Null
  Write-Host "[selector] generate_patch plan=$plan mode=$mode"
  $env:PLAN = $plan; $env:MODE = $mode
  bash scripts/selector-maintenance/generate_patch.sh | Out-Null
}

Run-Once -th $Threshold -min $MinAttempts -mode $Mode
if ($Sensitive) { Run-Once -th 0.7 -min 5 -mode $Mode }

Write-Host "[selector] 完成：工件位于 docs/selector-plans 与 docs/adr-0013-*.md"
