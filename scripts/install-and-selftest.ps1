Param(
  [switch]$Release
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Write-Host "1) 还原与构建解决方案..." -ForegroundColor Cyan
dotnet build -c ($Release ? 'Release' : 'Debug')

$tfm = 'net8.0'
$binDir = Join-Path -Path (Resolve-Path .).Path -ChildPath "XiaoHongShuMCP/bin/" + ($Release ? 'Release' : 'Debug') + "/$tfm"
$ps1 = Join-Path $binDir 'playwright.ps1'
if (-not (Test-Path $ps1)) {
  Write-Host "未找到 $ps1 ，尝试运行 dotnet tool 安装 Playwright 依赖..." -ForegroundColor Yellow
}

Write-Host "2) 安装 Playwright 浏览器..." -ForegroundColor Cyan
pwsh -NoProfile -ExecutionPolicy Bypass -File $ps1 install

Write-Host "3) 自测：列出工具清单（tools-list）..." -ForegroundColor Cyan
$env:XHS__Serilog__MinimumLevel='Error'
$env:XHS__BrowserSettings__Headless='true'
$env:XHS__BrowserSettings__UserDataDir='UserDataDir'
dotnet run --project XiaoHongShuMCP/XiaoHongShuMCP.csproj -- tools-list

Write-Host "完成。" -ForegroundColor Green
