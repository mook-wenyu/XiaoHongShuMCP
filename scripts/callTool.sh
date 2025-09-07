#!/usr/bin/env bash
set -euo pipefail

TOOL=${1:-}
shift || true

if [[ -z "$TOOL" ]]; then
  echo "Usage: scripts/callTool.sh <ToolName> [--json '{...}'] [--file args.json]" >&2
  exit 1
fi

dotnet run --project XiaoHongShuMCP -- callTool "$TOOL" "$@"

