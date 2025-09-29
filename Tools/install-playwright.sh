#!/usr/bin/env bash
# 中文：跨平台 Playwright 浏览器安装脚本（调用 PowerShell 版本）

set -euo pipefail

CONFIGURATION="Release"
FRAMEWORK="net8.0"
CACHE_PATH=""
DOWNLOAD_HOST=""
FORCE=0
ALLOW_BUILD=0
SKIP_IF_PRESENT=1
DEFAULT_BROWSERS=(chromium ffmpeg)
declare -a BROWSERS=()

ORIGINAL_BROWSERS_PATH="${PLAYWRIGHT_BROWSERS_PATH:-}"
ORIGINAL_DOWNLOAD_HOST="${PLAYWRIGHT_DOWNLOAD_HOST:-}"
APPLIED_BROWSERS_PATH=0
APPLIED_DOWNLOAD_HOST=0

usage() {
  cat <<'EOF'
用法：./install-playwright.sh [选项]

选项：
  -c, --configuration <配置>   指定构建配置（默认 Release）
  -f, --framework <TFM>        指定目标框架（默认 net8.0）
  -p, --cache-path <路径>      指定 Playwright 浏览器缓存目录
      --browser <名称>         指定需安装的浏览器，可多次传入
      --download-host <URL>    指定 Playwright 下载镜像地址（PLAYWRIGHT_DOWNLOAD_HOST）
      --skip-if-present        若缓存目录已存在浏览器则跳过安装（默认启用）
      --no-skip-if-present     强制执行安装流程
      --force                  强制重新安装浏览器
      --allow-build            缺少脚本时尝试运行 dotnet build（仅限维护者使用）
  -h, --help                   显示本帮助

示例：
  ./install-playwright.sh -c Debug --cache-path "$HOME/.cache/ms-playwright"
  ./install-playwright.sh --download-host https://playwrightProxy.example.com --skip-if-present
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    -c|--configuration)
      CONFIGURATION="$2"
      shift 2
      ;;
    -f|--framework)
      FRAMEWORK="$2"
      shift 2
      ;;
    -p|--cache-path)
      CACHE_PATH="$2"
      shift 2
      ;;
    --browser)
      BROWSERS+=("$2")
      shift 2
      ;;
    --download-host)
      DOWNLOAD_HOST="$2"
      shift 2
      ;;
    --skip-if-present)
      SKIP_IF_PRESENT=1
      shift
      ;;
    --no-skip-if-present)
      SKIP_IF_PRESENT=0
      shift
      ;;
    --force)
      FORCE=1
      shift
      ;;
    --allow-build)
      ALLOW_BUILD=1
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "未知参数：$1" >&2
      usage
      exit 1
      ;;
  esac
done

if ! command -v pwsh >/dev/null 2>&1; then
  echo "未检测到 pwsh（PowerShell 7+），请安装后重试。" >&2
  exit 1
fi

