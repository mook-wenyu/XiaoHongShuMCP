#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT_DIR/.."

PLAN="${PLAN:-docs/selector-plans/plan-$(date -u +%Y%m%d).json}"
MODE="${MODE:-reorder}" # or prune
SOURCE="${SOURCE:-XiaoHongShuMCP/Services/DomElementManager.cs}"

if [[ ! -f "$PLAN" ]]; then
  echo "Plan file not found: $PLAN" >&2
  exit 1
fi

dotnet run --project XiaoHongShuMCP/XiaoHongShuMCP.csproj -c Release -- selector-plan patch --plan "$PLAN" --mode "$MODE" --source "$SOURCE"
echo "Patch generated for plan: $PLAN (mode=$MODE)"
