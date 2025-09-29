#!/usr/bin/env pwsh
<#!
.SYNOPSIS
自动构建并执行 Playwright 浏览器安装命令。

.DESCRIPTION
在仓库根目录定位 `playwright.ps1` 并调用安装命令；默认不触发 `dotnet build`，仅当显式传入 `-BuildWhenMissing` 时才在维护者环境执行构建生成脚本。
支持配置构建配置、目标框架、缓存目录、镜像地址与可选参数。

.PARAMETER Configuration
构建配置（默认 Release）。

.PARAMETER Framework
目标框架（默认 net8.0）。

.PARAMETER CachePath
Playwright 浏览器缓存目录，等同于设置 `PLAYWRIGHT_BROWSERS_PATH`。

.PARAMETER DownloadHost
浏览器下载镜像地址，等同于设置 `PLAYWRIGHT_DOWNLOAD_HOST`。

.PARAMETER SkipIfBrowsersPresent
当缓存目录已存在目标浏览器时跳过安装（默认 true，可通过 `-SkipIfBrowsersPresent:$false` 关闭）。

.PARAMETER Force
是否强制重新安装浏览器，对应 `--force` 选项。

.PARAMETER Browser
指定需安装的浏览器名称，可多次传入。

.PARAMETER BuildWhenMissing
当仓库未包含 `playwright.ps1` 时，允许在已安装 .NET SDK 的开发环境中触发一次 `dotnet build` 以生成脚本。默认关闭，避免要求最终用户安装 .NET SDK。

.EXAMPLE
pwsh Tools/install-playwright.ps1 -Configuration Debug -Framework net8.0

.EXAMPLE
pwsh Tools/install-playwright.ps1 -CachePath "$HOME/.cache/ms-playwright" -DownloadHost https://playwrightProxy.example.com

.EXAMPLE
pwsh Tools/install-playwright.ps1 -BuildWhenMissing -Configuration Release
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Framework = "net8.0",
    [string]$CachePath,
    [string]$DownloadHost,
    [bool]$SkipIfBrowsersPresent = $true,
    [switch]$Force,
    [string[]]$Browser,
    [switch]$BuildWhenMissing
)

$ErrorActionPreference = "Stop"

$defaultBrowsers = @('chromium', 'ffmpeg')

function Write-Info([string]$message)
{
    Write-Host "[install-playwright] $message"
}

function Test-BrowsersInstalled([string]$path, [string[]]$targets)
{
    if ([string]::IsNullOrWhiteSpace($path) -or $targets.Count -eq 0)
    {
        return $false
    }

    try
    {
        $resolved = Resolve-Path -LiteralPath $path -ErrorAction Stop
        $root = $resolved.Path
        if (-not (Test-Path $root -PathType Container))
        {
            return $false
        }

        $searchRoot = Join-Path $root 'ms-playwright'
        if (-not (Test-Path $searchRoot -PathType Container))
        {
            $searchRoot = $root
        }

        foreach ($target in $targets)
        {
            $pattern = "${target}*"
            $match = Get-ChildItem -LiteralPath $searchRoot -Directory -Filter $pattern -ErrorAction SilentlyContinue | Select-Object -First 1
            if (-not $match)
            {
                return $false
            }
        }

        return $true
    }
    catch
    {
        return $false
    }
}

function Resolve-DotnetExecutable
{
    foreach ($name in @('dotnet', 'dotnet.exe'))
    {
        try
        {
            $command = Get-Command $name -ErrorAction Stop | Select-Object -First 1
            if ($command -and -not [string]::IsNullOrWhiteSpace($command.Source))
            {
                return $command.Source
            }
        }
        catch
        {
        }
    }

    return $null
}

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot
$projectFile = Join-Path $repoRoot "HushOps.Servers.XiaoHongShu.csproj"
$playwrightScript = Join-Path $repoRoot "bin/$Configuration/$Framework/playwright.ps1"

