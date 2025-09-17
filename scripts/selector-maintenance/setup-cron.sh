#!/usr/bin/env bash
# 中文脚本：安装/更新本地 cron 计划任务（每周一 03:00 本地时间）执行 run-local.sh。
# 用法： bash scripts/selector-maintenance/setup-cron.sh [--threshold 0.5] [--min 10] [--mode reorder] [--sensitive]
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT_DIR/.."

THRESHOLD="${THRESHOLD:-0.5}"
MIN_ATTEMPTS="${MIN_ATTEMPTS:-10}"
MODE="${MODE:-reorder}"
SENSITIVE=false

while [[ $# -gt 0 ]]; do
  case "$1" in
    --threshold) THRESHOLD="$2"; shift 2 ;;
    --min|--minAttempts) MIN_ATTEMPTS="$2"; shift 2 ;;
    --mode) MODE="$2"; shift 2 ;;
    --sensitive) SENSITIVE=true; shift ;;
    *) echo "未知参数: $1" >&2; exit 2 ;;
  esac
done

BIN="bash $(pwd)/scripts/selector-maintenance/run-local.sh --threshold $THRESHOLD --min $MIN_ATTEMPTS --mode $MODE"
if [[ "$SENSITIVE" == true ]]; then BIN+=" --sensitive"; fi

# 每周一 03:00 执行。日志输出到 logs/selector-maintenance.log
mkdir -p logs
CRON_LINE="0 3 * * 1 cd $(pwd) && $BIN >> $(pwd)/logs/selector-maintenance.log 2>&1"

TMP=$(mktemp)
crontab -l 2>/dev/null | grep -v 'selector-maintenance/run-local.sh' > "$TMP" || true
echo "$CRON_LINE" >> "$TMP"
crontab "$TMP"
rm -f "$TMP"

echo "已安装/更新 crontab：$CRON_LINE"

