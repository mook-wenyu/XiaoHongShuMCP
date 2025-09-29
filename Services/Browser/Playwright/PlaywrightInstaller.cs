using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HushOps.Servers.XiaoHongShu.Configuration;
using Microsoft.Extensions.Logging;

namespace HushOps.Servers.XiaoHongShu.Services.Browser.Playwright;

/// <summary>
/// 中文：负责检测并在必要时自动安装 Playwright 浏览器依赖。
/// </summary>
internal static class PlaywrightInstaller
{
    private static readonly SemaphoreSlim InstallationLock = new(1, 1);
    private static bool _installationCompleted;
    private static readonly IReadOnlyList<string> DefaultBrowsers = new[] { "chromium", "ffmpeg" };

    public static async Task EnsureInstalledAsync(PlaywrightInstallationOptions options, ILogger logger, CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _installationCompleted))
        {
            return;
        }

        await InstallationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_installationCompleted)
            {
                return;
            }

            logger.LogInformation("[PlaywrightInstaller] 开始检测并安装浏览器依赖……");

            var args = BuildArguments(options);

            if (options.SkipIfBrowsersPresent && BrowsersAlreadyInstalled(options, logger))
            {
                _installationCompleted = true;
                logger.LogInformation("[PlaywrightInstaller] 检测到浏览器缓存已存在，跳过自动安装。");
                return;
            }

            var originalBrowsersPath = ApplyBrowsersPath(options.BrowsersPath, logger);
            var previousDownloadHost = ApplyDownloadHost(options.DownloadHost, logger);
            try
            {
                var exitCode = await Task.Run(() => Microsoft.Playwright.Program.Main(args), cancellationToken).ConfigureAwait(false);
                if (exitCode != 0)
                {
                    throw new PlaywrightInstallationException($"Playwright 安装失败，退出码：{exitCode}。请尝试手动运行 `{ResolvePlaywrightScriptCommand()}` 并检查网络/代理配置。");
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (PlaywrightInstallationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (!options.IgnoreFailures)
                {
                    throw new PlaywrightInstallationException("Playwright 浏览器安装过程中发生异常，请手动执行安装脚本或检查网络配置。", ex);
                }

                logger.LogWarning(ex, "[PlaywrightInstaller] 自动安装失败，但已配置忽略错误，将继续启动（可能导致后续操作再次失败）。");
            }
            finally
            {
                RestoreBrowsersPath(originalBrowsersPath);
                RestoreDownloadHost(previousDownloadHost);
            }

            _installationCompleted = true;
            logger.LogInformation("[PlaywrightInstaller] 浏览器依赖检测完成。");
        }
        finally
        {
            InstallationLock.Release();
        }
    }

    private static string[] BuildArguments(PlaywrightInstallationOptions options)
    {
        var args = new List<string> { "install" };

        var targetBrowsers = options.Browsers.Count > 0 ? options.Browsers : DefaultBrowsers;
        args.AddRange(targetBrowsers);

        if (options.Arguments.Count > 0)
        {
            args.AddRange(options.Arguments);
        }

        return args.ToArray();
    }

    private static bool BrowsersAlreadyInstalled(PlaywrightInstallationOptions options, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(options.BrowsersPath))
        {
            return false;
        }

        try
        {
            var targetBrowsers = options.Browsers.Count > 0 ? options.Browsers : DefaultBrowsers;
            var root = Path.GetFullPath(options.BrowsersPath);
            if (!Directory.Exists(root))
            {
                return false;
            }

            var msPlaywright = Path.Combine(root, "ms-playwright");
            var searchRoot = Directory.Exists(msPlaywright) ? msPlaywright : root;

            foreach (var browser in targetBrowsers)
            {
                var matches = Directory.EnumerateDirectories(searchRoot, $"{browser}*", SearchOption.TopDirectoryOnly);
                if (!matches.Any())
                {
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[PlaywrightInstaller] 检测浏览器缓存时发生异常，忽略并继续执行安装流程。");
            return false;
        }
    }

    private static string? ApplyBrowsersPath(string? path, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var resolvedPath = Path.GetFullPath(path);
        Directory.CreateDirectory(resolvedPath);
        logger.LogInformation("[PlaywrightInstaller] 使用缓存目录：{Directory}", resolvedPath);

        var previous = Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH");
        Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", resolvedPath);
        return previous;
    }

    private static void RestoreBrowsersPath(string? previous)
    {
        Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", previous);
    }

    private static string? ApplyDownloadHost(string? host, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return null;
        }

        logger.LogInformation("[PlaywrightInstaller] 使用自定义下载镜像：{Host}", host);
        var previous = Environment.GetEnvironmentVariable("PLAYWRIGHT_DOWNLOAD_HOST");
        Environment.SetEnvironmentVariable("PLAYWRIGHT_DOWNLOAD_HOST", host);
        return previous;
    }

    private static void RestoreDownloadHost(string? previous)
    {
        Environment.SetEnvironmentVariable("PLAYWRIGHT_DOWNLOAD_HOST", previous);
    }

    private static string ResolvePlaywrightScriptCommand()
    {
        var baseDirectory = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var scriptPath = Path.Combine(baseDirectory, "playwright.ps1");
        return File.Exists(scriptPath) ? $"pwsh \"{scriptPath}\" install" : "pwsh bin/<Configuration>/<TFM>/playwright.ps1 install";
    }
}