browsers_present() {
  local base="$1"
  shift
  local targets=("$@")

  if [[ -z "$base" || ${#targets[@]} -eq 0 ]]; then
    return 1
  fi

  if [[ ! -d "$base" ]]; then
    return 1
  fi

  local search_root="$base/ms-playwright"
  if [[ ! -d "$search_root" ]]; then
    search_root="$base"
  fi

  for target in "${targets[@]}"; do
    shopt -s nullglob
    local matches=("$search_root/${target}"*)
    shopt -u nullglob
    if [[ ${#matches[@]} -eq 0 ]]; then
      return 1
    fi
  done

  return 0
}

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
PROJECT_FILE="${REPO_ROOT}/HushOps.Servers.XiaoHongShu.csproj"
PLAYWRIGHT_SCRIPT="${REPO_ROOT}/bin/${CONFIGURATION}/${FRAMEWORK}/playwright.ps1"

if [[ ! -f "${PLAYWRIGHT_SCRIPT}" ]]; then
  if [[ ${ALLOW_BUILD} -ne 1 ]]; then
    echo "未找到 ${PLAYWRIGHT_SCRIPT}。请使用发布产物中的浏览器包，或在具备 .NET SDK 的开发环境中运行 --allow-build 生成脚本。" >&2
    exit 1
  fi

  if ! command -v dotnet >/dev/null 2>&1; then
    echo "未检测到 dotnet CLI，无法在当前环境执行 --allow-build。请改在具备 .NET SDK 的开发环境生成脚本或使用发布产物。" >&2
    exit 1
  fi

  echo "未找到 ${PLAYWRIGHT_SCRIPT}，执行 dotnet build (${CONFIGURATION})" >&2
  dotnet build "${PROJECT_FILE}" -c "${CONFIGURATION}"
  if [[ ! -f "${PLAYWRIGHT_SCRIPT}" ]]; then
    echo "构建后仍未生成 playwright.ps1，请检查目标框架或配置。" >&2
    exit 1
  fi
fi

if [[ ${#BROWSERS[@]} -gt 0 ]]; then
  TARGET_BROWSERS=("${BROWSERS[@]}")
else
  TARGET_BROWSERS=("${DEFAULT_BROWSERS[@]}")
fi

if [[ ${SKIP_IF_PRESENT} -eq 1 ]]; then
  declare -a CANDIDATE_PATHS=()
  if [[ -n "${CACHE_PATH}" && -d "${CACHE_PATH}" ]]; then
    CANDIDATE_PATHS+=("$(cd "${CACHE_PATH}" && pwd)")
  fi
  if [[ -z "${CACHE_PATH}" && -n "${PLAYWRIGHT_BROWSERS_PATH:-}" ]]; then
    CANDIDATE_PATHS+=("${PLAYWRIGHT_BROWSERS_PATH}")
  fi

  for candidate in "${CANDIDATE_PATHS[@]}"; do
    if browsers_present "${candidate}" "${TARGET_BROWSERS[@]}"; then
      echo "检测到浏览器缓存已存在（${candidate}），跳过自动安装。"
      exit 0
    fi
  done
fi

cleanup() {
  if [[ ${APPLIED_BROWSERS_PATH} -eq 1 ]]; then
    if [[ -n "${ORIGINAL_BROWSERS_PATH}" ]]; then
      export PLAYWRIGHT_BROWSERS_PATH="${ORIGINAL_BROWSERS_PATH}"
    else
      unset PLAYWRIGHT_BROWSERS_PATH
    fi
  fi
  if [[ ${APPLIED_DOWNLOAD_HOST} -eq 1 ]]; then
    if [[ -n "${ORIGINAL_DOWNLOAD_HOST}" ]]; then
      export PLAYWRIGHT_DOWNLOAD_HOST="${ORIGINAL_DOWNLOAD_HOST}"
    else
      unset PLAYWRIGHT_DOWNLOAD_HOST
    fi
  fi
}

trap cleanup EXIT

if [[ -n "${CACHE_PATH}" ]]; then
  mkdir -p "${CACHE_PATH}"
  PLAYWRIGHT_BROWSERS_PATH="$(cd "${CACHE_PATH}" && pwd)"
  export PLAYWRIGHT_BROWSERS_PATH
  APPLIED_BROWSERS_PATH=1
  echo "使用缓存目录：${PLAYWRIGHT_BROWSERS_PATH}"
fi

if [[ -n "${DOWNLOAD_HOST}" ]]; then
  export PLAYWRIGHT_DOWNLOAD_HOST="${DOWNLOAD_HOST}"
  APPLIED_DOWNLOAD_HOST=1
  echo "使用自定义下载镜像：${DOWNLOAD_HOST}"
fi

ARGS=("install")
if [[ ${FORCE} -eq 1 ]]; then
  ARGS+=("--force")
fi
ARGS+=("${TARGET_BROWSERS[@]}")

echo "执行命令：pwsh bin/${CONFIGURATION}/${FRAMEWORK}/playwright.ps1 ${ARGS[*]}"

set +e
pwsh "${PLAYWRIGHT_SCRIPT}" "${ARGS[@]}"
EXIT_CODE=$?
set -e

if [[ ${EXIT_CODE} -ne 0 ]]; then
  echo "Playwright 安装失败，退出码：${EXIT_CODE}" >&2
  exit ${EXIT_CODE}
fi

echo "Playwright 浏览器安装完成。"
