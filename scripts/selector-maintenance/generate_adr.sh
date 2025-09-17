#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT_DIR/.."

PLAN="${PLAN:-docs/selector-plans/plan-$(date -u +%Y%m%d).json}"
THRESHOLD="${THRESHOLD:-0.5}"
MIN_ATTEMPTS="${MIN_ATTEMPTS:-10}"

if [[ ! -f "$PLAN" ]]; then
  echo "Plan file not found: $PLAN" >&2
  exit 1
fi

dotnet run --project XiaoHongShuMCP -c Release -- selector-adr --plan "$PLAN" --threshold "$THRESHOLD" --minAttempts "$MIN_ATTEMPTS"
echo "ADR generated for plan: $PLAN"

