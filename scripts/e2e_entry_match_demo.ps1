param(
  [ValidateSet('wrong-detail','profile','home')]
  [string]$Scenario = 'home',
  [string]$KeywordsJson = '["测试","演示"]'
)

$Root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$Project = Join-Path $Root 'XiaoHongShuMCP'

Write-Host "== XiaoHongShu MCP 端到端演示 ==" -ForegroundColor Cyan
Write-Host "场景: $Scenario"
Write-Host "关键词: $KeywordsJson"

switch ($Scenario) {
  'wrong-detail' {
    Write-Host "请手动执行：" -ForegroundColor Yellow
    Write-Host "  1) 确认已连接受控浏览器（Playwright）。"
    Write-Host "  2) 打开任意与关键词不匹配的笔记详情页。"
    Read-Host "准备就绪后回车继续"
  }
  'profile' {
    Write-Host "请手动执行：" -ForegroundColor Yellow
    Write-Host "  跳转到任意用户的个人主页。"
    Read-Host "准备就绪后回车继续"
  }
  default {
    Write-Host "可选：确保浏览器位于 https://www.xiaohongshu.com/ 首页。"
    Read-Host "回车继续"
  }
}

# 通用：设置 MCP 统一等待超时（默认 10 分钟）
$env:XHS__McpSettings__WaitTimeoutMs = '600000'
$env:XHS__Serilog__MinimumLevel = 'Information'

function Run-Demo([string]$label) {
  Write-Host "`n--- $label ---" -ForegroundColor Green
  Write-Host "参数：`n  threshold=$env:XHS__DetailMatchConfig__WeightedThreshold; fuzzy=$env:XHS__DetailMatchConfig__UseFuzzy; maxDist=$env:XHS__DetailMatchConfig__MaxDistanceCap; pinyin=$env:XHS__DetailMatchConfig__UsePinyin"
  dotnet run --project $Project -- callTool LikeNote --json "{`"keywords`": $KeywordsJson}" | Write-Output
  dotnet run --project $Project -- callTool FavoriteNote --json "{`"keywords`": $KeywordsJson}" | Write-Output
  dotnet run --project $Project -- callTool PostComment --json "{`"keywords`": $KeywordsJson, `"content`": `"E2E自动化演示评论`"}" | Write-Output
}

# 1) 严格：阈值高，禁用模糊/拼音
$env:XHS__DetailMatchConfig__WeightedThreshold = '0.80'
$env:XHS__DetailMatchConfig__UseFuzzy = 'false'
$env:XHS__DetailMatchConfig__MaxDistanceCap = '0'
$env:XHS__DetailMatchConfig__UsePinyin = 'false'
Run-Demo '第1轮：严格（无模糊/拼音）'

# 2) 模糊：启用模糊匹配（cap=2）
$env:XHS__DetailMatchConfig__WeightedThreshold = '0.40'
$env:XHS__DetailMatchConfig__UseFuzzy = 'true'
$env:XHS__DetailMatchConfig__MaxDistanceCap = '2'
$env:XHS__DetailMatchConfig__UsePinyin = 'false'
Run-Demo '第2轮：启用模糊（cap=2）'

# 3) 拼音：启用拼音首字母
$env:XHS__DetailMatchConfig__WeightedThreshold = '0.50'
$env:XHS__DetailMatchConfig__UseFuzzy = 'false'
$env:XHS__DetailMatchConfig__MaxDistanceCap = '0'
$env:XHS__DetailMatchConfig__UsePinyin = 'true'
Run-Demo '第3轮：启用拼音首字母'

Write-Host "`n== 演示完成。请上方查看匹配与入口页保障行为的输出结果。 ==" -ForegroundColor Cyan
