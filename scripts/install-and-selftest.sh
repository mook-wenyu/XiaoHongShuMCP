#!/usr/bin/env bash
set -euo pipefail

MODE=${1:-Debug}

echo "1) 还原与构建解决方案..."
dotnet build -c "$MODE"

TFM=net8.0
BIN_DIR="$(pwd)/XiaoHongShuMCP/bin/${MODE}/${TFM}"
SH="$BIN_DIR/playwright.sh"

echo "2) 安装 Playwright 浏览器..."
bash "$SH" install

echo "3) 自测：列出工具清单（tools-list）..."
export XHS__Serilog__MinimumLevel=Error
export XHS__BrowserSettings__Headless=true
export XHS__BrowserSettings__UserDataDir=UserDataDir
dotnet run --project XiaoHongShuMCP/XiaoHongShuMCP.csproj -- tools-list

echo "完成。"
