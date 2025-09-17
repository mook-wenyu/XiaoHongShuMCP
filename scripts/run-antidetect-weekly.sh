#!/usr/bin/env bash
set -euo pipefail
ROOT_DIR=$(cd "$(dirname "$0")/.." && pwd)
DAYS=${1:-7}
WL=${2:-$ROOT_DIR/docs/anti-detect/whitelist.json}
dotnet run -p "$ROOT_DIR/XiaoHongShuMCP" -- antidetect-weekly "$DAYS" "$WL"

