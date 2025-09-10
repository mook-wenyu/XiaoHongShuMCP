#!/usr/bin/env bash
set -euo pipefail

# 端到端演示脚本（中文）：入口页保障 + 详情页匹配
# 用法：
#   scripts/e2e_entry_match_demo.sh <场景> <关键词JSON>
#   场景：wrong-detail（错误详情页）| profile（个人页）| home（首页）
#   关键词JSON：JSON 数组，例如 '["iPhone 15", "苹果"]'

SCENARIO=${1:-home}
KEYWORDS_JSON=${2:-'["测试","演示"]'}

ROOT_DIR=$(cd "$(dirname "$0")/.." && pwd)
PROJECT="$ROOT_DIR/XiaoHongShuMCP"

echo "== XiaoHongShu MCP 端到端演示 =="
echo "场景: $SCENARIO"
echo "关键词: $KEYWORDS_JSON"

case "$SCENARIO" in
  wrong-detail)
    echo "请手动执行："
    echo "  1) 确认已连接受控浏览器（Playwright）。"
    echo "  2) 打开任意与关键词不匹配的笔记详情页。"
    read -rp "准备就绪后回车继续..."
    ;;
  profile)
    echo "请手动执行："
    echo "  1) 跳转到任意用户的个人主页。"
    read -rp "准备就绪后回车继续..."
    ;;
  home)
    echo "可选：确保浏览器位于 https://www.xiaohongshu.com/ 首页。"
    read -rp "回车继续..."
    ;;
  *)
    echo "Unknown scenario: $SCENARIO" >&2
    exit 1
    ;;
esac

# 通用：设置 MCP 统一等待超时（默认 10 分钟）与日志级别，便于观察
export XHS__McpSettings__WaitTimeoutMs=600000
export XHS__Serilog__MinimumLevel=Information

run_demo() {
  local label="$1"; shift
  echo
  echo "--- $label ---"
  echo "匹配参数：threshold=$XHS__DetailMatchConfig__WeightedThreshold, fuzzy=$XHS__DetailMatchConfig__UseFuzzy, maxDist=$XHS__DetailMatchConfig__MaxDistanceCap, pinyin=$XHS__DetailMatchConfig__UsePinyin"
  dotnet run --project "$PROJECT" -- callTool LikeNote --json "{\"keywords\": $KEYWORDS_JSON}" || true
  dotnet run --project "$PROJECT" -- callTool FavoriteNote --json "{\"keywords\": $KEYWORDS_JSON}" || true
  dotnet run --project "$PROJECT" -- callTool PostComment --json "{\"keywords\": $KEYWORDS_JSON, \"content\": \"E2E自动化演示评论\"}" || true
}

# 1) 严格：阈值高，不启用模糊/拼音
export XHS__DetailMatchConfig__WeightedThreshold=0.80
export XHS__DetailMatchConfig__UseFuzzy=false
export XHS__DetailMatchConfig__MaxDistanceCap=0
export XHS__DetailMatchConfig__UsePinyin=false
run_demo "第1轮：严格（无模糊/拼音）"

# 2) 放宽：降低阈值，启用模糊（上限=2）
export XHS__DetailMatchConfig__WeightedThreshold=0.40
export XHS__DetailMatchConfig__UseFuzzy=true
export XHS__DetailMatchConfig__MaxDistanceCap=2
export XHS__DetailMatchConfig__UsePinyin=false
run_demo "第2轮：启用模糊（cap=2）"

# 3) 中等阈值：启用拼音首字母
export XHS__DetailMatchConfig__WeightedThreshold=0.50
export XHS__DetailMatchConfig__UseFuzzy=false
export XHS__DetailMatchConfig__MaxDistanceCap=0
export XHS__DetailMatchConfig__UsePinyin=true
run_demo "第3轮：启用拼音首字母"

echo
echo "== 演示完成。请上方查看匹配与入口页保障行为的输出结果。 =="
