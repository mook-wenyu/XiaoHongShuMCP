#!/usr/bin/env bash
set -euo pipefail
ROOT_DIR=$(cd "$(dirname "$0")/.." && pwd)
DATE=${1:-}
WL=${2:-$ROOT_DIR/docs/anti-detect/whitelist.json}
dotnet run -p "$ROOT_DIR/XiaoHongShuMCP" -- antidetect-daily ${DATE:+$DATE} "$WL"

