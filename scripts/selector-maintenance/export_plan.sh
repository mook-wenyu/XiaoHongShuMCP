#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT_DIR/.."

THRESHOLD="${THRESHOLD:-0.5}"
MIN_ATTEMPTS="${MIN_ATTEMPTS:-10}"
OUT_DIR="${OUT_DIR:-docs/selector-plans}"

dotnet build HushOps.sln -c Release --nologo 1>/dev/null
dotnet run --project XiaoHongShuMCP/XiaoHongShuMCP.csproj -c Release -- selector-plan export --threshold "$THRESHOLD" --minAttempts "$MIN_ATTEMPTS" --out "$OUT_DIR"

echo "Exported plan to $OUT_DIR (threshold=$THRESHOLD, minAttempts=$MIN_ATTEMPTS)"
