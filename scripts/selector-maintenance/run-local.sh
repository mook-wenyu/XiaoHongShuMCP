#!/usr/bin/env bash
# 中文脚本：本地离线“选择器治理”一键执行（计划→ADR→补丁），默认不修改源码。
# 用法：
#   bash scripts/selector-maintenance/run-local.sh [--threshold 0.5] [--min 10] [--mode reorder|prune] [--source <file>] [--sensitive]
#   环境变量亦可覆盖：THRESHOLD / MIN_ATTEMPTS / MODE / SOURCE
# 说明：
#   - 始终生成 docs/selector-plans/plan-YYYYMMDD.json 与 ADR/补丁工件；
#   - 默认不改源；如需强制“物理落地”，请使用 apply_source.sh 并明确传入 --approve；
#   - 建议结合 cron 或计划任务（见 docs/selector-maintenance-local.md）。
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"  # scripts 目录
cd "$ROOT_DIR/.."                                 # 项目根

THRESHOLD="${THRESHOLD:-0.5}"
MIN_ATTEMPTS="${MIN_ATTEMPTS:-10}"
MODE="${MODE:-reorder}"          # reorder|prune
SOURCE="${SOURCE:-XiaoHongShuMCP/Services/DomElementManager.cs}"
SENSITIVE=false

while [[ $# -gt 0 ]]; do
  case "$1" in
    --threshold) THRESHOLD="$2"; shift 2 ;;
    --min|--minAttempts) MIN_ATTEMPTS="$2"; shift 2 ;;
    --mode) MODE="$2"; shift 2 ;;
    --source) SOURCE="$2"; shift 2 ;;
    --sensitive) SENSITIVE=true; shift ;;
    *) echo "未知参数: $1" >&2; exit 2 ;;
  esac
done

echo "[selector] build release..."
dotnet build HushOps.sln -c Release --nologo >/dev/null

run_once() {
  local th="$1"; local min="$2"; local mode="$3"; local src="$4";
  echo "[selector] export_plan threshold=$th min=$min"
  THRESHOLD="$th" MIN_ATTEMPTS="$min" bash scripts/selector-maintenance/export_plan.sh >/dev/null
  local PLAN="docs/selector-plans/plan-$(date -u +%Y%m%d).json"
  echo "[selector] generate_adr plan=$PLAN"
  PLAN="$PLAN" THRESHOLD="$th" MIN_ATTEMPTS="$min" bash scripts/selector-maintenance/generate_adr.sh >/dev/null
  echo "[selector] generate_patch plan=$PLAN mode=$mode"
  PLAN="$PLAN" MODE="$mode" SOURCE="$src" bash scripts/selector-maintenance/generate_patch.sh >/dev/null
}

# 常规阈值
run_once "$THRESHOLD" "$MIN_ATTEMPTS" "$MODE" "$SOURCE"

# 可选：更敏感阈值（用于加压检测，不会改源）
if [[ "$SENSITIVE" == true ]]; then
  echo "[selector] run sensitive pass (threshold=0.7 min=5)"
  run_once 0.7 5 "$MODE" "$SOURCE"
fi

echo "[selector] 完成。工件位于 docs/selector-plans/ 与 docs/adr-0013-*.md"
