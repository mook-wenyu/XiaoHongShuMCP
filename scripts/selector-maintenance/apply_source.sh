#!/usr/bin/env bash
# 中文脚本：将计划直接“物理落地”到源码（危险操作）。
# 用法：
#   PLAN=docs/selector-plans/plan-YYYYMMDD.json bash scripts/selector-maintenance/apply_source.sh --mode reorder|prune --source HushOps/Services/DomElementManager.cs --approve I_KNOW
# 保护：
#   - 必须显式传入 --approve I_KNOW；
#   - 建议先审阅补丁 docs/selector-plans/diff-*.patch 与 ADR 再执行；
#   - 请在专用分支进行，配合代码评审。
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT_DIR/.."

PLAN="${PLAN:-}"   # 需外部传入
MODE="reorder"
SOURCE_FILE="HushOps/Services/DomElementManager.cs"
APPROVE=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --mode) MODE="$2"; shift 2 ;;
    --source) SOURCE_FILE="$2"; shift 2 ;;
    --approve) APPROVE="$2"; shift 2 ;;
    *) echo "未知参数: $1" >&2; exit 2 ;;
  esac
done

if [[ -z "$PLAN" || ! -f "$PLAN" ]]; then
  echo "[apply] 计划文件不存在：请设置环境变量 PLAN 指向 JSON 文件" >&2
  exit 1
fi
if [[ "$APPROVE" != "I_KNOW" ]]; then
  echo "[apply] 缺少 --approve I_KNOW；为避免误操作，拒绝执行。" >&2
  exit 2
fi

echo "[apply] 将对源文件应用计划：plan=$PLAN mode=$MODE source=$SOURCE_FILE"
dotnet build HushOps.sln -c Release --nologo >/dev/null
dotnet run --project XiaoHongShuMCP/XiaoHongShuMCP.csproj -c Release -- selector-plan apply-source --plan "$PLAN" --mode "$MODE" --source "$SOURCE_FILE"
echo "[apply] 已尝试应用，请审阅变更并运行测试：dotnet test Tests -c Release"
