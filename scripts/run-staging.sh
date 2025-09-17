#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT_DIR"

if [[ -f profiles/staging/.env ]]; then
  export $(grep -v '^#' profiles/staging/.env | xargs)
fi

dotnet run --project XiaoHongShuMCP -c Release --no-build

