#!/usr/bin/env bash
set -euo pipefail
SCRIPT_DIR=$(cd "$(dirname "$0")" && pwd)
ROOT_DIR=$(cd "$SCRIPT_DIR/.." && pwd)
dotnet run -p "$ROOT_DIR/XiaoHongShuMCP/XiaoHongShuMCP.csproj" -- selector-apply