if (-not (Test-Path $playwrightScript))
{
    if (-not $BuildWhenMissing.IsPresent)
    {
        throw "未找到 $playwrightScript。请使用发布产物中的浏览器包，或在开发环境中先运行 `pwsh Tools/install-playwright.ps1 -BuildWhenMissing`（需已安装 .NET SDK）。"
    }

    Write-Info "未找到 $playwrightScript，开始执行 dotnet build ($Configuration)。"
    $dotnetExecutable = Resolve-DotnetExecutable
    if (-not $dotnetExecutable)
    {
        throw "未定位到 dotnet 可执行文件，请在具备 .NET SDK 的环境下执行 `-BuildWhenMissing` 或直接下载发布产物。"
    }

    Write-Info "调用 dotnet：$dotnetExecutable build $projectFile -c $Configuration"
    & $dotnetExecutable build $projectFile -c $Configuration
    if ($LASTEXITCODE -ne 0)
    {
        throw "dotnet build 失败，退出码：$LASTEXITCODE"
    }

    if (-not (Test-Path $playwrightScript))
    {
        throw "构建完成后仍未生成 playwright.ps1，请检查构建输出目录或目标框架参数。"
    }
}

$targetBrowsers = if ($Browser -and $Browser.Count -gt 0) { $Browser } else { $defaultBrowsers }

if ($SkipIfBrowsersPresent)
{
    $candidatePaths = @()
    if ($CachePath) { if (Test-Path $CachePath) { $candidatePaths += (Resolve-Path $CachePath -ErrorAction SilentlyContinue).Path } }
    $existingEnvPath = [Environment]::GetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH")
    if (-not [string]::IsNullOrWhiteSpace($existingEnvPath)) { $candidatePaths += $existingEnvPath }

    foreach ($path in ($candidatePaths | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Sort-Object -Unique))
    {
        if (Test-BrowsersInstalled $path $targetBrowsers)
        {
            Write-Info "检测到浏览器缓存已存在（$path），跳过自动安装。"
            return
        }
    }
}

$originalBrowsersPath = $null
$resolvedCachePath = $null
if ($CachePath)
{
    if (-not (Test-Path $CachePath))
    {
        Write-Info "创建浏览器缓存目录：$CachePath"
        New-Item -ItemType Directory -Path $CachePath -Force | Out-Null
    }

    $resolvedCachePath = (Resolve-Path $CachePath).Path
    $originalBrowsersPath = [Environment]::GetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH")
    [Environment]::SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", $resolvedCachePath)
    Write-Info "使用缓存目录：$resolvedCachePath"
}

$originalDownloadHost = $null
if (-not [string]::IsNullOrWhiteSpace($DownloadHost))
{
    $originalDownloadHost = [Environment]::GetEnvironmentVariable("PLAYWRIGHT_DOWNLOAD_HOST")
    [Environment]::SetEnvironmentVariable("PLAYWRIGHT_DOWNLOAD_HOST", $DownloadHost)
    Write-Info "使用自定义下载镜像：$DownloadHost"
}

$arguments = @("install")
if ($Force.IsPresent)
{
    $arguments += "--force"
}

if ($targetBrowsers.Count -gt 0)
{
    $arguments += $targetBrowsers
    Write-Info "安装目标浏览器：$($targetBrowsers -join ', ')"
}
else
{
    $arguments += $defaultBrowsers
    Write-Info "未指定浏览器，默认安装：$($defaultBrowsers -join ', ')"
}

Write-Info "执行命令：pwsh bin/$Configuration/$Framework/playwright.ps1 $($arguments -join ' ')"

try
{
    & $playwrightScript @arguments
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0)
    {
        throw "Playwright 安装失败，退出码：$exitCode"
    }

    Write-Info "浏览器安装完成。"
}
finally
{
    if ($CachePath)
    {
        [Environment]::SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", $originalBrowsersPath)
    }

    if (-not [string]::IsNullOrWhiteSpace($DownloadHost))
    {
        [Environment]::SetEnvironmentVariable("PLAYWRIGHT_DOWNLOAD_HOST", $originalDownloadHost)
    }
}
